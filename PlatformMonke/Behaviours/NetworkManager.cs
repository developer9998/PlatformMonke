using ExitGames.Client.Photon;
using Photon.Pun;
using PlatformMonke.Models;
using PlatformMonke.Tools;
using System;
using System.Linq;
using UnityEngine;

namespace PlatformMonke.Behaviours
{
    internal class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        private readonly int createLabel = StaticHash.Compute("PlatformMonke".GetStaticHash(), "CreatePlatform".GetStaticHash());
        private readonly int destroyLabel = StaticHash.Compute("PlatformMonke".GetStaticHash(), "DestroyPlatform".GetStaticHash());

        public void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
        }

        public void CreatePlatform(Platform platform)
        {
            try
            {
                if (!platform.IsLocal) return;

                object[] parameters =
                [
                    platform.IsLeftHand,
                    platform.Position,
                    platform.EulerAngles,
                    (byte)platform.Size.GetIndex(),
                    (byte)platform.Colour.GetIndex()
                ];
                RaiseEvent(createLabel, parameters);
            }
            catch (Exception)
            {

            }
        }

        public void DestroyPlatform(Platform platform)
        {
            try
            {
                if (!platform.IsLocal) return;

                object[] parameters = [platform.IsLeftHand];
                RaiseEvent(destroyLabel, parameters);
            }
            catch (Exception)
            {

            }
        }

        public void RaiseEvent(int labelHash, object[] providedData)
        {
            object[] finalData = new object[providedData.Length + 1];
            finalData[0] = labelHash;
            Array.Copy(providedData, 0, finalData, 1, providedData.Length);

            NetEventOptions eventOptions = NetworkSystemRaiseEvent.neoOthers;
            NetworkSystemRaiseEvent.RaiseEvent(176, finalData, eventOptions, true);
        }

        public void OnEvent(EventData eventData)
        {
            try
            {
                if (eventData.Code != 176) return;

                object[] data = (object[])eventData.CustomData;
                if (data.Length == 0 || data.ElementAtOrDefault(0) is not int labelHash || (labelHash != createLabel && labelHash != destroyLabel)) return;

                NetPlayer player = NetworkSystem.Instance.GetPlayer(eventData.Sender);
                Logging.Message($"OnEvent from {player.NickName}:\n{string.Join("\n", data)}");

                bool isLeftHand = (bool)data.ElementAtOrDefault(1);

                if (labelHash == createLabel)
                {
                    Vector3 position = (Vector3)data.ElementAtOrDefault(2);
                    Vector3 eulerAngles = (Vector3)data.ElementAtOrDefault(3);

                    EnumData<PlatformSize> sizeData = EnumData<PlatformSize>.Shared;
                    if (data.ElementAtOrDefault(4) is not byte sizeIndex || !sizeData.IndexToEnum.TryGetValue(sizeIndex, out PlatformSize size)) size = PlatformSize.Default;

                    EnumData<PlatformColour> colourData = EnumData<PlatformColour>.Shared;
                    if (data.ElementAtOrDefault(5) is not byte colourIndex || !colourData.IndexToEnum.TryGetValue(colourIndex, out PlatformColour colour)) colour = PlatformColour.Black;

                    PlatformManager.Instance.CreatePlatform(isLeftHand, position, eulerAngles, size, colour, player);

                    return;
                }

                PlatformManager.Instance.DestroyPlatform(isLeftHand, player);
            }
            catch (Exception)
            {

            }
        }
    }
}
