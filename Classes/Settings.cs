﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using Newtonsoft.Json;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using ZlizEQMap.Forms;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace ZlizEQMap
{
    public enum SettingsLogsInLogsDir
    {
        Undefined = 0,
        LogsDir = 1,
        RootDir = 2
    }

    public class SettingLoadResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string SettingKey { get; set; }
        public string SettingLoadedValue { get; set; }
        public string SettingDefaultValue { get; set; }
    }

    // this sucks PHENOMENALLY
    // this version of dotnet doesn't support DI so, uh, I'll just leave it alone. TODO: unfuck
    public static class Settings
    {
        private static bool Loaded = false;

        public static string EQDirectoryPath1 { get; set; }
        public static string EQDirectoryPath2 { get; set; }

        public static string ZoneDataSet1 { get; set; }
        public static string ZoneDataSet2 { get; set; }

        public static SettingsLogsInLogsDir LogsInLogsDir1 { get; set; } = SettingsLogsInLogsDir.RootDir;
        public static SettingsLogsInLogsDir LogsInLogsDir2 { get; set; } = SettingsLogsInLogsDir.LogsDir;

        public static int ActiveProfileIndex { get; set; } = 1;
        public static bool CheckAutoSizeOnMapSwitch { get; set; } = true;
        public static bool CheckGroupByContinent { get; set; } = true;
        public static bool CheckEnableDirection { get; set; } = true;
        public static bool CheckEnableLegend { get; set; } = true;

        public static string LastSelectedZone { get; set; } = "ecommons";
        public static string WikiRootURL { get; set; } = "http://wiki.project1999.com/";
        public static int OpacityLevel { get; set; } = 100;
        public static bool MinimizeToTray { get; set; } = false;
        public static bool AlwaysOnTop { get; set; } = false;
        public static int LegendFontSize { get; set; } = 10;

        public static bool UseLegacyUI { get; set; } = false;
        public static bool PopoutMapAlwaysOnTop { get; set; } = false;
        public static int PopoutMapOpacityLevel { get; set; } = 100;
        public static int PopoutMapPositionX { get; set; } = 100;
        public static int PopoutMapPositionY { get; set; } = 100;
        public static int PopoutMapSizeWidth { get; set; } = 450;
        public static int PopoutMapSizeHeight { get; set; } = 350;

        public static int NotesShow { get; set; } = 1;
        public static Font NotesFont { get; set; } = new Font("Tahoma", 8.25F, FontStyle.Regular);
        public static Color NotesColor { get; set; } = Color.Green;
        public static bool NotesClearAfterEntry { get; set; } = false;
        public static bool NotesAutoUpdate { get; set; } = false;
        public static bool NotesAutoSave { get; set; } = false;

        public static bool LocHistoryShow { get; set; } = true;
        public static int LocHistoryNumberToTrack { get; set; } = 25;

        public static int AutoParseIntervalMS { get; set; } = 1000;

        public static string GetEQDirectoryPath()
        {
            if (ActiveProfileIndex == 1)
                return EQDirectoryPath1;
            else
                return EQDirectoryPath2;
        }

        public static SettingsLogsInLogsDir GetLogsInLogsDir()
        {
            if (ActiveProfileIndex == 1)
                return LogsInLogsDir1;
            else
                return LogsInLogsDir2;
        }

        public static string GetZoneDataSet()
        {
            if (ActiveProfileIndex == 1)
                return ZoneDataSet1;
            else
                return ZoneDataSet2;
        }

        public static bool SettingsFileExists
        {
            get { return File.Exists(Paths.SettingsFilePath); }
        }

        public static void InitializeSettings(bool initializeDefaultSettings = false)
        {
            if (!initializeDefaultSettings)
            {
                LoadSettings();
            }
        }

        public static void BackupSettings()
        {
            File.Copy(Paths.SettingsFilePath, Paths.SettingsFilePath + $"_Backup_{DateTime.Now.Ticks}");
        }

        // This is so dumb I can't believe I made this with my own two beautiful hands
        public static T ProcessSetting<T>(T convertTarget, string value, ref string storeInString)
        {
            if (convertTarget != null)
            {
                storeInString = convertTarget.ToString();
            }

            T convertedValue;
            try
            {
                convertedValue = (T)Convert.ChangeType(value, typeof(T));
            }
            catch (InvalidCastException ex)
            {
                // Okay, try it with Json then
                convertedValue = JsonConvert.DeserializeObject<T>(value);
            }

            return convertedValue;
        }

        public static int Clamp(int value, int min, int max)
        {
            return value;
        }

        // since dotnet doesn't even have clamp smh
        public static T Clamp<T>(this T value, T min, T max) where T : IComparable<T>
        {
            return ((value.CompareTo(min) < 0) ? min : ((value.CompareTo(max) > 0) ? max : value));
        }

        public static void LoadSettings(bool forceReload = false)
        {
            if (!forceReload && Loaded)
                return;

            using (FileStream fs = File.OpenRead(Paths.SettingsFilePath))
            {
                using (TextReader tr = new StreamReader(fs))
                {
                    string line = "";
                    string key = "";
                    string value = "";
                    string aggregatedResult = "";
                    while ((line = tr.ReadLine()) != null)
                    {
                        try
                        {
                            string[] readValues = line.Split('=');

                            key = readValues[0];
                            if (readValues.Length != 2)
                            {
                                // Well, that's not what we expected, it might be one of the font settings with a weird string in it. Just combine everything after the first =
                                value = string.Join("=", readValues.ToList().GetRange(1, readValues.Length - 1));
                            }
                            else
                            {
                                value = readValues[1];
                            }

                            string currentValue = "";

                            Console.WriteLine($"{key}, {value}");

                            // if this wasn't a static class we could use reflection to do this
                            // or we could make a collection object and serialize the whole thing instead of parsing em one value at a time
                            // or we could use a regular dotnet winforms settings object, like Settings.settings, that's built in
                            // but here we are lmao
                            // it can be refactored in the future but it's really not that important
                            switch (key)
                            {
                                case "EQDirectoryPath1":
                                    EQDirectoryPath1 = ProcessSetting(EQDirectoryPath1, value, ref currentValue);
                                    break;

                                case "EQDirectoryPath2":
                                    EQDirectoryPath2 = ProcessSetting(EQDirectoryPath2, value, ref currentValue);
                                    break;

                                case "LogsInLogsDir1":
                                    LogsInLogsDir1 = ProcessSetting(LogsInLogsDir1, value, ref currentValue);
                                    break;

                                case "LogsInLogsDir2":
                                    LogsInLogsDir2 = ProcessSetting(LogsInLogsDir2, value, ref currentValue);
                                    break;

                                case "ZoneDataSet1":
                                    ZoneDataSet1 = ProcessSetting(ZoneDataSet1, value, ref currentValue);
                                    break;

                                case "ZoneDataSet2":
                                    ZoneDataSet2 = ProcessSetting(ZoneDataSet2, value, ref currentValue);
                                    break;

                                case "ActiveProfileIndex":
                                    ActiveProfileIndex = ProcessSetting(ActiveProfileIndex, value, ref currentValue);
                                    break;

                                case "CheckAutoSizeOnMapSwitch":
                                    CheckAutoSizeOnMapSwitch = ProcessSetting(CheckAutoSizeOnMapSwitch, value, ref currentValue);
                                    break;

                                case "CheckGroupByContinent":
                                    CheckGroupByContinent = ProcessSetting(CheckGroupByContinent, value, ref currentValue);
                                    break;

                                case "CheckEnableDirection":
                                    CheckEnableDirection = ProcessSetting(CheckEnableDirection, value, ref currentValue);
                                    break;

                                case "CheckEnableLegend":
                                    CheckEnableLegend = ProcessSetting(CheckEnableLegend, value, ref currentValue);
                                    break;

                                case "LastSelectedZone":
                                    LastSelectedZone = ProcessSetting(ZoneDataSet2, value, ref currentValue);
                                    break;

                                case "WikiRootURL":
                                    WikiRootURL = ProcessSetting(ZoneDataSet2, value, ref currentValue);
                                    break;

                                case "OpacityLevel":
                                    OpacityLevel = ProcessSetting(OpacityLevel, value, ref currentValue);
                                    OpacityLevel = Clamp(OpacityLevel, 5, 100);
                                    break;

                                case "MinimizeToTray":
                                    MinimizeToTray = ProcessSetting(MinimizeToTray, value, ref currentValue);
                                    break;

                                case "AlwaysOnTop":
                                    AlwaysOnTop = ProcessSetting(AlwaysOnTop, value, ref currentValue);
                                    break;

                                case "LegendFontSize":
                                    LegendFontSize = ProcessSetting(LegendFontSize, value, ref currentValue);
                                    LegendFontSize = Clamp(LegendFontSize, 2, 256);
                                    break;

                                case "UseLegacyUI":
                                    UseLegacyUI = ProcessSetting(UseLegacyUI, value, ref currentValue);
                                    break;

                                case "PopoutMapAlwaysOnTop":
                                    PopoutMapAlwaysOnTop = ProcessSetting(PopoutMapAlwaysOnTop, value, ref currentValue);
                                    break;

                                case "PopoutMapOpacityLevel":
                                    PopoutMapOpacityLevel = ProcessSetting(PopoutMapOpacityLevel, value, ref currentValue);
                                    PopoutMapOpacityLevel = Clamp(PopoutMapOpacityLevel, 5, 100);
                                    break;

                                case "NotesShow":
                                    NotesShow = ProcessSetting(NotesShow, value, ref currentValue);
                                    break;

                                case "NotesFont":
                                    NotesFont = ProcessSetting(NotesFont, value, ref currentValue);
                                    break;

                                case "NotesColor":
                                    NotesColor = ProcessSetting(NotesColor, value, ref currentValue);
                                    break;

                                case "NotesClearAfterEntry":
                                    NotesClearAfterEntry = ProcessSetting(NotesClearAfterEntry, value, ref currentValue);
                                    break;

                                case "NotesAutoUpdate":
                                    NotesAutoUpdate = ProcessSetting(NotesAutoUpdate, value, ref currentValue);
                                    break;

                                case "NotesAutoSave":
                                    NotesAutoSave = ProcessSetting(NotesAutoSave, value, ref currentValue);
                                    break;

                                case "LocHistoryShow":
                                    LocHistoryShow = ProcessSetting(LocHistoryShow, value, ref currentValue);
                                    break;

                                case "LocHistoryNumberToTrack":
                                    LocHistoryNumberToTrack = ProcessSetting(LocHistoryNumberToTrack, value, ref currentValue);
                                    LocHistoryNumberToTrack = Clamp(LocHistoryNumberToTrack, 0, 4096);
                                    break;

                                case "PopoutMapPositionX":
                                    PopoutMapPositionX = ProcessSetting(PopoutMapPositionX, value, ref currentValue);
                                    break;
                                case "PopoutMapPositionY":
                                    PopoutMapPositionY = ProcessSetting(PopoutMapPositionY, value, ref currentValue);
                                    break;
                                case "PopoutMapSizeWidth":
                                    PopoutMapSizeWidth = ProcessSetting(PopoutMapSizeWidth, value, ref currentValue);
                                    break;
                                case "PopoutMapSizeHeight":
                                    PopoutMapSizeHeight = ProcessSetting(PopoutMapSizeHeight, value, ref currentValue);
                                    break;

                                case "AutoParseIntervalMS":
                                    AutoParseIntervalMS = ProcessSetting(AutoParseIntervalMS, value, ref currentValue);
                                    break;

                                default:
                                    Console.WriteLine($"Unknown Key: {key}");
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            // Error! Add the problem setting to the list to show later so the user can see which settings got screwed up.
                            aggregatedResult += $"Error with setting {key}: \n{ex.Message}";
                            Console.WriteLine(ex.Message);
                            continue;
                        }
                    }
                    if (!string.IsNullOrEmpty(aggregatedResult))
                    {
                        DialogResult settingErrorResult = MessageBox.Show($"There was a problem loading or parsing one or more of the settings." +
                            $"\n\n{aggregatedResult}" +
                            $"Some settings have been reset to their default values. Here's some details" +
                            $"\n\nWould you like to make a backup of the currently broken settings file?",
                            "Setting Load Error",
                            MessageBoxButtons.YesNo);
                        if (settingErrorResult == DialogResult.Yes)
                        {
                            BackupSettings();
                        }
                    }
                }
            }

            // Backward compatibility when adding new settings
            if (LegendFontSize == 0)
                LegendFontSize = 10;

            Loaded = true;
            return;
        }

        public static void SaveSettings()
        {
            using (FileStream fs = File.Create(Paths.SettingsFilePath))
            {
                using (TextWriter tw = new StreamWriter(fs))
                {
                    WriteSetting(tw, "EQDirectoryPath1", EQDirectoryPath1);
                    WriteSetting(tw, "EQDirectoryPath2", EQDirectoryPath2);
                    WriteSetting(tw, "LogsInLogsDir1", Convert.ToInt32(LogsInLogsDir1).ToString());
                    WriteSetting(tw, "LogsInLogsDir2", Convert.ToInt32(LogsInLogsDir2).ToString());
                    WriteSetting(tw, "ZoneDataSet1", ZoneDataSet1);
                    WriteSetting(tw, "ZoneDataSet2", ZoneDataSet2);
                    WriteSetting(tw, "ActiveProfileIndex", ActiveProfileIndex.ToString());
                    WriteSetting(tw, "CheckAutoSizeOnMapSwitch", CheckAutoSizeOnMapSwitch.ToString());
                    WriteSetting(tw, "CheckGroupByContinent", CheckGroupByContinent.ToString());
                    WriteSetting(tw, "CheckEnableDirection", CheckEnableDirection.ToString());
                    WriteSetting(tw, "CheckEnableLegend", CheckEnableLegend.ToString());
                    WriteSetting(tw, "LastSelectedZone", LastSelectedZone);
                    WriteSetting(tw, "WikiRootURL", WikiRootURL);
                    WriteSetting(tw, "OpacityLevel", OpacityLevel.ToString());
                    WriteSetting(tw, "MinimizeToTray", MinimizeToTray.ToString());
                    WriteSetting(tw, "AlwaysOnTop", AlwaysOnTop.ToString());
                    WriteSetting(tw, "LegendFontSize", LegendFontSize.ToString());
                    WriteSetting(tw, "UseLegacyUI", UseLegacyUI.ToString());

                    WriteSetting(tw, "PopoutMapAlwaysOnTop", PopoutMapAlwaysOnTop.ToString());
                    WriteSetting(tw, "PopoutMapOpacityLevel", PopoutMapOpacityLevel.ToString());

                    WriteSetting(tw, "NotesShow", NotesShow.ToString());
                    WriteSetting(tw, "NotesFont", JsonConvert.SerializeObject(NotesFont));
                    WriteSetting(tw, "NotesColor", JsonConvert.SerializeObject(NotesColor));
                    WriteSetting(tw, "NotesClearAfterEntry", NotesClearAfterEntry.ToString());
                    WriteSetting(tw, "NotesAutoUpdate", NotesAutoUpdate.ToString());
                    WriteSetting(tw, "NotesAutoSave", NotesAutoSave.ToString());

                    WriteSetting(tw, "LocHistoryShow", LocHistoryShow.ToString());
                    WriteSetting(tw, "LocHistoryNumberToTrack", LocHistoryNumberToTrack.ToString());

                    WriteSetting(tw, "PopoutMapPositionX", PopoutMapPositionX.ToString());
                    WriteSetting(tw, "PopoutMapPositionY", PopoutMapPositionY.ToString());
                    WriteSetting(tw, "PopoutMapSizeWidth", PopoutMapSizeWidth.ToString());
                    WriteSetting(tw, "PopoutMapSizeHeight", PopoutMapSizeHeight.ToString());
                    WriteSetting(tw, "AutoParseIntervalMS", AutoParseIntervalMS.ToString());
                }
            }
        }

        private static void WriteSetting(TextWriter tw, string key, string value)
        {
            tw.WriteLine(String.Format("{0}={1}", key, value));
        }
    }
}
