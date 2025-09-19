using BepInEx.Configuration;
using GorillaExtensions;
using PlatformMonke.Models;
using PlatformMonke.Tools;
using PlatformMonke.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using Player = GorillaLocomotion.GTPlayer;

namespace PlatformMonke.Behaviours
{
    internal class PlatformManager : MonoBehaviour
    {
        public static PlatformManager Instance { get; private set; }

        public NetPlayer LocalPlayer
        {
            get
            {
                if (localPlayer == null && NetworkSystem.Instance is NetworkSystem netSys)
                    localPlayer = netSys.GetLocalPlayer();
                return localPlayer;
            }
        }

        public ReadOnlyCollection<NetPlayer> WhitelistedPlayers
        {
            get
            {
                if (whitelistedPlayerCache == null && whitelistedPlayers != null) whitelistedPlayerCache = whitelistedPlayers.AsReadOnly();
                return whitelistedPlayerCache;
            }
        }

        private NetPlayer localPlayer;

        private ReadOnlyCollection<NetPlayer> whitelistedPlayerCache;

        private bool wasInModdedRoom, hasLeftPlatform, hasRightPlatform;

        private GorillaVelocityEstimator leftHandEstimator, rightHandEstimator;

        private readonly Dictionary<NetPlayer, Dictionary<bool, PlatformController>> platforms = [];

        private readonly List<NetPlayer> whitelistedPlayers = [];

        public void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            localPlayer = NetworkSystem.Instance?.GetLocalPlayer();

            leftHandEstimator = Player.Instance.leftControllerTransform.gameObject.AddComponent<GorillaVelocityEstimator>();
            leftHandEstimator.useGlobalSpace = true;

            rightHandEstimator = Player.Instance.rightControllerTransform.gameObject.AddComponent<GorillaVelocityEstimator>();
            rightHandEstimator.useGlobalSpace = true;

            NetworkSystem.Instance.OnMultiplayerStarted += OnJoinedRoom;
            NetworkSystem.Instance.OnReturnedToSinglePlayer += OnLeftRoom;
            NetworkSystem.Instance.OnPlayerJoined += OnPlayerJoined;
            NetworkSystem.Instance.OnPlayerLeft += OnPlayerLeft;

            Plugin.Instance.Config.SettingChanged += OnSettingChanged;

            MaterialOverrideUtility.ClassifyOverrides(GorillaTagger.Instance.offlineVRRig.materialsToChangeTo);
        }

        public void Update()
        {
            if (Plugin.Instance.InModdedRoom)
            {
                wasInModdedRoom = true;

                if (!hasLeftPlatform && ControllerInputPoller.instance.leftGrab && Plugin.Instance.enabled)
                {
                    hasLeftPlatform = true;
                    CreateLocalPlatform(true);
                }
                else if (hasLeftPlatform && ControllerInputPoller.instance.leftGrabRelease)
                {
                    hasLeftPlatform = false;

                    if (Configuration.RemoveReleasedPlatforms.Value) DestroyLocalPlatform(true);
                    else if (platforms.ContainsKey(LocalPlayer) && platforms[LocalPlayer].TryGetValue(true, out PlatformController leftController) && leftController.IsStickyPlatform) leftController.EndStickyEffect();

                    if (Configuration.StickyPlatforms.Value) Player.Instance.playerRigidBody.linearVelocity = Player.Instance.bodyVelocityTracker.GetAverageVelocity(true);
                }

                if (!hasRightPlatform && ControllerInputPoller.instance.rightGrab && Plugin.Instance.enabled)
                {
                    hasRightPlatform = true;
                    CreateLocalPlatform(false);
                }
                else if (hasRightPlatform && ControllerInputPoller.instance.rightGrabRelease)
                {
                    hasRightPlatform = false;

                    if (Configuration.RemoveReleasedPlatforms.Value) DestroyLocalPlatform(false);
                    else if (platforms.ContainsKey(LocalPlayer) && platforms[LocalPlayer].TryGetValue(false, out PlatformController rightController) && rightController.IsStickyPlatform) rightController.EndStickyEffect();

                    if (Configuration.StickyPlatforms.Value) Player.Instance.playerRigidBody.linearVelocity = Player.Instance.bodyVelocityTracker.GetAverageVelocity(true);
                }

                if ((hasLeftPlatform || hasRightPlatform) && !Plugin.Instance.enabled)
                {
                    hasLeftPlatform = false;
                    hasRightPlatform = false;

                    DestroyLocalPlatform(true);
                    DestroyLocalPlatform(false);
                }

                return;
            }

            if (wasInModdedRoom)
            {
                wasInModdedRoom = false;
                hasLeftPlatform = false;
                hasRightPlatform = false;

                foreach (var (owner, collection) in platforms)
                {
                    foreach (var isLeftHand in collection.Keys)
                    {
                        DestroyPlatform(isLeftHand, owner);
                    }
                }

                platforms.Clear();
            }
        }

        private void CreateLocalPlatform(bool isLeftHand)
        {
            Transform hand = isLeftHand ? Player.Instance.leftHandFollower : Player.Instance.rightHandFollower;
            Vector3 handPosition = hand.position;
            Vector3 handEulerAngles = hand.eulerAngles;
            GorillaVelocityEstimator estimator = isLeftHand ? leftHandEstimator : rightHandEstimator;

            float distance = Player.Instance.minimumRaycastDistance * 1.1f;
            VRRig localRig = VRRig.LocalRig;
            Vector3 displacement = (isLeftHand ? -localRig.leftHandTransform.parent.right : localRig.rightHandTransform.parent.right) * distance;
            Vector3 rigVelocity = localRig.LatestVelocity();
            Vector3 totalVelocity = Configuration.StickyPlatforms.Value ? Vector3.zero : estimator.linearVelocity + (Vector3.up * (rigVelocity.y < 0f ? rigVelocity.y : 0f)) + (rigVelocity.WithY(0f) * 2.5f);
            Vector3 finalPosition = handPosition + displacement + (totalVelocity * Time.smoothDeltaTime);

            CreatePlatform(isLeftHand, finalPosition, handEulerAngles, (isLeftHand ? Configuration.LeftPlatformSize : Configuration.RightPlatformSize).Value, (isLeftHand ? Configuration.LeftPlatformColour : Configuration.RightPlatformColour).Value, LocalPlayer);

            bool hasPlatform = isLeftHand ? ref hasLeftPlatform : ref hasRightPlatform;
            hasPlatform = true;
        }

        private void UpdatePlatform(bool isLeftHand)
        {
            if (platforms.TryGetValue(LocalPlayer, out var collection) && collection.TryGetValue(isLeftHand, out PlatformController controller))
            {
                Platform platform = controller.Platform;
                CreatePlatform(isLeftHand, platform.Position, platform.EulerAngles, (isLeftHand ? Configuration.LeftPlatformSize : Configuration.RightPlatformSize).Value, (isLeftHand ? Configuration.LeftPlatformColour : Configuration.RightPlatformColour).Value, LocalPlayer);
            }
        }

        private void DestroyLocalPlatform(bool isLeftHand)
        {
            DestroyPlatform(isLeftHand, LocalPlayer);

            bool hasPlatform = isLeftHand ? ref hasLeftPlatform : ref hasRightPlatform;
            hasPlatform = false;
        }

        public void CreatePlatform(bool isLeftHand, Vector3 position, Vector3 eulerAngles, PlatformSize size, PlatformColour colour, NetPlayer owner)
        {
            try
            {
                if (owner == null || owner.IsNull) throw new ArgumentNullException(nameof(owner));

                if (!platforms.TryGetValue(owner, out Dictionary<bool, PlatformController> collection))
                {
                    collection = [];
                    platforms.Add(owner, collection);
                }

                if (collection.TryGetValue(isLeftHand, out PlatformController controller))
                {
                    controller.Destroy();
                    collection.Remove(isLeftHand);
                }

                if (collection.TryGetValue(!isLeftHand, out PlatformController oppositeController) && oppositeController.IsStickyPlatform)
                {
                    oppositeController.EndStickyEffect();
                }

                controller = new(new Platform()
                {
                    IsLeftHand = isLeftHand,
                    Position = position,
                    EulerAngles = eulerAngles,
                    Size = size,
                    Colour = colour,
                    Owner = owner
                });

                collection.Add(isLeftHand, controller);
            }
            catch (Exception ex)
            {
                Logging.Fatal($"Platform creation failure for {((owner == null || owner.IsNull) ? "null player" : owner.NickName)}");
                Logging.Error(ex);
            }
        }

        public void DestroyPlatform(bool isLeftHand, NetPlayer owner)
        {
            try
            {
                if (owner == null || owner.IsNull) throw new ArgumentNullException(nameof(owner));

                if (!platforms.TryGetValue(owner, out Dictionary<bool, PlatformController> collection)) return;

                if (collection.TryGetValue(isLeftHand, out PlatformController controller))
                {
                    controller.Destroy();
                    collection.Remove(isLeftHand);
                }
            }
            catch (Exception ex)
            {
                Logging.Fatal($"Platform deletion failure for {((owner == null || owner.IsNull) ? "null player" : owner.NickName)}");
                Logging.Error(ex);
            }
        }

        private void OnJoinedRoom()
        {
            string[] array = Configuration.WhitelistedPlayers.Value;
            IEnumerable<NetPlayer> playersToWhitelist = NetworkSystem.Instance.PlayerListOthers.Where(player => array.Contains(player.UserId));

            whitelistedPlayers.AddRange(playersToWhitelist);
            whitelistedPlayerCache = null;
        }

        private void OnLeftRoom()
        {
            whitelistedPlayers.Clear();
            whitelistedPlayerCache = null;
        }

        private void OnPlayerJoined(NetPlayer player)
        {
            if (player.IsLocal) return;

            if (Configuration.WhitelistedPlayers.Value.Contains(player.UserId) && !whitelistedPlayers.Contains(player))
            {
                whitelistedPlayers.Add(player);
                whitelistedPlayerCache = null;
            }
        }

        private void OnPlayerLeft(NetPlayer player)
        {
            if (platforms.TryGetValue(player, out var collection))
            {
                foreach (var isLeftHand in collection.Keys)
                {
                    DestroyPlatform(isLeftHand, player);
                }

                platforms.Remove(player);
            }

            if (whitelistedPlayers.Remove(player))
                whitelistedPlayerCache = null;
        }

        private void OnSettingChanged(object sender, SettingChangedEventArgs args)
        {
            ConfigEntryBase entry = args.ChangedSetting;

            if (entry == Configuration.WhitelistedPlayers)
            {
                string[] array = Configuration.WhitelistedPlayers.Value;
                IEnumerable<NetPlayer> playersToWhitelist = NetworkSystem.Instance.PlayerListOthers.Where(player => array.Contains(player.UserId));

                for (int i = 0; i < whitelistedPlayers.Count; i++)
                {
                    if (!playersToWhitelist.Contains(whitelistedPlayers[i]))
                    {
                        whitelistedPlayers.RemoveAt(i);
                        i--;
                    }
                }

                playersToWhitelist.Where(player => !whitelistedPlayers.Contains(player)).ForEach(player => whitelistedPlayers.Add(player));

                if (whitelistedPlayerCache != null && !whitelistedPlayerCache.SequenceEqual(whitelistedPlayers))
                    whitelistedPlayerCache = null;

                foreach (var (player, collection) in platforms)
                {
                    if (player.IsLocal) continue;
                    bool isCollisionAllowed = playersToWhitelist.Contains(player);
                    collection.Values.ForEach(controller => controller.EvaluatePlatformCollision(isCollisionAllowed));
                }

                return;
            }

            if (entry != Configuration.RemoveReleasedPlatforms && entry != Configuration.StickyPlatforms)
            {
                UpdatePlatform(true);
                UpdatePlatform(false);
            }
        }
    }
}
