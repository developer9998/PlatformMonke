using PlatformMonke.Behaviours;
using PlatformMonke.Models;
using UnityEngine;

namespace PlatformMonke.Utilities
{
    internal static class PlatformUtility
    {
        public static Platform CreateObject(Platform platform)
        {
            GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.name = $"Platform [{platform.Owner.NickName} {(platform.IsLeftHand ? "Left" : "Right")}]";

            Transform transform = gameObject.transform;
            transform.parent = PlatformManager.Instance.transform;
            transform.position = platform.Position;
            transform.eulerAngles = platform.EulerAngles;
            transform.localScale = GetUnityStructure(platform.Size);

            Material material = new(UberShader.GetShader())
            {
                color = GetUnityStructure(platform.Colour)
            };

            MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
            meshRenderer.material = material;

            if (VRRigCache.Instance.TryGetVrrig(platform.Owner, out RigContainer rigContainer))
            {
                VRRig rig = rigContainer.Rig;
                if (platform.Colour == PlatformColour.PlayerMaterial) gameObject.AddComponent<PlatformCustomMaterial>().Rig = rig;
                else if (platform.Colour == PlatformColour.PlayerColour) gameObject.AddComponent<PlatformCustomColour>().Rig = rig;
            }

            gameObject.AddComponent<GorillaSurfaceOverride>();

            platform.Object = gameObject;

            return platform;
        }

        public static string GetDisplayName(PlatformSize size) => size switch
        {
            PlatformSize.One or PlatformSize.Two or PlatformSize.Three or PlatformSize.Four or PlatformSize.Five => ((int)size).ToString(),
            PlatformSize.VerticalPlank => "Vertical Plank",
            PlatformSize.HorizontalPlank => "Horizontal Plank",
            PlatformSize.ChonkyBoi => "<color=#FFFF00>CHOMNKY BOI</color>",
            PlatformSize.HeftyChonk => "<color=#FD6E17>HEFTY CHONK</color>",
            PlatformSize.MegaChonker => "<color=#ff0000>MEGA CHONKER</color>",
            _ => size.GetName()
        };

        public static string GetDisplayName(PlatformColour colour) => colour switch
        {
            PlatformColour.PlayerColour => "Monke Colour",
            PlatformColour.PlayerMaterial => "Monke Material",
            _ => colour.GetName()
        };

        public static Vector3 GetUnityStructure(PlatformSize size) => size switch
        {
            PlatformSize.One => new Vector3(0.025f, 0.08f, 0.12f),
            PlatformSize.Two => new Vector3(0.025f, 0.15f, 0.25f),
            PlatformSize.Three => new Vector3(0.025f, 0.30f, 0.40f),
            PlatformSize.Four => new Vector3(0.025f, 0.45f, 0.55f),
            PlatformSize.Five => new Vector3(0.025f, 0.60f, 0.70f),
            PlatformSize.VerticalPlank => new Vector3(0.025f, 0.40f, 1.4f),
            PlatformSize.HorizontalPlank => new Vector3(0.025f, 1.4f, 0.40f),
            PlatformSize.ChonkyBoi => new Vector3(0.025f, 5f, 10f),
            PlatformSize.HeftyChonk => new Vector3(0.025f, 12.5f, 40f),
            PlatformSize.MegaChonker => new Vector3(0.025f, 20f, 70f),
            _ => new Vector3(0.025f, 0.25f, 0.32f)
        };

        public static Color GetUnityStructure(PlatformColour colour) => colour switch
        {
            PlatformColour.Grey => Color.grey,
            PlatformColour.White => Color.white,
            PlatformColour.Red => Color.red,
            PlatformColour.Green => Color.green,
            PlatformColour.Blue => Color.blue,
            PlatformColour.Yellow => Color.yellow,
            PlatformColour.Pink => new Color(0.9f, 0f, 1f),
            PlatformColour.Purple => new Color(0.5f, 0f, 1f),
            PlatformColour.Cyan => Color.cyan,
            _ => Color.black
        };
    }
}
