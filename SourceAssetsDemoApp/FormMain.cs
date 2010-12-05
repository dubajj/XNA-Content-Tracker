using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using SD = System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using System.Diagnostics;
using System.Reflection;
using Arands.Content;
using X2Model;

namespace SourceAssetsDemoApp
{
    public partial class FormMain : Form
    {
        ContentTracker content;
        Model currentModel;
        Stopwatch timer = new Stopwatch();
        float timeDelta;
        float timeTotal;

        Matrix mtxView;
        Matrix mtxProj;

        public FormMain()
        {
            InitializeComponent();

            this.Load += new EventHandler(FormMain_Load);
            this.Resize += new EventHandler(FormMain_Resize);

            Application.Idle += new EventHandler(Application_Idle);
            timer.Start();            
        }

        void Application_Idle(object sender, EventArgs e)
        {
            while (AppStillIdle())
            {
                gdcModel.Invalidate();
                timeDelta = (float)timer.Elapsed.TotalSeconds;
                timeTotal += timeDelta;
                timer.Reset();
                timer.Start();
            }
        }

        private bool AppStillIdle()
        {
            NativeMethods.Message msg;
            return !NativeMethods.PeekMessage(out msg, Handle, 0, 0, 0);
        }

        void Draw()
        {
            gdcModel.GraphicsDevice.Clear(Color.DarkGray);

            if (currentModel != null)
                DrawModel(currentModel);
        }
        
        private void DrawModel(Model m)
        {
            foreach (ModelMesh mm in m.Meshes)
            {
                foreach (Effect fx in mm.Effects)
                {
                    if (fx is BasicEffect)
                    {
                        BasicEffect bfx = fx as BasicEffect;
                        bfx.World = Matrix.CreateRotationY(timeTotal);
                        bfx.View = mtxView;
                        bfx.Projection = mtxProj;
                    }
                }
                mm.Draw();
            }
        }

        void FormMain_Load(object sender, EventArgs e)
        {
            gdcModel.DrawMethod = Draw;

            content = new ContentTracker(gdcModel.Services);
            content.UseSourceAssets = true;
            content.RegisterSourceAssetLoader(typeof(Model), new string[] { ".x" }, LoadModelSource);
        }

        /// <summary>
        /// The function to pass as a delegate to ContentTracker for loading X files
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        object LoadModelSource(string fileName)
        {
            return X2Model.ModelLoader.ModelFromFile(gdcModel.GraphicsDevice, fileName);
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.DefaultExt = ".x";
            ofd.Title = "Open X File...";
            ofd.Filter = "X Files (*.x)|*.x|All Files (*.*) |*.*";
            ofd.FileName = "";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    BoundingSphere bounds;

                    content.RootDirectory = System.IO.Path.GetDirectoryName(ofd.FileName);
                    currentModel = content.Load<Model>(System.IO.Path.GetFileNameWithoutExtension(ofd.FileName));
                    bounds = MeasureModel(currentModel);
                    //OutputDebugInfo(); // This is very slow!

                    mtxView = Matrix.CreateLookAt(
                        bounds.Center + new Vector3(0.0f, bounds.Radius * 0.0f, bounds.Radius * 1.2f), 
                        bounds.Center, 
                        Vector3.UnitY);

                    mtxProj = Matrix.CreatePerspectiveFieldOfView(
                        MathHelper.PiOver2,
                        (float)gdcModel.Width / (float)gdcModel.Height,
                        1.0f, 10000.0f);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Failed to load X file");
                }
            }
        }

        void FormMain_Resize(object sender, EventArgs e)
        {
            // Keep correct aspect ratio
            mtxProj = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.PiOver2, 
                (float)gdcModel.Width / (float)gdcModel.Height, 
                1.0f, 10000.0f);
        }

        /// <summary>
        /// Whenever a new model is selected, we examine it to see how big
        /// it is and where it is centered. This lets us automatically zoom
        /// the display, so we can correctly handle models of any scale.
        /// </summary>
        BoundingSphere MeasureModel(Model model)
        {
            // Look up the absolute bone transforms for this model.
            Matrix[] boneTransforms = new Matrix[model.Bones.Count];

            model.CopyAbsoluteBoneTransformsTo(boneTransforms);

            // Compute an (approximate) model center position by
            // averaging the center of each mesh bounding sphere.
            Vector3 modelCenter = Vector3.Zero;

            foreach (ModelMesh mesh in model.Meshes)
            {
                BoundingSphere meshBounds = mesh.BoundingSphere;
                Matrix transform = boneTransforms[mesh.ParentBone.Index];
                Vector3 meshCenter = Vector3.Transform(meshBounds.Center, transform);

                modelCenter += meshCenter;
            }

            modelCenter /= model.Meshes.Count;

            // Now we know the center point, we can compute the model radius
            // by examining the radius of each mesh bounding sphere.
            float modelRadius = 0;

            foreach (ModelMesh mesh in model.Meshes)
            {
                BoundingSphere meshBounds = mesh.BoundingSphere;
                Matrix transform = boneTransforms[mesh.ParentBone.Index];
                Vector3 meshCenter = Vector3.Transform(meshBounds.Center, transform);

                float transformScale = transform.Forward.Length();

                float meshRadius = (meshCenter - modelCenter).Length() +
                                   (meshBounds.Radius * transformScale);

                modelRadius = Math.Max(modelRadius, meshRadius);
            }

            return new BoundingSphere(modelCenter, modelRadius);
        }

        void OutputDebugInfo()
        {
            foreach (ModelMesh mm in currentModel.Meshes)
            {
                VertexDeclaration decl = mm.MeshParts[0].VertexDeclaration;
                Debug.Write("Vert Decl: ");
                foreach (VertexElement elem in decl.GetVertexElements())
                {
                    Debug.Write(string.Format("[{0}, {1}, {2}] ", elem.Offset, elem.VertexElementFormat, elem.VertexElementUsage));
                }
                Debug.Write("\n");

                Debug.WriteLine("Mesh Vert Data: " + mm.Name);
                float[] vertData = new float[mm.VertexBuffer.SizeInBytes / sizeof(float)];
                mm.VertexBuffer.GetData<float>(vertData);

                for (int i = 0; i < vertData.Length; i++)
                {
                    // Insert new line at vert boundary
                    if (i > 0 && i % (decl.GetVertexStrideSize(0) / 4) == 0)
                        Debug.Write("\n");

                    Debug.Write(string.Format("{0:F1} ", vertData[i]));
                }
                Debug.Write("\n");

                Debug.WriteLine("Index Data: " + mm.Name);
                short[] indexData = new short[mm.IndexBuffer.SizeInBytes / sizeof(short)];
                mm.IndexBuffer.GetData<short>(indexData);

                for (int i = 0; i < indexData.Length; i++)
                {
                    // Insert new line at vert boundary
                    if (i > 0 && i % 3 == 0)
                        Debug.Write("\n");

                    Debug.Write(string.Format("{0} ", indexData[i]));
                }
                Debug.Write("\n");

                foreach (ModelMeshPart mmp in mm.MeshParts)
                {
                    Debug.WriteLine(string.Format("Mesh Part: Verts {0} - {1}, Indices {2} - {3}, ", mmp.BaseVertex, mmp.BaseVertex + mmp.NumVertices, mmp.StartIndex, mmp.StartIndex + mmp.PrimitiveCount * 3));
                }
            }
            Debug.WriteLine("------------------------------------------------");
        }
    }
}
