using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Reflection;

namespace Arands.Content
{
    /// <summary>
    /// Async section of ContentTracker
    /// </summary>
    public partial class ContentTracker
    {
        /// <summary>
        /// Signature of a method to call when an 
        /// asset finishes loading asyncronously
        /// </summary>
        /// <param name="asset"></param>
        public delegate void AssetLoaded(object asset);

        /// <summary>
        /// The paramaters required to asyncronously load an asset
        /// </summary>
        class LoadAsyncParams
        {
            /// <summary>
            /// The Type of the asset to load. This will be the generic
            /// type parameter when calling Load<T>
            /// </summary>
            internal Type AssetType;

            /// <summary>
            /// The AssetTracker to pass to Load<T>. Also 
            /// stores the asset name to load
            /// </summary>
            internal AssetTracker Tracker;

            /// <summary>
            /// Method to invoke when the asset is loaded
            /// </summary>
            internal List<AssetLoaded> ItemLoadedMethods = new List<AssetLoaded>();

            /// <summary>
            /// Constructor for convenience
            /// </summary>
            internal LoadAsyncParams(Type type, string name, AssetLoaded loadedMethod)
            {
                AssetType = type;
                ItemLoadedMethods.Add(loadedMethod);
                Tracker = new AssetTracker();
                Tracker.AssetName = name;
                Tracker.RefCount = 1;
                Tracker.Status = AssetStatus.Loading;
            }
        }

        ///////////////////////////
        // Threaded loading members


        /// <summary>
        /// Queue of items to be loaded
        /// </summary>
        Queue<LoadAsyncParams> loadItemsQueue;

        /// <summary>
        /// Thread on which asyncronous loading will occur
        /// </summary>
        Thread loadThread;

        /// <summary>
        /// Reset event so load thread can wait once the queue is empty
        /// </summary>
        AutoResetEvent loadResetEvent;

        /// <summary>
        ///  A flag to request the thread to return from its loop
        /// </summary>
        private volatile bool mCloseRequested;


    /// <summary>
    /// Asyncronously loads the specified asset
    /// </summary>
    /// <typeparam name="T">Generic type parameter</typeparam>
    /// <param name="assetName">Name of asset to laod</param>
    /// <param name="itemLoadedMethod">Method to call once load is completed</param>
    /// <returns>AssetTracker of asset to be loaded. Allows 
    /// users to poll the asset status if desired</returns>
    public AssetTracker LoadAsync<T>(string assetName, AssetLoaded itemLoadedMethod)
    {
        AssetTracker tracker = null;

        // Check if asset is already loaded
        if (loadedAssets.ContainsKey(assetName))
        {
            tracker = loadedAssets[assetName];

            // Increment reference count
            tracker.RefCount++;

            // Call the specified item loaded method
            if (itemLoadedMethod != null)
                itemLoadedMethod(tracker.Asset);
        }
        else
        {
            if (loadThread == null)
            {
                // First time LoadAsync has been called so 
                // initialise thread, reset event and queue
                loadThread = new Thread(new ThreadStart(LoadingThreadWorker));
                loadThread.Name = "File Loading Worker";

                loadItemsQueue = new Queue<LoadAsyncParams>();
                loadResetEvent = new AutoResetEvent(false);
                
                //reset the request flag to close the thread
                mCloseRequested = false;

                // Start thread. It will wait once queue is empty
                loadThread.Start();
            }

            // Create the async argument structure and enqueue it for async load.
            lock (loadItemsQueue)
            {
                // first check if this item is already enqueued
                Queue<LoadAsyncParams>.Enumerator enumer = loadItemsQueue.GetEnumerator();
                while (enumer.MoveNext())
                {
                    if (enumer.Current.Tracker.AssetName == assetName)
                    {
                        // Register the itemLoaded method
                        enumer.Current.ItemLoadedMethods.Add(itemLoadedMethod);
                        tracker = enumer.Current.Tracker;
                        tracker.RefCount++;
                        break;
                    }
                }

                // Item not already queued for loading
                if (tracker == null)
                {
                    LoadAsyncParams args = new LoadAsyncParams(typeof(T), assetName, itemLoadedMethod);
                    tracker = args.Tracker;
                    loadItemsQueue.Enqueue(args);
                }
            }
            
            // Tell loading thread to stop waiting
            loadResetEvent.Set();
        }

        // Return tracker. Allows async caller to poll loaded status
        return tracker;
    }

        /// <summary>
        /// Consume the Queue of assets to be loaded then wait 
        /// </summary>
        void LoadingThreadWorker()
        {
            LoadAsyncParams args;

            while (!mCloseRequested)
            {
                while (loadItemsQueue.Count > 0 && !IsLoading)
                {
                    // Get next item to process
                    lock (loadItemsQueue)
                    {
                        args = loadItemsQueue.Peek();
                    }

                    // Process head queue entry
                    CallGenericLoad(args);

                    // Ensure Load<T> correctly added AssetTracker to the dictionary
                    if (loadedAssets.ContainsKey(args.Tracker.AssetName))
                    {
                        // Call back the item loaded methods
                        foreach (AssetLoaded method in args.ItemLoadedMethods)
                        {
                            if (method != null)
                                method.Invoke(args.Tracker.Asset);
                        }

                        // The asset is now ready to use
                        args.Tracker.Status = AssetStatus.Active;
                    }

                    // Remove processed item. Can't be removed until
                    // loading complete as new async requests may need
                    // to add AssetLoaded methods to it's list
                    lock (loadItemsQueue)
                    {
                        loadItemsQueue.Dequeue();
                    }
                }

                // Wait until next load call
                loadResetEvent.WaitOne();
            }
        }

        /// <summary>
        /// Calls the private Load<T>(string, AssetTracker) method.
        /// Reflection is needed to use an arbitrary generic type parameter
        /// </summary>
        /// <param name="loadArgs"></param>
        void CallGenericLoad(LoadAsyncParams loadArgs)
        {
            // The AssetType must not be null
            if (loadArgs.AssetType == null)
                return;

            MethodInfo closedMethod = null;

            // Unfortunate workaround to get it working on the XBox 360. 
            // Just using GetMethod fails on the generic methods.
            MethodInfo[] mis = typeof(ContentTracker).GetMethods();
            for (int i = 0; i < mis.Length; i++)
            {
                if (mis[i].Name == "LoadToTracker")
                {
                    closedMethod = mis[i].MakeGenericMethod(loadArgs.AssetType);
                    break;
                }
            }

            ////////////////////////////
            // Works on PC but not XBox 360.
            //// Get the method info
            //MethodInfo genericMethod =
            //    typeof(ContentTracker).GetMethod(
            //        "LoadToTracker",
            //        BindingFlags.Public | BindingFlags.Instance,
            //        null,
            //        new Type[] { typeof(string), typeof(AssetTracker) },
            //        null);

            //// Supply the generic method with the type parameter
            //closedMethod = genericMethod.MakeGenericMethod(loadArgs.AssetType);
            ////////////////////////

            // Invoke the load method
            if (closedMethod != null)
            {
                closedMethod.Invoke(
                    this,
                    new object[] { 
                        loadArgs.Tracker.AssetName, 
                        loadArgs.Tracker,
                        false });
            }
        }
    }
}
