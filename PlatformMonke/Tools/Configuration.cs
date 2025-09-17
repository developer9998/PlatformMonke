using BepInEx.Configuration;
using PlatformMonke.Models;

namespace PlatformMonke.Tools
{
    internal static class Configuration
    {
        public static ConfigEntry<PlatformSize> LeftPlatformSize, RightPlatformSize;

        public static ConfigEntry<PlatformColour> LeftPlatformColour, RightPlatformColour;

        public static ConfigEntry<bool> RemoveReleasedPlatforms, StickyPlatforms;

        public static ConfigEntry<string[]> WhitelistedPlayers;
    }
}
