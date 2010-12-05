using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SDX = SlimDX.Direct3D9;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System.Reflection;
using System.IO;

namespace X2Model
{
    public class ModelLoader
    {
        public static EffectPool CurrentEffectPool = null;
        
        static string currentModelPath;

        public static Model ModelFromFile(GraphicsDevice device, string fileName)
        {
            Model m = new Model();
            if (CurrentEffectPool == null)
                CurrentEffectPool = new EffectPool();

            currentModelPath = Path.GetDirectoryName(fileName);

            SDX.Device sdxDevice = ConvertDevice(device);

            SDX.AnimationController animController;
            AllocateHierarchy allocator = new AllocateHierarchy();
            SDX.Frame sdxFrame = SDX.Frame.LoadHierarchyFromX(sdxDevice, fileName, SDX.MeshFlags.SystemMemory, allocator, null, out animController);

            List<ModelBone> boneList = new List<ModelBone>();
            List<ModelMesh> meshList = new List<ModelMesh>();

            RecurseProcessFrame(null, sdxFrame, boneList, meshList, device);

            sdxDevice.Dispose();

            // Set the bones' parent and children
            foreach (ModelBone mb in boneList)
            {
                ModelBone[] childBones = boneList.FindAll((ModelBone b) => { return b.Parent == mb; }).ToArray();
                ReflectionHelper.CallMethod<ModelBone>(mb, "SetParentAndChildren", new object[] { mb.Parent, childBones });
            }

            //
            // Set the model bone & mesh collections and the root bone
            ReflectionHelper.SetField<Model>(m, "bones", ReflectionHelper.Construct<ModelBoneCollection>(
                                                            new Type[] { typeof(ModelBone[]) },
                                                            new object[] { boneList.ToArray() }));

            ReflectionHelper.SetField<Model>(m, "meshes", ReflectionHelper.Construct<ModelMeshCollection>(
                                                            new Type[] { typeof(ModelMesh[]) },
                                                            new object[] { meshList.ToArray() }));

            ReflectionHelper.SetField<Model>(m, "root", boneList[0]);

            return m;
        }

        // Converts the XNA Graphics device to a SlimDX Device
        static SDX.Device ConvertDevice(GraphicsDevice xnaDevice)
        {
            Pointer p = ReflectionHelper.GetField<GraphicsDevice, Pointer>(xnaDevice, "pComPtr");
            IntPtr pDevice;
            unsafe { pDevice = new IntPtr(Pointer.Unbox(p)); }
            return SDX.Device.FromPointer(pDevice);
        }

        static Vector3 ConvertColor4ToV3(SlimDX.Color4 sdxClr)
        {
            return new Vector3(sdxClr.Red, sdxClr.Green, sdxClr.Blue);
        }
        static Vector4 ConvertColor4ToV4(SlimDX.Color4 sdxClr)
        {
            return new Vector4(sdxClr.Red, sdxClr.Green, sdxClr.Blue, sdxClr.Alpha);
        }

        static Matrix ConvertMatrix(SlimDX.Matrix sdxMtx)
        {
            Matrix mtx = new Matrix();
            mtx.M11 = sdxMtx.M11;
            mtx.M12 = sdxMtx.M12;
            mtx.M13 = -sdxMtx.M13;
            mtx.M14 = sdxMtx.M14;
            mtx.M21 = sdxMtx.M21;
            mtx.M22 = sdxMtx.M22;
            mtx.M23 = -sdxMtx.M23;
            mtx.M24 = sdxMtx.M24;
            mtx.M31 = -sdxMtx.M31;
            mtx.M32 = -sdxMtx.M32;
            mtx.M33 = sdxMtx.M33;
            mtx.M34 = sdxMtx.M34;
            mtx.M41 = sdxMtx.M41;
            mtx.M42 = sdxMtx.M42;
            mtx.M43 = sdxMtx.M43;
            mtx.M44 = sdxMtx.M44;

            return mtx;
        }

        static void RecurseProcessFrame(ModelBone parentBone, SDX.Frame currentFrame, List<ModelBone> bones, List<ModelMesh> meshes, GraphicsDevice device)
        {
            Matrix mtxFrame = ConvertMatrix(currentFrame.TransformationMatrix);

            //internal ModelBone(string name, Matrix transform, int index);
            ModelBone frameBone = ReflectionHelper.Construct<ModelBone>( 
                                    new Type[] { typeof(string), typeof(Matrix), typeof(int) },
                                    new object[] { currentFrame.Name, mtxFrame, bones.Count });


            if (parentBone != null)
                ReflectionHelper.SetField<ModelBone>(frameBone, "parent", parentBone);

            bones.Add(frameBone);

            // Convert and add the mesh 
            if (currentFrame.MeshContainer != null &&
                currentFrame.MeshContainer.MeshData.Type == SDX.MeshDataType.Mesh)
            {
                // XNA Mesh data
                BoundingSphere bs;
                VertexBuffer vb;
                VertexDeclaration decl;
                IndexBuffer ib;
                ModelMeshPart[] meshParts;

                // SlimDX Mesh data
                SDX.ExtendedMaterial[] sdxMaterials;
                SDX.EffectInstance[] sdxEffects;
                //Int32[] attribs;
                SDX.VertexElement[] sdxElems;
                SlimDX.DataStream ds;
                SDX.Mesh sdxMesh = currentFrame.MeshContainer.MeshData.Mesh;
                sdxMesh.OptimizeInPlace(SDX.MeshOptimizeFlags.AttributeSort | SDX.MeshOptimizeFlags.Compact | SDX.MeshOptimizeFlags.DoNotSplit);

                // Create a bone for the mesh
                //internal ModelBone(string name, Matrix transform, int index);
                ModelBone meshBone = ReflectionHelper.Construct<ModelBone>(
                                        new Type[] { typeof(string), typeof(Matrix), typeof(int) },
                                        new object[] { currentFrame.MeshContainer.Name, Matrix.Identity, bones.Count });

                // Mesh bone is a child of the Frame bone
                ReflectionHelper.SetField<ModelBone>(meshBone, "parent", frameBone);
                bones.Add(meshBone);

                // Attribute buffer not needed.
                // SlimDX Attribute buffer (tells us which faces have which material)
                //ds = sdxMesh.LockAttributeBuffer(SDX.LockFlags.ReadOnly);
                //attribs = ds.ReadRange<Int32>(sdxMesh.FaceCount);
                //sdxMesh.UnlockAttributeBuffer();

                // Convert the Vertex Declaration
                sdxElems = sdxMesh.GetDeclaration();
                VertexElement[] xnaElems = new VertexElement[sdxElems.Length - 1];
                int posOffset = -1;
                int nmlOffset = -1;
                for (int i = 0; i < sdxElems.Length - 1; i++)
                {
                    xnaElems[i] = new VertexElement(
                                    sdxElems[i].Stream, 
                                    sdxElems[i].Offset, 
                                    (VertexElementFormat)(int)sdxElems[i].Type, 
                                    (VertexElementMethod)(int)sdxElems[i].Method, 
                                    (VertexElementUsage)(int)sdxElems[i].Usage, 
                                    sdxElems[i].UsageIndex);

                    if (sdxElems[i].Type == SDX.DeclarationType.Float3)
                    {
                        // Store stream offsets of position and normal (in float size)
                        if (sdxElems[i].Usage == SlimDX.Direct3D9.DeclarationUsage.Position)
                            posOffset = sdxElems[i].Offset / 4;
                        else if (sdxElems[i].Usage == SlimDX.Direct3D9.DeclarationUsage.Normal)
                            nmlOffset = sdxElems[i].Offset / 4;
                    }
                }
                decl = new VertexDeclaration(device, xnaElems);

                // Create Vertex Buffer
                int vbSizeInBytes = sdxMesh.VertexBuffer.Description.SizeInBytes;
                vb = new VertexBuffer(
                            device, vbSizeInBytes, 
                            (BufferUsage)(int)sdxMesh.VertexBuffer.Description.Usage);
                ds = sdxMesh.LockVertexBuffer(SDX.LockFlags.ReadOnly);

                // Copy vertex data. Perform mappings if easy...
                int vbStride = vbSizeInBytes / sdxMesh.VertexCount;
                if (vbStride % 4 != 0)
                {
                    // Copy full Vertex Buffer... not sure how to handle it otherwise!
                    vb.SetData<byte>(ds.ReadRange<byte>(vbSizeInBytes));
                }
                else
                {
                    float[] vbData = ds.ReadRange<float>(vbSizeInBytes / 4);
                    for (int i = 0; i < sdxMesh.VertexCount; i++)
                    {
                        int start = vbStride * i / 4;
                        
                        if (posOffset >= 0)
                            // Negate Position.Z
                            vbData[start + posOffset + 2] *= -1f;

                        if (nmlOffset >= 0)
                            // Negate Position.Z
                            vbData[start + nmlOffset + 2] *= -1f; 
                    }
                    vb.SetData<float>(vbData);
                }
                sdxMesh.UnlockVertexBuffer();
                sdxMesh.VertexBuffer.Dispose();

                // Get Index buffer info
                int ibSizeInBytes = sdxMesh.IndexBuffer.Description.SizeInBytes;
                bool ibFmt16 = sdxMesh.IndexBuffer.Description.Format == SDX.Format.Index16;

                // System mem index buffer data
                short[] ibData16 = null;
                int[] ibData32 = null;

                // Create Index Buffer
                ib = new IndexBuffer(
                            device, ibSizeInBytes, 
                            (BufferUsage)(int)sdxMesh.IndexBuffer.Description.Usage, 
                            ibFmt16 ? IndexElementSize.SixteenBits : IndexElementSize.ThirtyTwoBits);

                // Copy Index Buffer
                ds = sdxMesh.LockIndexBuffer(SDX.LockFlags.ReadOnly);
                if (ibFmt16)
                    ibData16 = ds.ReadRange<short>(ibSizeInBytes / 2);
                else
                    ibData32 = ds.ReadRange<int>(ibSizeInBytes / 4);

                sdxMesh.UnlockIndexBuffer();
                sdxMesh.IndexBuffer.Dispose();
                    
                // Calculate and converte the bounding sphere
                SlimDX.BoundingSphere sdxBS = SDX.Frame.CalculateBoundingSphere(currentFrame);
                bs = new BoundingSphere(new Vector3(sdxBS.Center.X, sdxBS.Center.Y, sdxBS.Center.Z), sdxBS.Radius);

                // SlimDX materials, effects & attributes
                sdxMaterials = currentFrame.MeshContainer.GetMaterials();
                sdxEffects = currentFrame.MeshContainer.GetEffects();
                SDX.AttributeRange[] sdxAttribRanges = sdxMesh.GetAttributeTable();

                // Create all the ModelMeshParts
                meshParts = new ModelMeshPart[sdxAttribRanges.Length];
                for (int i = 0; i < sdxAttribRanges.Length; i++)
                {
                    SDX.AttributeRange ar = sdxAttribRanges[i];

                    // Call constructor
                    //internal ModelMeshPart(int streamOffset, int baseVertex, int numVertices, int startIndex, int primitiveCount, VertexBuffer vertexBuffer, IndexBuffer indexBuffer, VertexDeclaration vertexDeclaration, object tag)
                    meshParts[i] = ReflectionHelper.Construct<ModelMeshPart>(
                                    new Type[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(VertexBuffer), typeof(IndexBuffer), typeof(VertexDeclaration), typeof(object) },
                                    new object[] { 0, ar.VertexStart, ar.VertexCount, ar.FaceStart * 3, ar.FaceCount, vb, ib, decl, null });

                    // Rework index buffer so each mesh part starts at zero... 
                    // not sure why I have to though :|
                    int startIdx = ar.FaceStart * 3;
                    int endIdx = startIdx + ar.FaceCount * 3;
                    if (ibFmt16)
                        for (int idx = startIdx; idx < endIdx; idx++)
                            ibData16[idx] -= (short)ar.VertexStart;
                    else
                        for (int idx = startIdx; idx < endIdx; idx++)
                            ibData32[idx] -= ar.VertexStart;

                    // Create a BasicEffect or Load an instance of the specified Effect
                    Effect fx = null;
                    if (string.IsNullOrEmpty(sdxEffects[i].EffectFileName))
                    {
                        BasicEffect bfx = new BasicEffect(device, CurrentEffectPool);
                        bfx.DiffuseColor = ConvertColor4ToV3(sdxMaterials[i].MaterialD3D.Diffuse);
                        bfx.EmissiveColor = ConvertColor4ToV3(sdxMaterials[i].MaterialD3D.Emissive);
                        bfx.AmbientLightColor = ConvertColor4ToV3(sdxMaterials[i].MaterialD3D.Ambient);
                        bfx.SpecularColor = ConvertColor4ToV3(sdxMaterials[i].MaterialD3D.Specular);
                        bfx.SpecularPower = sdxMaterials[i].MaterialD3D.Power;
                        
                        if (!string.IsNullOrEmpty(sdxMaterials[i].TextureFileName))
                        {
                            string texFilePath = Path.Combine(currentModelPath, sdxMaterials[i].TextureFileName);
                            if (File.Exists(texFilePath))
                            {
                                try
                                {
                                    bfx.Texture = Texture2D.FromFile(device, texFilePath);
                                    bfx.TextureEnabled = true;
                                }
                                catch { } // Todo: log errors
                            }
                        }

                        fx = bfx;
                    }
                    else
                    {
                        string fxFilePath = Path.Combine(currentModelPath, sdxEffects[i].EffectFileName);
                        if (File.Exists(fxFilePath))
                        {
                            CompiledEffect compiledFx = Effect.CompileEffectFromFile(fxFilePath, null, null, CompilerOptions.None, TargetPlatform.Windows);
                            fx = new Effect(device, compiledFx.GetEffectCode(), CompilerOptions.None, CurrentEffectPool);

                            foreach (SDX.EffectDefault d in sdxEffects[i].Defaults)
                            {
                                EffectParameter prm = fx.Parameters[d.ParameterName];

                                // Assume strings are texture names
                                if (d.Type == SDX.EffectDefaultType.String)
                                {
                                    string texName = new string(d.Value.ReadRange<char>((int)(d.Value.Length / sizeof(char))));
                                    string texFilePath = Path.Combine(currentModelPath, texName);
                                    if (File.Exists(texFilePath))
                                    {
                                        try
                                        {
                                            prm.SetValue(Texture2D.FromFile(device, texFilePath));
                                        }
                                        catch { } //todo log error
                                    }
                                }
                                else if (d.Type == SDX.EffectDefaultType.Floats)
                                {
                                    int numFloatsExpected = prm.ColumnCount * prm.RowCount;
                                    switch (numFloatsExpected)
                                    {
                                        case 1:
                                            prm.SetValue(d.Value.Read<float>());
                                            break;
                                        case 2:
                                            prm.SetValue(d.Value.Read<Vector2>());
                                            break;
                                        case 3:
                                            prm.SetValue(d.Value.Read<Vector3>());
                                            break;
                                        case 4:
                                            prm.SetValue(d.Value.Read<Vector4>());
                                            break;
                                        case 16:
                                            prm.SetValue(d.Value.Read<Matrix>());
                                            break;
                                        default:
                                            // Todo: log warning
                                            break;

                                    }
                                }
                                else // (d.Type == SDX.EffectDefaultType.Dword)
                                {
                                    prm.SetValue(d.Value.Read<Int32>());
                                }
                            }
                        }
                    }

                    // Apply created effect to the ModelMeshPart
                    ReflectionHelper.SetField<ModelMeshPart>(meshParts[i], "effect", fx);

                    sdxMesh.Dispose();
                }

                // Fill the index buffer
                if (ibFmt16)
                    ib.SetData<short>(ibData16);
                else
                    ib.SetData<int>(ibData32);

                // Call ModelMesh constructor
                // internal ModelMesh(string name, ModelBone parentBone, BoundingSphere boundingSphere, VertexBuffer vertexBuffer, IndexBuffer indexBuffer, ModelMeshPart[] meshParts, object tag);
                ModelMesh mm = ReflectionHelper.Construct<ModelMesh>(
                                new Type[] { typeof(string), typeof(ModelBone), typeof(BoundingSphere), typeof(VertexBuffer), typeof(IndexBuffer), typeof(ModelMeshPart[]), typeof(object) },
                                new object[] { currentFrame.MeshContainer.Name, frameBone, bs, vb, ib, meshParts, null });

                // Add all ModelMeshPart effects to the ModelMesh.Effects collection
                foreach (ModelMeshPart mmp in meshParts)
                {
                    if (mmp.Effect != null)
                        ReflectionHelper.CallMethod<ModelEffectCollection>(mm.Effects, "Add", new object[] { mmp.Effect });
                }

                // Store the created the mesh for this frame
                meshes.Add(mm);
            }

            // Recurse through child frames
            SDX.Frame frame = currentFrame.FirstChild;
            while (frame != null)
            {
                RecurseProcessFrame(parentBone, frame, bones, meshes, device);
                frame = frame.Sibling;
            }

            currentFrame.Dispose();
        }
    }
}
