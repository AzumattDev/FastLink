using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FastLink.Patches;
using FastLink.Util;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FastLink;

[BepInPlugin(ModGUID, ModName, ModVersion)]
[BepInIncompatibility("randyknapp.mods.auga")]
public class FastLinkPlugin : BaseUnityPlugin

{
    internal const string ModName = "FastLink";
    internal const string ModVersion = "1.0.0";
    internal const string Author = "Azumatt";
    private const string ModGUID = Author + "." + ModName;
    private static string ConfigFileName = ModGUID + ".cfg";
    private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

    private readonly Harmony _harmony = new(ModGUID);

    public static readonly ManualLogSource FastLinkLogger =
        BepInEx.Logging.Logger.CreateLogSource(ModName);

    private void Awake()
    {
        UIAnchor = Config.Bind("UI", "Position of the UI", new Vector2(-900, 200),
            new ConfigDescription("Sets the anchor position of the UI"));
        UIAnchor.SettingChanged += SaveConfig;

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

    private void SaveConfig(object sender, EventArgs e)
    {
        Config.Save();
    }


    #region ConfigOptions

    public static ConfigEntry<Vector2> UIAnchor = null!;

    #endregion
}