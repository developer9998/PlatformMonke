using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Newtonsoft.Json;
using PlatformMonke.Behaviours;
using PlatformMonke.Models;
using PlatformMonke.Tools;
using System;
using UnityEngine;
using Utilla.Attributes;

namespace PlatformMonke
{
    [BepInDependency("dev.gorillainfowatch")]
    [BepInDependency("org.legoandmars.gorillatag.utilla"), ModdedGamemode]
    [BepInPlugin(Constants.GUID, Constants.Name, Constants.Version)]
    internal class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }

        public new ManualLogSource Logger;

        public bool InModdedRoom;

        public void Awake()
        {
            Instance ??= this;
            Logger = base.Logger;

            TypeConverter typeConverter = new();
            typeConverter.ConvertToString = (value, type) => JsonConvert.SerializeObject(value, type, null);
            typeConverter.ConvertToObject = (value, type) => JsonConvert.DeserializeObject(value, type, (JsonSerializerSettings)null);
            TomlTypeConverter.AddConverter(typeof(string[]), typeConverter);

            Configuration.LeftPlatformSize = Config.Bind("Collision", "Left Platform Size", PlatformSize.Default, "The size of the left platform");
            Configuration.RightPlatformSize = Config.Bind("Collision", "Right Platform Size", PlatformSize.Default, "The size of the right platform");

            Configuration.LeftPlatformColour = Config.Bind("Appearance", "Left Platform Colour", PlatformColour.Black, "The colour of the left platform");
            Configuration.RightPlatformColour = Config.Bind("Appearance", "Right Platform Colour", PlatformColour.Black, "The colour of the right platform");

            Configuration.RemoveReleasedPlatforms = Config.Bind("Behaviour", "Remove Platforms on Release", true, "Whether platforms are removed when releasing your grip");
            Configuration.StickyPlatforms = Config.Bind("Behaviour", "Stick Platforms to Hand", false, "Whether platforms stick to your hand when created, known as 'sticky platforms'");

            Configuration.WhitelistedPlayers = Config.Bind("Interaction", "Whitelisted Players", Array.Empty<string>(), "The array of players (in the form of ID) you can interact/collide with");

            GorillaTagger.OnPlayerSpawned(Initialize);
        }

        public void Initialize()
        {
            try
            {
                GameObject gameObject = new(Constants.Name);
                gameObject.AddComponent<NetworkManager>();
                gameObject.AddComponent<PlatformManager>();
            }
            catch (Exception)
            {

            }
        }

        public void OnEnable()
        {

        }

        public void OnDisable()
        {

        }

        [ModdedGamemodeJoin]
        public void OnJoin()
        {
            InModdedRoom = true;
        }

        [ModdedGamemodeLeave]
        public void OnLeave()
        {
            InModdedRoom = false;
        }
    }
}
