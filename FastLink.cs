using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FastLink.Patches;
using FastLink.Util;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastLink;

[BepInPlugin(ModGUID, ModName, ModVersion)]
//[BepInIncompatibility("randyknapp.mods.auga")]
public partial class FastLinkPlugin : BaseUnityPlugin

{
    internal const string ModName = "FastLink";
    internal const string ModVersion = "1.3.4";
    internal const string Author = "Azumatt";
    private const string ModGUID = Author + "." + ModName;
    private static string ConfigFileName = ModGUID + ".cfg";
    private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

    private readonly Harmony _harmony = new(ModGUID);

    public static readonly ManualLogSource FastLinkLogger =
        BepInEx.Logging.Logger.CreateLogSource(ModName);

    public enum Toggle
    {
        Off,
        On
    }

    private void Awake()
    {
        Config.Bind("General", "FastLink URL", "https://valheim.thunderstore.io/package/Azumatt/FastLink/",
            new ConfigDescription("Link to the mod page", null,
                new ConfigurationManagerAttributes
                {
                    HideSettingName = true, HideDefaultButton = true,
                    Description = $"Edit the {Servers.ConfigFileName} directly from the configuration manager.",
                    CustomDrawer = Functions.EditServersButton
                }));
        Sort = Config.Bind("General", "Sort List Alphabetically", Toggle.On,
            new ConfigDescription(
                "Sorts the Server List Alphabetically. If disabled, the list will be displayed in the same order as the file. NOTE: If you are using colors in your server name, if on, it will still sort but the color you use will have an affect on on the order."));
        Sort.SettingChanged += (_, _) => ReadNewServers(null!, null!);        
        HideIP = Config.Bind("General", "Hide the IP in the panel", Toggle.Off, new ConfigDescription("Turn on to hide the IP in the connection panel at the main menu. Also hides it in the tooltip"));
        HideIP.SettingChanged += (_, _) => ReadNewServers(null!, null!);
        UIAnchor = Config.Bind("UI", "Position of the UI", new Vector2(429f, 172f),
            new ConfigDescription("Sets the anchor position of the UI"));
        UIAnchor.SettingChanged += SaveAndReset;

        LocalScale = Config.Bind("UI", "LocalScale of the UI", new Vector3(1f, 1f, 1f),
            new ConfigDescription(
                "Sets the local scale the UI. This is overall size of the UI. Defaults to vanilla JoinGame UI size. I prefer 0.85, 0.85, 0.85"));
        LocalScale.SettingChanged += SaveAndReset;

        ShowPasswordPrompt = Config.Bind("General", "Show Password Prompt", Toggle.Off,
            new ConfigDescription(
                "Set to true if you want to still show the password prompt to the user. This is for servers that have a password but don't wish to use the file to keep the password."));

        ShowPasswordInTooltip = Config.Bind("General", "Show Password In Tooltip", Toggle.Off,
            new ConfigDescription(
                "Set to true if you want to show the password inside the tooltip hover."));
        ShowPasswordInTooltip.SettingChanged += (_, _) => ReadNewServers(null!, null!);
        LoadTooltipAsset("fastlink");
        _harmony.PatchAll();
        SetupWatcher();
    }

    private void Start()
    {
        Game.isModded = true;
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
            SetupGui.Connecting = null;
            foreach (GameObject serverListElement in SetupGui.MServerListElements)
                Destroy(serverListElement);
            SetupGui.MServerListElements.Clear();

            Servers.Init();
            Functions.AbortConnect();
            Functions.PopulateServerList(SetupGui.Fastlink);
            Functions.UpdateServerList();
            SetupGui.MJoinServer = null;
        }
        catch
        {
            if (Player.m_localPlayer == null || SceneManager.GetActiveScene().name != "main")
            {
                FastLinkLogger.LogError($"There was an issue loading your {Servers.ConfigFileName}");
                FastLinkLogger.LogError("Please check your config entries for spelling and format!");
            }
        }
    }

    private void SaveAndReset(object sender, EventArgs e)
    {
        Config.Save();
        SetupGui.FastlinkRootGo.GetComponent<RectTransform>().anchoredPosition =
            new Vector2(UIAnchor.Value.x, UIAnchor.Value.y);
        SetupGui.Fastlink.gameObject.transform.localScale = LocalScale.Value;
    }


    private void LoadTooltipAsset(string bundleName)
    {
        AssetBundle? assetBundle = GetAssetBundleFromResources(bundleName);
        SetupGui.FastlinkTooltip = assetBundle.LoadAsset<GameObject>("FastLinkTooltip");
        assetBundle?.Unload(false);
    }

    private static AssetBundle GetAssetBundleFromResources(string filename)
    {
        Assembly execAssembly = Assembly.GetExecutingAssembly();
        string resourceName = execAssembly.GetManifestResourceNames()
            .Single(str => str.EndsWith(filename));

        using Stream? stream = execAssembly.GetManifestResourceStream(resourceName);
        return AssetBundle.LoadFromStream(stream);
    }

    #region ConfigOptions

    public static ConfigEntry<Vector2> UIAnchor = null!;
    public static ConfigEntry<Vector3> LocalScale = null!;
    public static ConfigEntry<Toggle> Sort = null!;
    public static ConfigEntry<Toggle> HideIP = null!;
    public static ConfigEntry<Toggle> ShowPasswordPrompt = null!;
    public static ConfigEntry<Toggle> ShowPasswordInTooltip = null!;

    internal sealed class ConfigurationManagerAttributes
    {
        public Action<ConfigEntryBase> CustomDrawer = null!;
        public bool? HideDefaultButton;
        public bool? HideSettingName;
        public string Description = "";
    }

    #endregion
}