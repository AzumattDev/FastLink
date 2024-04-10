using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using BepInEx.Configuration;
using FastLink.Patches;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static FastLink.Patches.SetupGui;
using Object = UnityEngine.Object;

namespace FastLink.Util;

public static class Functions
{
    internal static void DestroyAll(GameObject thing)
    {
        //Object.DestroyImmediate(thing.transform.Find("Join manually").gameObject);
        Object.DestroyImmediate(thing.transform.Find("FilterField").gameObject);
        Object.DestroyImmediate(thing.transform.Find("Refresh").gameObject);
        //Object.DestroyImmediate(thing.transform.Find("FriendGames").gameObject);
        //Object.DestroyImmediate(thing.transform.Find("PublicGames").gameObject);
        Object.DestroyImmediate(thing.transform.Find("Server help").gameObject);
        Object.DestroyImmediate(thing.transform.Find("Back").gameObject);
        Object.DestroyImmediate(thing.transform.Find("Join").gameObject);

        // Destroy the new tab buttons for now
        Object.DestroyImmediate(thing.transform.Find("FavoriteTab").gameObject);
        Object.DestroyImmediate(thing.transform.Find("RecentTab").gameObject);
        Object.DestroyImmediate(thing.transform.Find("FriendsTab").gameObject);
        Object.DestroyImmediate(thing.transform.Find("CommunityTab").gameObject);

        // Destroy the new buttons for now
        Object.DestroyImmediate(thing.transform.Find("Add server").gameObject);
        Object.DestroyImmediate(thing.transform.Find("FavoriteButton").gameObject);
    }

    internal static void MerchButton()
    {
        MerchRootGo = GameObject.Find("GUI/StartGui/Menu/StartGui_MerchButton").gameObject;
        if (MerchRootGo != null)
        {
            MerchRootGo.AddComponent<MerchAreaDragControl>();
        }
    }


    internal static void PopulateServerList(GameObject linkpanel)
    {
        FastLinkPlugin.FastLinkLogger.LogDebug("POPULATE SERVER LIST");
        MServerListElement = linkpanel.transform.Find("ServerList/ServerElementSteamCrossplay").gameObject;
        linkpanel.transform.Find("ServerList").gameObject.GetComponent<Image>().enabled = false;
        GameObject? listRoot = GameObject.Find("GuiRoot/GUI/StartGui/FastLink/JoinPanel(Clone)/ServerList/ListRoot").gameObject;
        listRoot.gameObject.transform.localScale = new Vector3(1, (float)0.8, 1);
        MServerListRoot = listRoot.GetComponent<RectTransform>();
        listRoot.gameObject.GetComponent<RectTransform>().pivot = new Vector2(listRoot.gameObject.GetComponent<RectTransform>().pivot.x, 1); // Literally here just because Valheim's UI forces scrollbar to halfway.
        MServerCount = linkpanel.transform.Find("serverCount").gameObject.GetComponent<TextMeshProUGUI>();
        MServerListBaseSize = MServerListRoot.rect.height;
    }

    internal static void UpdateServerList()
    {
        FastLinkPlugin.FastLinkLogger.LogDebug("UPDATE SERVER LIST");
        MServerList.Clear();
        if (Connecting != null)
        {
            FastLinkPlugin.FastLinkLogger.LogDebug("Connecting not null");
            AbortConnect();
        }
        else if (Servers.entries.Count > 0)
        {
            foreach (Definition Definition in Servers.entries)
            {
                MServerList.Add(Definition);
            }
        }
        else
        {
            FastLinkPlugin.FastLinkLogger.LogError("No servers defined");
            FastLinkPlugin.FastLinkLogger.LogError($"Please create this file {Servers.ConfigPath}");
        }

        if (FastLinkPlugin.Sort.Value == FastLinkPlugin.Toggle.On)
        {
            MServerList.Sort((Comparison<Definition>)((a, b) =>
                string.Compare(a?.serverName, b?.serverName, StringComparison.Ordinal)));
        }

        if (MJoinServer != null && !MServerList.Contains(MJoinServer))
        {
            FastLinkPlugin.FastLinkLogger.LogDebug("Server list does not contain selected server, clearing selected server");
            MJoinServer = null;
        }

        UpdateServerListGui(false);
    }

    private static void UpdateServerListGui(bool centerSelection)
    {
        if (MServerList.Count != MServerListElements.Count)
        {
            FastLinkPlugin.FastLinkLogger.LogDebug("UPDATE SERVER LIST GUI");
            foreach (GameObject? serverListElement in MServerListElements)
                Object.DestroyImmediate(serverListElement);
            MServerListElements.Clear();
            MServerListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(MServerListBaseSize, MServerList.Count * m_serverListElementStep));
            for (int index = 0; index < MServerList.Count; ++index)
            {
                GameObject? gameObject = Object.Instantiate(MServerListElement, MServerListRoot);
                gameObject.SetActive(true);
                ((gameObject.transform as RectTransform)!).anchoredPosition = new Vector2(0.0f, index * -m_serverListElementStep);
                gameObject.GetComponent<Button>().onClick.AddListener(() => OnSelectedServer(gameObject));
                if (!MServerListElements.Contains(gameObject))
                {
                    MServerListElements.Add(gameObject);
                }

                string servers = MServerListElements.Count > 1 ? " Servers" : " Server";
                if (MServerCount != null) MServerCount.text = MServerListElements.Count + servers;
            }
        }

        FastLinkPlugin.FastLinkLogger.LogDebug($"ServerList count: {MServerList.Count}");
        for (int index = 0; index < MServerList.Count; ++index)
        {
            Definition server = MServerList[index];
            GameObject? serverListElement = MServerListElements?[index];
            if (serverListElement == null) continue;
            serverListElement.GetComponentInChildren<TMP_Text>().text = $"{index + 1}. {server.serverName}";
            //serverListElement.GetComponentInChildren<UITooltip>().m_tooltipPrefab = FastlinkTooltip;
            serverListElement.GetComponentInChildren<UITooltip>().Set(server.serverName, server.ToString());
            //serverListElement.GetComponentInChildren<UITooltip>().m_text = server.ToString();

            // Cache the text elements
            var players = serverListElement.transform.Find("players").GetComponent<TMP_Text>().text = string.Empty;
            var modifiers = serverListElement.transform.Find("modifiers").GetComponent<TMP_Text>();
            var version = serverListElement.transform.Find("version").GetComponent<TMP_Text>();
            if (FastLinkPlugin.HideIP.Value == FastLinkPlugin.Toggle.On)
            {
                modifiers.text = "Hidden";
                version.text = "";
            }
            else
            {
                modifiers.text = server.address;
                version.text = server.port.ToString();
            }

            serverListElement.transform.Find("Private").gameObject.SetActive(server.password.Length > 1);
            serverListElement.transform.Find("PVP").gameObject.SetActive(server.ispvp);
            serverListElement.transform.Find("crossplay").gameObject.SetActive(server.iscrossplay);
            Transform target = serverListElement.transform.Find("selected");

            bool flag = MJoinServer != null && MJoinServer.Equals(server);
            target.gameObject.SetActive(flag);
        }
    }

    private static void Connect(Definition server)
    {
        FastLinkPlugin.FastLinkLogger.LogDebug("DO CONNECT");
        Connecting = server;
        try
        {
            IPAddress address = IPAddress.Parse(server.address);
            if (!JoinServer(address, server.port))
            {
                Connecting = null;
                FastLinkPlugin.FastLinkLogger.LogError("Server address was not valid");
            }
        }
        catch (FormatException)
        {
            FastLinkPlugin.FastLinkLogger.LogDebug($"Resolving: {server.address}");
            try
            {
                ResolveTask = Dns.GetHostEntryAsync(server.address);
                FastLinkPlugin.FastLinkLogger.LogDebug($"Resolving after task: {ResolveTask.Result.AddressList[0]}");
            }
            catch (Exception)
            {
                FastLinkPlugin.FastLinkLogger.LogError(
                    $"You are trying to resolve the IP : {server.address}, but something is happening causing it to not work properly.");
            }

            if (ResolveTask == null)
            {
                FastLinkPlugin.FastLinkLogger.LogError("Your resolve task was null, fix it you idiot");
                return;
            }

            if (ResolveTask.IsFaulted)
            {
                FastLinkPlugin.FastLinkLogger.LogError($"Error resolving IP: {ResolveTask.Exception}");
                FastLinkPlugin.FastLinkLogger.LogError(ResolveTask.Exception?.InnerException != null
                    ? ResolveTask.Exception.InnerException.Message
                    : ResolveTask.Exception?.Message);
                ResolveTask = null;
                Connecting = null;
            }
            else if (ResolveTask.IsCanceled)
            {
                FastLinkPlugin.FastLinkLogger.LogError($"Error CANCELED: {ResolveTask.Result.HostName}");
                ResolveTask = null;
                Connecting = null;
            }
            else if (ResolveTask.IsCompleted)
            {
                FastLinkPlugin.FastLinkLogger.LogDebug($"COMPLETE: {server.address}");
                foreach (IPAddress address in ResolveTask.Result.AddressList)
                {
                    FastLinkPlugin.FastLinkLogger.LogDebug($"Resolved Completed: {address}");
                    ResolveTask = null;
                    if (!JoinServer(address, server.port))
                    {
                        Connecting = null;
                        FastLinkPlugin.FastLinkLogger.LogError("Server DNS resolved to invalid address");
                    }

                    return;
                }
            }
            else
            {
                ResolveTask = null;
                Connecting = null;
                FastLinkPlugin.FastLinkLogger.LogError("Server DNS resolved to no valid addresses");
            }
        }
    }

    private static bool JoinServer(IPAddress address, ushort port)
    {
        string target = $"{(address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{address}]" : $"{address}")}:{port}";
        FastLinkPlugin.FastLinkLogger.LogDebug($"Server and Port passed into JoinServer: {target}");

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            address = address.MapToIPv6();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return false;
        }

        SteamNetworkingIPAddr networkingIpAddr = new();
        networkingIpAddr.SetIPv6(address.GetAddressBytes(), port);
        ZSteamMatchmaking.instance.m_joinData = (ServerJoinData)new ServerJoinDataDedicated(networkingIpAddr.GetIPv4(), port);

        /*ZSteamMatchmaking.instance.m_joinAddr.SetIPv6(address.GetAddressBytes(), port);
        ZSteamMatchmaking.instance.m_haveJoinAddr = true;*/
        return true;
    }

    /*private static bool JoinServer(IPAddress address, ushort port)
    {
        string target = $"{(address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{address}]" : $"{address}")}:{port}";
        FastLinkPlugin.FastLinkLogger.LogDebug($"Server and Port passed into JoinServer: {target}");

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            address = address.MapToIPv6();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return false;
        }

        // Assuming instance of your join data structure
        var myJoinData = GetMyJoinData(); // Replace with actual method to get join data

        bool isPlayFabServer = myJoinData is ServerJoinDataPlayFabUser;
        bool isSteamServer = myJoinData is ServerJoinDataSteamUser;
        bool isDedicatedServer = myJoinData is ServerJoinDataDedicated;

        if (isPlayFabServer)
        {
            // PlayFab server joining logic
            var playFabJoinData = myJoinData as ServerJoinDataPlayFabUser;
            ZNet.SetServerHost(playFabJoinData.m_remotePlayerId);
        }
        else if (isSteamServer)
        {
            // Steam server joining logic
            var steamJoinData = myJoinData as ServerJoinDataSteamUser;
            ZNet.SetServerHost((ulong)steamJoinData.m_joinUserID);
        }
        else if (isDedicatedServer)
        {
            // Dedicated server joining logic
            var dedicatedJoinData = myJoinData as ServerJoinDataDedicated;
            if (dedicatedJoinData.IsValid())
            {
                // Here, implement the logic for dedicated server joining,
                // similar to what you see in the base game's JoinServer method.
                // This might involve calling PlayFab matchmaking APIs and handling the response.
            }
            else
            {
                return false; // Invalid server data
            }
        }
        else
        {
            FastLinkPlugin.FastLinkLogger.LogError("Unknown server data type");
            return false;
        }

        // Additional code here for transitioning scenes, logging, etc., as needed.
        // ...

        return true;
    }*/


    public static string? CurrentPass() => Connecting?.password;

    public static void AbortConnect()
    {
        FastLinkPlugin.FastLinkLogger.LogDebug("ABORT CONNECT");
        Connecting = null;
        ResolveTask = null;
    }

    private static void OnSelectedServer(GameObject gameObject)
    {
        MJoinServer = MServerList[FindSelectedServer(gameObject)];
        Connect(new Definition
        {
            serverName = MJoinServer.serverName, address = MJoinServer.address, port = MJoinServer.port,
            password = MJoinServer.password
        });
        UpdateServerListGui(false);
    }

    private static int FindSelectedServer(Object button)
    {
        try
        {
            FastLinkPlugin.FastLinkLogger.LogDebug("FIND SELECTED");
            for (int index = 0; index < MServerListElements.Count; ++index)
            {
                if (index >= 0 && index < MServerListElements.Count && MServerListElements[index] == button)
                {
                    return index;
                }
            }

            return -1;
        }
        catch (Exception e)
        {
            FastLinkPlugin.FastLinkLogger.LogDebug($"The issues were found here: {e}");
        }

        return 1;
    }

    internal static void ShouldShowCursor()
    {
        if (FastLinkPlugin.EditorText.Length > 1)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    internal static void EditServersButton(ConfigEntryBase _)
    {
        GUILayout.BeginVertical();
        if (File.Exists(Servers.ConfigPath) && Servers.entries.Count > 0 &&
            GUILayout.Button("Edit Servers", GUILayout.ExpandWidth(true)))
        {
            FastLinkPlugin.EditorText = File.ReadAllText(Servers.ConfigPath);
        }

        GUILayout.EndVertical();
    }

    internal static void SaveFromEditor()
    {
        try
        {
            File.WriteAllText(Servers.ConfigPath, FastLinkPlugin.EditorText);
            FastLinkPlugin.FastLinkLogger.LogInfo("Saved servers, rebuilding list");
        }
        catch (Exception e)
        {
            FastLinkPlugin.FastLinkLogger.LogError($"Error saving servers: {e}");
        }
    }

    internal static void MakeShitDarkerInABadWay()
    {
        for (int i = 0; i < 6; ++i)
        {
            GUI.Box(FastLinkPlugin.ScreenRect, Texture2D.blackTexture);
        }
    }

    internal static void BuildContentScroller()
    {
        FastLinkPlugin.SettingWindowScrollPos =
            GUILayout.BeginScrollView(FastLinkPlugin.SettingWindowScrollPos, false, true);
        GUI.SetNextControlName("FastLinkEditor");
        GUIStyle style = new()
        {
            richText = true,
            normal =
            {
                textColor = Color.white,
                background = Texture2D.blackTexture
            }
        };
        if (FastLinkPlugin.RichTextOn)
        {
            FastLinkPlugin.EditorText = GUILayout.TextArea(FastLinkPlugin.EditorText, style,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
        }
        else
        {
            FastLinkPlugin.EditorText = GUILayout.TextArea(FastLinkPlugin.EditorText, GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
        }

        GUILayout.EndScrollView();
    }

    internal static void BuildButtons()
    {
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        if (GUILayout.Button(Localization.instance.Localize("$settings_apply"), GUILayout.ExpandWidth(true)))
        {
            SaveFromEditor();
            FastLinkPlugin.EditorText = "";
        }

        GUI.backgroundColor = Color.red;
        GUI.contentColor = Color.white;
        if (GUILayout.Button("Discard", GUILayout.ExpandWidth(true)))
        {
            FastLinkPlugin.EditorText = "";
        }

        GUILayout.Space(20);
        GUI.backgroundColor = Color.cyan;
        GUI.contentColor = Color.white;
        if (GUILayout.Button(FastLinkPlugin.RichTextOn ? "No Rich Text" : "Use Rich Text", GUILayout.ExpandWidth(true)))
        {
            FastLinkPlugin.RichTextOn = !FastLinkPlugin.RichTextOn;
        }

        GUILayout.EndVertical();
    }

    private static void DoQuickLoad(string fileName, FileHelpers.FileSource fileSource)
    {
        string worldName = PlayerPrefs.GetString("world");
        Game.SetProfile(fileName, fileSource);

        if (string.IsNullOrEmpty(worldName))
            return;

        FastLinkPlugin.FastLinkLogger.LogDebug($"got world name {worldName}");

        FejdStartup.instance.UpdateCharacterList();
        FejdStartup.instance.UpdateWorldList(true);

        bool isOn = FejdStartup.instance.m_publicServerToggle.isOn;
        bool isOn2 = FejdStartup.instance.m_openServerToggle.isOn;
        string text = FejdStartup.instance.m_serverPassword.text;
        World world = FejdStartup.instance.FindWorld(worldName);
        if (world == null)
            return;

        FastLinkPlugin.FastLinkLogger.LogDebug($"got world");


        ZNet.SetServer(true, isOn2, isOn, worldName, text, world);
        ZNet.ResetServerHost();

        FastLinkPlugin.FastLinkLogger.LogDebug($"Set server");
        try
        {
            string eventLabel = $"open:{isOn2},public:{isOn}";
            Gogan.LogEvent("Menu", "WorldStart", eventLabel, 0L);
        }
        catch
        {
            FastLinkPlugin.FastLinkLogger.LogDebug($"Error calling Gogan... oh well");
        }

        FastLinkPlugin.FastLinkLogger.LogDebug($"transitioning...");
        FejdStartup.instance.TransitionToMainScene();
    }
}