using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Arands.Content
{
    /// <summary>
    /// Describes the load status of an asset
    /// </summary>
    public enum AssetStatus { Active, Loading, Disposed }

    /// <summary>
    /// Tracks individual asset information
    /// </summary>
    public class AssetTracker
    {
        /// <summary>
        /// Name of the asset.
        /// Used as the key to look up this AssetTracker
        /// </summary>
        public string AssetName;

        /// <summary>
        /// The actual asset
        /// </summary>
        public object Asset;

        /// <summary>
        /// Disposables owned by this asset
        /// </summary>
        public List<IDisposable> Disposables = new List<IDisposable>();

        /// <summary>
        /// Asset's reference count.
        /// Asset will be unloaded when this reaches zero
        /// </summary>
        public int RefCount;

        /// <summary>
        /// Asset's current load status.
        /// Can be used to check when threaded loading completes
        /// </summary>
        public AssetStatus Status = AssetStatus.Disposed;

        /// <summary>
        /// Assets that we reference (children)
        /// </summary>
        public List<string> RefersTo = new List<string>();

        /// <summary>
        /// Assets that reference us (parents)
        /// </summary>
        public List<string> ReferredToBy = new List<string>();
        
        /// <summary>
        /// This method is an Action<IDisposable>, allowing
        /// ReadAsset<T>() to track the disposables for this asset 
        /// </summary>
        /// <param name="disposable">An IDisposable referenced by this asset</param>
        public void TrackDisposableAsset(IDisposable disposable)
        {
            Disposables.Add(disposable);
        }
        
        /// <summary>
        /// Callback invoked when this asset is reloaded or unloaded
        /// </summary>
        public Action<object> AssetChanged;

        /// <summary>
        /// Helper for invoking AssetChanged. 
        /// Marked as internal because it's invoked by ContentTracker. 
        /// </summary>
        protected internal void OnAssetChanged(object newReference)
        {
            if (AssetChanged != null)
                AssetChanged(newReference);
        }
    }
}
