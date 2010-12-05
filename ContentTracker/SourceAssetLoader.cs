using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System.IO;

namespace Arands.Content
{
    public partial class ContentTracker
    {
        /// <summary>
        /// Registered source asset types
        /// </summary>
        Dictionary<Type, ValidSourceAssetDesc> SourceAssetLoaders = new Dictionary<Type, ValidSourceAssetDesc>();

        /// <summary>
        /// Internal class for storing source asset loading information
        /// </summary>
        class ValidSourceAssetDesc
        {
            internal string[] validExtensions;
            internal Func<string, object> loadAssetDelegate;

            public ValidSourceAssetDesc(string[] exts, Func<string, object> loadDelegate)
            {
                validExtensions = exts;
                loadAssetDelegate = loadDelegate;
            }
        }

        /// <summary>
        /// Causes ContentTracker to attempt to find and load source assets of the given Type.
        /// UseSourceAssets must also be set to true for this to happen
        /// </summary>
        /// <param name="assetType">Asset Type to search for source assets</param>
        /// <param name="validExtensions">An array of file extensions (eg ".png") that are valid for the asset Type</param>
        /// <param name="loadAssetDelegate">Method to call when a valid source file is found</param>
        public void RegisterSourceAssetLoader(Type assetType, string[] validExtensions, Func<string, object> loadAssetDelegate)
        {
            if (loadAssetDelegate == null)
                return;

            SourceAssetLoaders[assetType] = new ValidSourceAssetDesc(validExtensions, loadAssetDelegate);
        }

        private bool TrySearchForValidAssetSource<T>(string assetName, out string fileName)
        {
            if (!SourceAssetLoaders.ContainsKey(typeof(T)))
            {
                fileName = null;
                return false;
            }

            ValidSourceAssetDesc assetLoader = SourceAssetLoaders[typeof(T)];

            // Determine folder of requested asset
            string searchFolder;
            if (Path.IsPathRooted(assetName))
                searchFolder = Path.GetDirectoryName(assetName);
            else
                searchFolder = Path.Combine(this.RootDirectory, Path.GetDirectoryName(assetName));

            // Get all files in folder with same name as asset
            string[] files = Directory.GetFiles(searchFolder, Path.GetFileName(assetName) + ".*", SearchOption.TopDirectoryOnly);
            foreach (string f in files)
            {
                // Find first file with a valid extension
                foreach (string ext in assetLoader.validExtensions)
                {
                    if (f.ToLower().EndsWith(ext))
                    {
                        fileName = f;
                        return true;
                    }
                }
            }
            fileName = "";
            return false;
        }
    }
}