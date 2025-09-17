using PlatformMonke.Behaviours;
using PlatformMonke.Tools;
using PlatformMonke.Utilities;
using UnityEngine;
using Player = GorillaLocomotion.GTPlayer;

namespace PlatformMonke.Models
{
    internal class PlatformController
    {
        public Platform Platform { get; private set; }
        public bool IsStickyPlatform => isStickyPlatform;

        private bool isStickyPlatform;

        private readonly Transform temporaryHeldTransform;

        private readonly GorillaGrabber temporaryGrabber;

        public PlatformController(Platform platform)
        {
            Platform = PlatformUtility.CreateObject(platform);

            if (Platform.IsLocal)
            {
                NetworkManager.Instance.CreatePlatform(platform);

                if (Configuration.StickyPlatforms.Value)
                {
                    isStickyPlatform = true;

                    GameObject temporaryHeldObject = new($"Grab Object [{(Platform.IsLeftHand ? "Left" : "Right")}]");
                    Transform hand = Platform.IsLeftHand ? Player.Instance.leftControllerTransform : Player.Instance.rightControllerTransform;
                    temporaryHeldTransform = temporaryHeldObject.transform;
                    temporaryHeldTransform.parent = Platform.Object.transform.parent;
                    temporaryHeldTransform.position = hand.position;
                    temporaryHeldTransform.eulerAngles = hand.eulerAngles;
                    temporaryGrabber = hand.GetComponentInChildren<GorillaGrabber>();

                    Player.Instance.AddHandHold(temporaryHeldTransform, Vector3.zero, temporaryGrabber, temporaryGrabber.IsRightHand, false, out _);
                }
            }
        }

        public void ClearStickyFactor()
        {
            if (isStickyPlatform)
            {
                isStickyPlatform = false;

                Player.Instance.RemoveHandHold(temporaryGrabber, temporaryGrabber.IsRightHand);
                Object.Destroy(temporaryHeldTransform.gameObject);
            }
        }

        public void Destroy()
        {
            Object.Destroy(Platform.Object);

            if (Platform.IsLocal)
            {
                NetworkManager.Instance.DestroyPlatform(Platform);
                ClearStickyFactor();
            }
        }
    }
}
