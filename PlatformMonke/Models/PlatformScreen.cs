using BepInEx.Configuration;
using GorillaInfoWatch.Models;
using GorillaInfoWatch.Models.Attributes;
using GorillaInfoWatch.Models.Enumerations;
using GorillaInfoWatch.Models.Widgets;
using PlatformMonke.Tools;
using PlatformMonke.Utilities;
using System;
using UnityEngine;

[assembly: InfoWatchCompatible]

namespace PlatformMonke.Models
{
    [ShowOnHomeScreen(DisplayTitle = "<b><i><color=#FFE65C>Platform Monke</color></i></b>")]
    internal class PlatformScreen : InfoScreen
    {
        public override string Title => "<color=#FFE65C>Platform Monke</color>";
        public override string Description => "Mod for Quest by <color=green>Waulta</color>, an unofficial port of AirJump by <color=blue>fchb1239</color>";

        public override InfoContent GetContent()
        {
            LineBuilder lines = new();

            lines.Skip().Add(Plugin.Instance.enabled ? "<color=green>Enabled</color>" : "<color=red>Disabled</color>", new Widget_Switch(Plugin.Instance.enabled, value =>
            {
                Plugin.Instance.enabled = value;
                SetText();
            })).Skip();

            DrawEnumEntry(lines, Configuration.LeftPlatformSize);
            DrawEnumEntry(lines, Configuration.RightPlatformSize);
            DrawEnumEntry(lines, Configuration.LeftPlatformColour);
            DrawEnumEntry(lines, Configuration.RightPlatformColour);
            DrawBoolEntry(lines, Configuration.RemoveReleasedPlatforms);
            DrawBoolEntry(lines, Configuration.StickyPlatforms);

            return lines;
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
    }
}