using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading;
using Arands.Content;

namespace ContentTrackerTestGame
{
    /// <summary>
    /// A group of models that can be loaded, 
    /// drawn and unloaded together.
    /// </summary>
    class WorldChunk
    {
        public Matrix Transform;
        public List<ModelInfo> Models = new List<ModelInfo>();
        public bool Active;
        public BoundingSphere Bounds;

        ContentTracker content;

        public WorldChunk(Matrix worldTrans, string[] modelArray)
        {
            Transform = worldTrans;

            // build the list of ModelInfo
            for (int i = 0; i < modelArray.Length; i++)
            {
                ModelInfo mi = new ModelInfo();
                mi.ModelName = modelArray[i];
                Models.Add(mi);
            }
        }

        /// <summary>
        /// Call LoadContent on the child ModelInfo objects
        /// and set Active flag to true
        /// </summary>
        /// <param name="contentTracker"></param>
        /// <param name="loadAsync"></param>
        public void LoadContent(ContentTracker contentTracker, bool loadAsync)
        {
            content = contentTracker;

            foreach (ModelInfo mi in Models)
            {
                mi.LoadContent(contentTracker, loadAsync);
            }
            Active = true;
        }

        /// <summary>
        /// Call ReleaseContent on the child ModelInfo objects
        /// and set Active flag to false
        /// </summary>
        public void ReleaseContent()
        {
            foreach (ModelInfo mi in Models)
            {
                content.Release(mi.ModelName);
            }
            Active = false;
        }

        /// <summary>
        /// Draw all models in this chunk, if chunk is active
        /// </summary>
        /// <param name="view"></param>
        /// <param name="proj"></param>
        public void Draw(ref Matrix view, ref Matrix proj)
        {
            if (!Active)
                return;

            foreach (ModelInfo mi in Models)
            {
                if (mi.Model == null)
                    continue;

                if (mi.Tracker.Status != AssetStatus.Active)
                    continue;

                foreach (ModelMesh mm in mi.Model.Meshes)
                {
                    foreach (BasicEffect be in mm.Effects)
                    {
                        be.World = Matrix.CreateFromQuaternion(mi.Rotation) * Matrix.CreateTranslation(mi.Position) * Transform;
                        be.View = view;
                        be.Projection = proj;
                    }

                    mm.Draw();
                }
            }
        }
    }
}
