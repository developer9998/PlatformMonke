using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PlatformMonke.Utilities
{
    internal static class MaterialOverrideUtility
    {
        private static readonly Dictionary<int, Material> overridenMaterials = [];

        public static void ClassifyOverrides(Material[] materialArray)
        {
            for(int i = 0; i < materialArray.Length; i++)
            {
                if (i == 0)
                {
                    Texture2D texture = new(80, 95, TextureFormat.RGBA32, false) 
                    {
                        filterMode = FilterMode.Point 
                    };

                    using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PlatformMonke.Content.lightfur.png");
                    byte[] bytes = new byte[stream.Length];
                    stream.Read(bytes, 0, bytes.Length);
                    stream.Close();
                    texture.LoadImage(bytes);

                    Material material = new(materialArray[4])
                    {
                        color = Color.white, // doesn't completely matter here as this is substituted when used
                        mainTexture = texture
                    };
                    material.EnableKeyword("_USE_TEXTURE");
                    material.DisableKeyword("_USE_TEX_ARRAY_ATLAS");

                    overridenMaterials.TryAdd(0, material);
                }
            }
        }

        public static Material GetMaterialOverride(int index) => overridenMaterials.TryGetValue(index, out Material material) ? material : null;
    }
}
