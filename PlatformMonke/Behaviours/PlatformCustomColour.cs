using UnityEngine;

namespace PlatformMonke.Behaviours
{
    [RequireComponent(typeof(MeshRenderer))]
    internal class PlatformCustomColour : MonoBehaviour
    {
        public VRRig Rig;

        private Material material;

        public void Start()
        {
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            material = new Material(meshRenderer.material);
            meshRenderer.material = material;

            Rig.OnColorChanged += OnColourChanged;
            OnColourChanged(Rig.playerColor);
        }

        public void OnDestroy()
        {
            Rig.OnColorChanged -= OnColourChanged;
        }

        public void OnColourChanged(Color colour)
        {
            material.color = colour;
        }
    }
}