//
//  ContentTracker.cs
//
//      Custom ContentManager implementation
//
//      Aranda Morrison 2008
//

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Arands.Content
{
    /// <summary>
    /// Custom content manager. Allows for removing individual items and provides
    /// detailed information about asset relationships
    /// </summary>
    public partial class ContentTracker : ContentManager
    {
        #region Constructors

        /// <summary>
        /// Constructs a ContentTracker
        /// </summary>
        /// <param name="serviceProvider">The provider object for getting services</param>
        public ContentTracker(IServiceProvider serviceProvider)
            : base(serviceProvider)
        { }

        /// <summary>
        /// Constructs a ContentTracker
        /// </summary>
        /// <param name="serviceProvider">The provider object for getting services</param>
        /// <param name="rootDirectory">The root directory of the ContentTracker</param>
        public ContentTracker(IServiceProvider serviceProvider, string rootDirectory)
            : base(serviceProvider, rootDirectory)
        { }

        #endregion

        #region Member Variables

        /// <summary>
        /// All loaded AssetTrackers, keyed by the asset name
        /// </summary>
        Dictionary<string, AssetTracker> loadedAssets = new Dictionary<string, AssetTracker>();

        /// <summary>
        /// List of loaded asset names. Useful for statistics
        /// </summary>
        List<string> loadedAssetNames = new List<string>();

        /// <summary>
        /// A list of AssetTrackers that were loaded directly from disk. 
        /// They can not be reloaded or unloaded individually as they do not have unique names.
        /// They do need to be stored so they can be Disposed when the ContentTracker is Unloaded.
        /// </summary>
        List<AssetTracker> untrackedAssets = new List<AssetTracker>();

        /// <summary>
        /// The assets currently being loaded. Use a Stack here as 
        /// Load<T>() is called recursively. Top AssetTracker is
        /// currently being loaded
        /// </summary>
        Stack<AssetTracker> loadingAssetsStack = new Stack<AssetTracker>();

        #endregion

        #region Properties

        /// <summary>
        /// Returns true if the ContentTracker is currently loading.
        /// Returns false otherwise
        /// </summary>
        public bool IsLoading
        {
            get { return loadingAssetsStack.Count > 0; }
        }

        /// <summary>
        /// Tells the ContentTracker to attempt to load source asset types, if they exist.
        /// </summary>
        public bool UseSourceAssets
        {
            get;
            set;
        }

        #endregion

        #region ContentManager overrides


        /// <summary>
        /// Return the specified asset. Read it from disk if not available
        /// </summary>
        /// <typeparam name="T">The Type of asset to load</typeparam>
        /// <param name="assetName">Name of the asset to load</param>
        /// <returns>A reference to the loaded asset</returns>
        public override T Load<T>(string assetName)
        {
            return LoadToTracker<T>(assetName, null, false);
        }

        /// <summary>
        /// Return the specified asset. ALWAYS Read it from disk
        /// </summary>
        /// <typeparam name="T">The Type of asset to load</typeparam>
        /// <param name="assetName">Name of the asset to load</param>
        /// <returns>A reference to the loaded asset</returns>
        public T LoadFromDisk<T>(string assetName)
        {
            return LoadToTracker<T>(assetName, null, true);
        }

        /// <summary>
        /// Return the specified asset. Read it from disk if not available
        /// 
        /// </summary>
        /// <typeparam name="T">The Type of asset to load</typeparam>
        /// <param name="assetName">Name of the asset to load</param>
        /// <param name="tracker">
        /// An AssetTracker to use when loading this asset. May be null
        /// </param>
        /// <returns>A reference to the loaded asset</returns>
        public T LoadToTracker<T>(string assetName, AssetTracker tracker, bool forceReadAsset)
        {
            // Return asset if currently loaded
            if (!forceReadAsset && loadedAssets.ContainsKey(assetName))
            {
                // Get asset tracker
                AssetTracker trackerExisting = loadedAssets[assetName];

                // Get asset as correct type
                T asset = (T)trackerExisting.Asset;

                // Increment tracker's reference count after the cast as the cast will  
                // throw an exception if the incorrect generic type parameter is given 
                trackerExisting.RefCount++;

                // Maintain the reference lists to show that this asset was loaded by
                // the asset on the top of the stack
                if (loadingAssetsStack.Count > 0)
                {
                    loadingAssetsStack.Peek().RefersTo.Add(assetName);
                    trackerExisting.ReferredToBy.Add(loadingAssetsStack.Peek().AssetName);
                }

                return asset;
            }

            // Need to load the asset. Create an AssetTracker to track it
            // unless we have been passed an existing AssetTracker
            if (tracker == null)
            {
                // Initialise tracker
                tracker = new AssetTracker();
                tracker.RefCount = 1;
                tracker.AssetName = assetName;
            }

            // Stack count will be zero if called by user. 
            // Otherwise, Load<T> was called internally by ReadAsset<T>
            if (loadingAssetsStack.Count > 0)
            {
                // Maintain the reference lists
                
                // The asset on the top of the stack refers to this asset
                loadingAssetsStack.Peek().RefersTo.Add(assetName);

                // This asset was loaded by the asset on the top of the stack
                tracker.ReferredToBy.Add(loadingAssetsStack.Peek().AssetName);
            }

            // Put current asset tracker on top of the stack 
            // for next call to Load<T>
            loadingAssetsStack.Push(tracker);

            try
            {
                // Preparation complete. Now finally read the asset from disk. 
                // This is where the internal magic happens. 
#if WINDOWS
                string fileName;
                if (UseSourceAssets && TrySearchForValidAssetSource<T>(assetName, out fileName))
                {
                    tracker.Asset = SourceAssetLoaders[typeof(T)].loadAssetDelegate(fileName);
                }
                else
#endif
                tracker.Asset = ReadAsset<T>(assetName, tracker.TrackDisposableAsset);

                // Ensure the list of disposables doesn't refer to the 
                // actual asset, or to any assets in the loadedAssets Dictionary.
                // Best to do this now to avoid multiple disposing later
                tracker.Disposables.RemoveAll(delegate(IDisposable d)
                {
                    string tmp = "";
                    return tracker.Asset == d || SearchForAsset(d, out tmp);
                });
            
            }
            finally
            {
                // Asset has been loaded so the top tracker is not needed on the stack
                loadingAssetsStack.Pop();
            }

            // Store the asset and it's disposables list
            if (forceReadAsset)
                untrackedAssets.Add(tracker);
            else
            {
                loadedAssets.Add(assetName, tracker);
                loadedAssetNames.Add(assetName);
            }

            // Mark tracker as ready to use
            tracker.Status = AssetStatus.Active;

            // Return loaded asset
            return (T)tracker.Asset;
        }


        /// <summary>
        /// Clean up all assets
        /// </summary>
        public override void Unload()
        {
            // Dispose all IDisposables now
            Dictionary<string, AssetTracker>.Enumerator enumer = loadedAssets.GetEnumerator();
            while (enumer.MoveNext())
            {
                // Fire change event. New asset argument is null
                // to represent unload of the item
                enumer.Current.Value.OnAssetChanged(null);
                
                // Don't release children as all assets 
                // will be unloaded in this loop
                DisposeAssetTracker(enumer.Current.Value, false);
            }

            loadedAssets.Clear();

            // Dispose all untracked assets to
            foreach (AssetTracker uta in untrackedAssets)
            {
                uta.OnAssetChanged(null);
                DisposeAssetTracker(uta, false);
            }
            untrackedAssets.Clear();

            // Stop and cleanup the loading thread
            if (loadThread != null)
            {
                try
                {
                    mCloseRequested = true;
                    loadResetEvent.Set();
                    loadThread.Join();
                    loadThread = null;
                }
                catch { }
            }
        }

        #endregion

        #region ContentTracker Methods

        /// <summary>
        /// Release asset. Decrements the reference count and
        /// removes child assets when the count is zero
        /// </summary>
        /// <param name="assetName">Asset to release</param>
        public void Release(string assetName)
        {
            if (loadedAssets.ContainsKey(assetName))
            {
                AssetTracker tracker = loadedAssets[assetName];
                tracker.RefCount--;
            
                if (tracker.RefCount == 0)
                {
                    tracker.OnAssetChanged(null);
                    DisposeAssetTracker(tracker, true);

                    // Remove from dictionary
                    loadedAssets.Remove(assetName);
                    loadedAssetNames.Remove(assetName);
                }
            }
        }

        /// <summary>
        /// Force an asset to be disposed. Optionally releases child assets 
        /// </summary>
        /// <param name="assetName">Name of asset to unload</param>
        /// <param name="releaseChildren">Release child assets</param>
        public void Unload(string assetName, bool releaseChildren)
        {
            if (loadedAssets.ContainsKey(assetName))
            {
                AssetTracker tracker = loadedAssets[assetName];

                // Fire changed event
                tracker.OnAssetChanged(null);

                // Destroy disposables
                DisposeAssetTracker(tracker, releaseChildren);

                // Remove from dictionary
                loadedAssets.Remove(assetName);
                loadedAssetNames.Remove(assetName);
            }
        }


        /// <summary>
        /// Destroy IDisposables that were tracked by this asset but do not 
        /// exist as assets in their own right. This will also dispose the
        /// unmanaged internals like vertex and index buffers
        /// </summary>
        /// <param name="tracker">AssetTracker to dispose</param>
        /// <param name="releaseChildren">If true, child assets will be released</param>
        private void DisposeAssetTracker(AssetTracker tracker, bool releaseChildren)
        {
            // Invoke asset changed event.
            tracker.OnAssetChanged(null);

            // Mark tracker as disposed
            tracker.Status = AssetStatus.Disposed;

            // Destroy tracked disposables
            foreach (IDisposable disposable in tracker.Disposables)
            {
                disposable.Dispose();
            }

            // Dispose the actual asset, if possible
            if (tracker.Asset is IDisposable)
                ((IDisposable)tracker.Asset).Dispose();

            // Handle child assets
            foreach (string childAsset in tracker.RefersTo)
            {
                if (loadedAssets.ContainsKey(childAsset))
                {
                    // Maintain child reference list
                    loadedAssets[childAsset].ReferredToBy.Remove(tracker.AssetName);

                    // release child assets if requested
                    if (releaseChildren)
                        Release(childAsset);
                }
            }
        }

        /// <summary>
        /// Reloads the specified asset.
        /// </summary>
        /// <typeparam name="T">The Type of asset to reload</typeparam>
        /// <param name="assetName">Name of the asset to load</param>
        /// <returns>The new reference to the reloaded asset</returns>
        public T Reload<T>(string assetName)
        { 
            if (loadedAssets.ContainsKey(assetName))
            {
                AssetTracker oldAssetTracker = loadedAssets[assetName];

                // Remove tracker so Load<T>() will create a new one
                loadedAssets.Remove(assetName);
                loadedAssetNames.Remove(assetName);

                // Load it again
                T asset = Load<T>(assetName);

                // Invoke AssetChanged event
                oldAssetTracker.OnAssetChanged(asset);

                // Destroy previous tracker
                DisposeAssetTracker(oldAssetTracker, true);

                return asset;
            }
            else
                return Load<T>(assetName);
        }


        /// <summary>
        /// Tests if the specified asset is loaded
        /// </summary>
        /// <param name="assetName">Name of asset to test</param>
        /// <returns>True if asset is loaded otherwise false</returns>
        public bool IsLoaded(string assetName)
        {
            if (loadedAssets.ContainsKey(assetName))
                return true;

            return false;
        }

        /// <summary>
        /// Returns the Asset Tracker
        /// </summary>
        /// <param name="assetName">Name of asset to get tracker for</param>
        /// <returns>The AssetTracker with specified name, or null if not existing</returns>
        public AssetTracker GetTracker(string assetName)
        {
            if (!loadedAssets.ContainsKey(assetName))
                return null;

            return loadedAssets[assetName];
        }

        /// <summary>
        /// Gets an Asset's reference count
        /// </summary>
        /// <param name="assetName">Name of asset to get reference count for</param>
        /// <returns>Reference count of specified asset</returns>
        public int GetReferenceCount(string assetName)
        {            
            // Return zero if not currently loaded
            if (!loadedAssets.ContainsKey(assetName))
                return 0;

            return loadedAssets[assetName].RefCount;
        }

        /// <summary>
        /// Get the names of all assets that are currently loaded
        /// </summary>
        /// <returns>List of loaded asset names</returns>
        public List<string> GetLoadedAssetNames()
        {
            return loadedAssetNames;
        }

        /// <summary>
        /// Check if the specified object is a loaded asset
        /// </summary>
        /// <param name="asset">asset reference to search for</param>
        /// <param name="assetName">Name asset with specified reference if found</param>
        /// <returns>True if the specified asset was found and false otherwise</returns>
        public bool SearchForAsset(object asset, out string assetName)
        {
            // Enumerate all loaded assets
            Dictionary<string, AssetTracker>.Enumerator enumer = loadedAssets.GetEnumerator();
            while (enumer.MoveNext())
            {
                if (asset == enumer.Current.Value.Asset)
                {
                    // Found asset
                    assetName = enumer.Current.Key;
                    return true;
                }
            }
            assetName = "";
            return false;
        }

        #endregion

    }
}
