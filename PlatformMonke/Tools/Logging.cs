﻿using BepInEx.Logging;

namespace PlatformMonke.Tools
{
    internal static class Logging
    {
        public static void Message(object data) => Log(LogLevel.Message, data);

        public static void Info(object data) => Log(LogLevel.Info, data);

        public static void Warning(object data) => Log(LogLevel.Warning, data);

        public static void Error(object data) => Log(LogLevel.Error, data);

        public static void Fatal(object data) => Log(LogLevel.Fatal, data);

        public static void Log(LogLevel level, object data)
        {
#if DEBUG
            Plugin.Instance.Logger?.Log(level, data);
#endif
        }
    }
}
