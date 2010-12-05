using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Arands.Content;

namespace ContentTrackerTestGame
{
    /// <summary>
    /// Encapsulates a Model instance and it's parameters
    /// </summary>
    public class ModelInfo
    {
        public string ModelName;
        public Vector3 Position;
        public Quaternion Rotation;
        public bool FogEnable = true;
        
        public Model Model;
        public AssetTracker Tracker;

        public void LoadContent(ContentTracker content, bool loadAsync)
        {
            if (loadAsync)
            {
                // Store AssetTracker for status checking 
                Tracker = content.LoadAsync<Model>(ModelName, AssetLoaded);
            }
            else
            {
                // Normal non-threaded loading
                Model = content.Load<Model>(ModelName);

                // Call loaded method to assign effect parameters
                AssetLoaded(Model);

                // Get the tracker. We want to use the AssetChanged event
                Tracker = content.GetTracker(ModelName);
                
            }

            // Respond to the AssetChanged callback.
            // This allows us to ensure the asset reference is up-to-date.
            if (Tracker != null)
            {
                Tracker.AssetChanged += AssetLoaded;
            }
        }

        // This method will be called whenever the 
        // asset is loaded/reloaded or unloaded
        public void AssetLoaded(object model)
        {
            // Assign our model reference
            Model = model as Model;

            if (model == null)
                return;

            // Assign effect params
            foreach (ModelMesh mm in Model.Meshes)
            {
                foreach (Effect e in mm.Effects)
                {
                    BasicEffect be = e as BasicEffect;
                    if (be == null)
                        continue;

                    // For demo purposes, use hard code global values
                    // to initialise effect parameters
                    be.SpecularColor = TestGame.SpecColor;
                    be.SpecularPower = 10.0f;
                    be.EnableDefaultLighting();
                    be.PreferPerPixelLighting = true;
                    be.FogEnabled = FogEnable;
                    if (FogEnable)
                    {
                        be.FogColor = TestGame.FogColor;
                        be.FogStart = TestGame.FarClip / 4.0f;
                        be.FogEnd = TestGame.FarClip - 10.0f;
                    }
                }
            }
        }
    }
}
