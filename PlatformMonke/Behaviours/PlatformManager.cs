using BepInEx.Configuration;
using PlatformMonke.Models;
using PlatformMonke.Tools;
using PlatformMonke.Utilities;
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

            localPlayer = NetworkSystem.Instance.GetLocalPlayer();

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
            if (Plugin.Instance.InModdedRoom && Plugin.Instance.enabled)
            {
                wasInModdedRoom = true;

                if (!hasLeftPlatform && ControllerInputPoller.instance.leftGrab)
                {
                    hasLeftPlatform = true;
                    CreateLocalPlatform(true);
                }
                else if (hasLeftPlatform && ControllerInputPoller.instance.leftGrabRelease)
                {
                    hasLeftPlatform = false;

                    if (Configuration.RemoveReleasedPlatforms.Value) DestroyLocalPlatform(true);
                    else if (platforms.ContainsKey(LocalPlayer) && platforms[LocalPlayer].TryGetValue(true, out var leftController) && leftController.IsStickyPlatform) leftController.ClearStickyFactor();

                    if (Configuration.StickyPlatforms.Value) Player.Instance.playerRigidBody.linearVelocity = Player.Instance.bodyVelocityTracker.GetAverageVelocity(true);
                }

                if (!hasRightPlatform && ControllerInputPoller.instance.rightGrab)
                {
                    hasRightPlatform = true;
                    CreateLocalPlatform(false);
                }
                else if (hasRightPlatform && ControllerInputPoller.instance.rightGrabRelease)
                {
                    hasRightPlatform = false;

                    if (Configuration.RemoveReleasedPlatforms.Value) DestroyLocalPlatform(false);
                    else if (platforms.ContainsKey(LocalPlayer) && platforms[LocalPlayer].TryGetValue(false, out var rightController) && rightController.IsStickyPlatform) rightController.ClearStickyFactor();

                    if (Configuration.StickyPlatforms.Value) Player.Instance.playerRigidBody.linearVelocity = Player.Instance.bodyVelocityTracker.GetAverageVelocity(true, 0.15f, false);
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

            Vector3 handOffset = Vector3.down * (Player.Instance.minimumRaycastDistance * 1.75f);
            Vector3 totalVelocity = Configuration.StickyPlatforms.Value ? Vector3.zero : estimator.linearVelocity + VRRig.LocalRig.LatestVelocity();
            Vector3 finalPosition = handPosition + handOffset + (totalVelocity * Time.deltaTime);

            CreatePlatform(isLeftHand, finalPosition, handEulerAngles, (isLeftHand ? Configuration.LeftPlatformSize : Configuration.RightPlatformSize).Value, (isLeftHand ? Configuration.LeftPlatformColour : Configuration.RightPlatformColour).Value, LocalPlayer);
        }

        public void UpdatePlatform(bool isLeftHand)
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
        }

        public void CreatePlatform(bool isLeftHand, Vector3 position, Vector3 eulerAngles, PlatformSize size, PlatformColour colour, NetPlayer owner)
        {
            try
            {
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
                    oppositeController.ClearStickyFactor();
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
            catch
            {

            }
        }

        public void DestroyPlatform(bool isLeftHand, NetPlayer owner)
        {
            try
            {
                if (!platforms.TryGetValue(owner, out Dictionary<bool, PlatformController> collection)) return;

                if (collection.TryGetValue(isLeftHand, out PlatformController controller))
                {
                    controller.Destroy();
                    collection.Remove(isLeftHand);
                }
            }
            catch
            {

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
            if (!whitelistedPlayers.Contains(player))
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
                
                for(int i = 0; i < whitelistedPlayers.Count; i++)
                {
                    if (!playersToWhitelist.Contains(whitelistedPlayers[i]))
                    {
                        whitelistedPlayers.Remove(whitelistedPlayers[i]);
                        i--;
                    }
                }

                foreach(NetPlayer player in playersToWhitelist)
                {
                    if (!whitelistedPlayers.Contains(player))
                        whitelistedPlayers.Add(player);
                }

                if (whitelistedPlayerCache != null && !whitelistedPlayerCache.SequenceEqual(whitelistedPlayers))
                    whitelistedPlayerCache = null;

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
