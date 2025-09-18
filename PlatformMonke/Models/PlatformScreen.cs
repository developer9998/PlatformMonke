using BepInEx.Configuration;
using GorillaInfoWatch.Extensions;
using GorillaInfoWatch.Models;
using GorillaInfoWatch.Models.Attributes;
using GorillaInfoWatch.Models.Enumerations;
using GorillaInfoWatch.Models.Widgets;
using PlatformMonke.Behaviours;
using PlatformMonke.Tools;
using PlatformMonke.Utilities;
using System;
using System.Linq;
using UnityEngine;

[assembly: InfoWatchCompatible]

namespace PlatformMonke.Models
{
    [ShowOnHomeScreen(DisplayTitle = "<b><i><color=#FFE65C>Platform Monke</color></i></b>")]
    internal class PlatformScreen : InfoScreen
    {
        public override string Title => "<color=#FFE65C>Platform Monke</color>";
        public override string Description => "Mod for Quest by <color=green>Waulta</color>, an unofficial port of AirJump by <color=blue>fchb1239</color>";

        public override void OnScreenLoad()
        {
            NetworkSystem.Instance.OnMultiplayerStarted += SetContent;
            NetworkSystem.Instance.OnReturnedToSinglePlayer += SetContent;
            NetworkSystem.Instance.OnPlayerJoined += OnPlayerActivity;
            NetworkSystem.Instance.OnPlayerLeft += OnPlayerActivity;
        }

        public override void OnScreenUnload()
        {
            NetworkSystem.Instance.OnMultiplayerStarted -= SetContent;
            NetworkSystem.Instance.OnReturnedToSinglePlayer -= SetContent;
            NetworkSystem.Instance.OnPlayerJoined -= OnPlayerActivity;
            NetworkSystem.Instance.OnPlayerLeft -= OnPlayerActivity;
        }

        public override InfoContent GetContent()
        {
            LineBuilder configLines = new();
            configLines.Skip();

            if (!Plugin.Instance.InModdedRoom)
            {
                configLines.BeginCentre().BeginColour("FF6D49").Append("PlatformMonke must be disabled at this time").EndColour().EndAlign().AppendLine().AppendLine();
                configLines.BeginCentre().Append("You must enter a modded room in order to use PlatformMonke.").EndAlign().AppendLine();

                return configLines;
            }

            configLines.Add(Plugin.Instance.enabled ? "<color=green>Enabled</color>" : "<color=red>Disabled</color>", new Widget_Switch(Plugin.Instance.enabled, value =>
            {
                Plugin.Instance.enabled = value;
                SetText();
            })).Skip();

            DrawEnumEntry(configLines, Configuration.LeftPlatformSize);
            DrawEnumEntry(configLines, Configuration.RightPlatformSize);
            DrawEnumEntry(configLines, Configuration.LeftPlatformColour);
            DrawEnumEntry(configLines, Configuration.RightPlatformColour);
            DrawBoolEntry(configLines, Configuration.RemoveReleasedPlatforms);
            DrawBoolEntry(configLines, Configuration.StickyPlatforms);

            PageBuilder pages = new();
            pages.AddPage(lines: configLines);

            if (NetworkSystem.Instance.RoomPlayerCount > 1)
            {
                LineBuilder interactionLines = new();

                interactionLines.Skip().AddRange("You can collide with platforms created by a player by selecting them".ToTextArray()).Add();

                NetPlayer[] playerArray = (NetPlayer[])NetworkSystem.Instance.PlayerListOthers.Clone();
                Array.Sort(playerArray, (x, y) => x.ActorNumber.CompareTo(y.ActorNumber));

                foreach(NetPlayer player in playerArray)
                {
                    if (player == null || player.IsNull) continue;

                    string sanitizedName = player.SanitizedNickName;
                    
                    interactionLines.Add((sanitizedName == null || sanitizedName.Length == 0) ? player.NickName : sanitizedName, new Widget_Switch(PlatformManager.Instance.WhitelistedPlayers.Contains(player), value =>
                    {
                        if (!player.IsLocal && !player.InRoom) return;

                        string playerId = player.UserId;
                        string[] array = Configuration.WhitelistedPlayers.Value;
                        int index = Array.IndexOf(array, playerId);

                        if (value && index == -1) Configuration.WhitelistedPlayers.Value = [.. array.Append(playerId)];
                        else if (!value && index != -1) Configuration.WhitelistedPlayers.Value = [.. array.Take(index).Concat(array.Skip(index + 1))];

                        SetContent();
                    }));
                }

                pages.AddPage(lines: interactionLines);
            }

            return pages;
        }

        public LineBuilder DrawEnumEntry<T>(LineBuilder lines, ConfigEntry<T> entry) where T : struct, Enum
        {
            EnumData<T> data = EnumData<T>.Shared;
            int maxIndex = data.Names.Length - 1;

            void ChangeEntryValue(object[] parameters)
            {
                int desiredIndex = entry.Value.GetIndex() + (int)parameters[0];
                int finalIndex = Mathf.Clamp(desiredIndex, 0, maxIndex);
                if (!data.IndexToEnum.TryGetValue(finalIndex, out T value)) value = data.Values[0];
                entry.Value = value;
                SetText();
            }

            lines.BeginColour("FFFF99").Append(entry.Definition.Key).Append(": ").EndColour().Add(new Widget_PushButton(ChangeEntryValue, -1)
            {
                //Colour = ColourPalette.Red,
                Symbol = new Symbol(Symbols.Minus)
                {
                    Colour = Color.black
                }
            }, new Widget_PushButton(ChangeEntryValue, 1)
            {
                //Colour = ColourPalette.Green,
                Symbol = new Symbol(Symbols.Plus)
                {
                    Colour = Color.black
                }
            });

            string displayValue;

            if (entry.SettingType == typeof(PlatformSize))
                displayValue = PlatformUtility.GetDisplayName((PlatformSize)entry.BoxedValue);
            else if (entry.SettingType == typeof(PlatformColour))
                displayValue = PlatformUtility.GetDisplayName((PlatformColour)entry.BoxedValue);
            else
                displayValue = entry.Value.GetName();

            lines.Append("<voffset=2em><size=70%>").AppendColour("<  ", "AADDAA").Append("</size>");
            lines.Append(displayValue);
            lines.Append("<size=70%>").AppendColour("  >", "AADDAA").Append("</size>").AppendLine();

            return lines;
        }

        public LineBuilder DrawBoolEntry(LineBuilder lines, ConfigEntry<bool> entry)
        {
            lines.BeginColour("FFFF99").Append(entry.Definition.Key).Append(": ").EndColour().Append(entry.Value ? "<color=green>Enabled</color>" : "<color=red>Disabled</color>").Add(new Widget_Switch(entry.Value, value =>
            {
                entry.Value = value;
                SetText();
            }));

            return lines;
        }

        public void OnPlayerActivity(NetPlayer _) => SetContent();
    }
}