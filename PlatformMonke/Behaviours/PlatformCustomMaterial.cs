using System.Collections.Generic;
using UnityEngine;
using PlatformMonke.Utilities;

namespace PlatformMonke.Behaviours
{
    [RequireComponent(typeof(MeshRenderer))]
    internal class PlatformCustomMaterial : MonoBehaviour
    {
        public VRRig Rig;

        private MeshRenderer meshRenderer;

        private int materialIndex = -1;

        private Material[] materials;

        private readonly Dictionary<VRRig, Material[]> materialArrayCache = [];

        public void Start()
        {
            meshRenderer = GetComponent<MeshRenderer>();

            if (!materialArrayCache.TryGetValue(Rig, out materials))
            {
                materials = new Material[Rig.materialsToChangeTo.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = MaterialOverrideUtility.GetMaterialOverride(i) ?? Rig.materialsToChangeTo[i];
                    materials[i] = i == 0 ? new Material(material) : material;
                }
                materialArrayCache.TryAdd(Rig, materials);
            }

            materials[0].color = Rig.playerColor;
            Rig.OnColorChanged += OnColourChanged;

            materialIndex = -1;
            Update();
        }

        public void Update()
        {
            if (materialIndex != Rig.setMatIndex)
            {
                materialIndex = Rig.setMatIndex;
                meshRenderer.material = materials[materialIndex];
            }
        }

        public void OnDestroy()
        {
            Rig.OnColorChanged -= OnColourChanged;
        }

        public void OnColourChanged(Color colour)
        {
            materials[0].color = colour;
        }
    }
}
