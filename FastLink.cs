using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FastLink.Patches;
using FastLink.Util;
using HarmonyLib;
using ServerSync;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FastLink
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class FastLinkPlugin : BaseUnityPlugin

    {
        // GuiRoot/GUI/StartGui/StartGame/Panel/JoinPanel
        internal const string ModName = "FastLink";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource FastLinkLogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static readonly ConfigSync ConfigSync = new(ModGUID)
            { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        private void Awake()
        {
            _serverConfigLocked = config("General", "Force Server Config", true, "Force Server Config");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            UIAnchor = config("UI", "Position of the UI", new Vector2(50, 800),
                new ConfigDescription("Sets the anchor position of the UI"), false);

            _harmony.PatchAll();
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;

            FileSystemWatcher serverWatcher = new(Paths.ConfigPath, Servers.ConfigFileName);
            serverWatcher.Changed += ReadNewServers;
            serverWatcher.Created += ReadNewServers;
            serverWatcher.Renamed += ReadNewServers;
            serverWatcher.IncludeSubdirectories = true;
            serverWatcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            serverWatcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                FastLinkLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                FastLinkLogger.LogError($"There was an issue loading your {ConfigFileName}");
                FastLinkLogger.LogError("Please check your config entries for spelling and format!");
            }
        }

        private void ReadNewServers(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(Servers.ConfigPath)) return;
            try
            {
                FastLinkLogger.LogDebug("Reloading Server List");
                PatchUiInit.Connecting = null;
                foreach (GameObject serverListElement in PatchUiInit.MServerListElements)
                    Destroy(serverListElement);
                PatchUiInit.MServerListElements.Clear();

                Servers.Init();
                Functions.AbortConnect();
                Functions.PopulateServerList(PatchUiInit.Fastlink);
                Functions.UpdateServerList();
                PatchUiInit.MJoinServer = null;
            }
            catch
            {
                FastLinkLogger.LogError($"There was an issue loading your {Servers.ConfigFileName}");
                FastLinkLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        private static ConfigEntry<bool>? _serverConfigLocked;
        public static ConfigEntry<Vector2> UIAnchor = null!;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            public bool? Browsable = false;
        }

        class AcceptableShortcuts : AcceptableValueBase // Used for KeyboardShortcut Configs 
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;
            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() =>
                "# Acceptable values: " + string.Join(", ", KeyboardShortcut.AllKeyCodes);
        }

        #endregion
    }
}