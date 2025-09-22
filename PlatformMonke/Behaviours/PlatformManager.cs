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

        private readonly Dictionary<NetPlayer, PlatformController> leftPlatforms = [], rightPlatforms = [];

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
                    else if (leftPlatforms.TryGetValue(LocalPlayer, out PlatformController leftController) && leftController.IsStickyPlatform) leftController.EndStickyEffect();

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
                    else if (rightPlatforms.TryGetValue(LocalPlayer, out PlatformController rightController) && rightController.IsStickyPlatform) rightController.EndStickyEffect();

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

                foreach (var owner in leftPlatforms.Keys.ToArray())
                {
                    DestroyPlatform(true, owner);
                }

                foreach (var owner in rightPlatforms.Keys.ToArray())
                {
                    DestroyPlatform(false, owner);
                }

                leftPlatforms.Clear();
                rightPlatforms.Clear();
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
            var dictionary = isLeftHand ? leftPlatforms : rightPlatforms;
            if (dictionary.TryGetValue(LocalPlayer, out PlatformController controller))
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

                var dictionary = isLeftHand ? leftPlatforms : rightPlatforms;
                var oppositeDictionary = isLeftHand ? rightPlatforms : leftPlatforms;

                if (dictionary.TryGetValue(owner, out PlatformController controller))
                {
                    controller.Destroy();
                }

                if (oppositeDictionary.TryGetValue(owner, out PlatformController oppositeController) && oppositeController.IsStickyPlatform)
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

                if (dictionary.ContainsKey(owner)) dictionary[owner] = controller;
                else dictionary.Add(owner, controller);
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

                var dictionary = isLeftHand ? leftPlatforms : rightPlatforms;

                if (dictionary.TryGetValue(owner, out PlatformController controller))
                {
                    controller.Destroy();
                    dictionary.Remove(owner);
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
            if (leftPlatforms.ContainsKey(player))
            {
                DestroyPlatform(true, player);
                leftPlatforms.Remove(player);
            }

            if (rightPlatforms.ContainsKey(player))
            {
                DestroyPlatform(false, player);
                rightPlatforms.Remove(player);
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

                foreach (var (player, controller) in leftPlatforms)
                {
                    if (player.IsLocal) continue;
                    controller.EvaluatePlatformCollision(playersToWhitelist.Contains(player));
                }

                foreach (var (player, controller) in rightPlatforms)
                {
                    if (player.IsLocal) continue;
                    controller.EvaluatePlatformCollision(playersToWhitelist.Contains(player));
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
