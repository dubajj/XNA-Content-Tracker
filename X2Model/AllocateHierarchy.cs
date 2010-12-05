using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlimDX.Direct3D9;

namespace X2Model
{
    public class CustomFrame : Frame
    {
        public CustomFrame(string name)
        {
            base.Name = name;
        }
    }

    public class CustomMeshContainer : MeshContainer
    {
    }

    public class AllocateHierarchy : IAllocateHierarchy
    {

        #region IAllocateHierarchy Members

        public Frame CreateFrame(string name)
        {
            return new CustomFrame(name);
        }

        public MeshContainer CreateMeshContainer(string name, MeshData meshData, ExtendedMaterial[] materials, EffectInstance[] effectInstances, int[] adjacency, SkinInfo skinInfo)
        {
            CustomMeshContainer mc = new CustomMeshContainer();
            mc.Name = name;
            mc.MeshData = meshData;
            mc.SetMaterials(materials);
            mc.SetEffects(effectInstances);
            mc.SetAdjacency(adjacency);
            mc.SkinInfo = skinInfo;

            return mc;
        }

        public void DestroyFrame(Frame frame)
        {
            
        }

        public void DestroyMeshContainer(MeshContainer container)
        {
            
        }

        #endregion
    }
}
