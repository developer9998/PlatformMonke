using UnityEngine;

namespace PlatformMonke.Models
{
    internal struct Platform
    {
        public readonly bool IsLocal => Owner != null && !Owner.IsNull && Owner.IsLocal;

        public NetPlayer Owner;

        public Vector3 Position, EulerAngles;

        public PlatformSize Size;

        public PlatformColour Colour;

        public bool IsLeftHand;

        public GameObject Object;
    }
}
