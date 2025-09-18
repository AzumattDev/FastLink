using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    internal static bool MHasFocus = false;
    internal static int MSelectedIndex = -1;
    private static GameObject _panelChrome;
    private static CanvasGroup _panelChromeCg;
    private static FastLinkPanelPulse _panelPulse;
    private static TMP_Text _focusHint;
    private static readonly Color PanelLineColor = new(1f, 0.85f, 0.20f, 1f);
    internal static Dictionary<Button, Navigation> ButtonNavigationCache = new();
    internal static HashSet<Button> Buttons = new();


    internal static void CacheButtonNavigations(FejdStartup __instance)
    {
        Transform? menu = __instance.transform.Find("Menu");
        if (menu == null) return;
        Transform? menulist = menu.Find("MenuList");
        if (menulist == null) return;
        Transform? entries = menulist.Find("MenuEntries");
        if (entries == null) return;
        Buttons = entries.GetComponentsInChildren<Button>(true).ToHashSet();
        foreach (Button? btn in Buttons)
        {
            if (!btn) continue;
            Navigation nav = btn.navigation;
            ButtonNavigationCache[btn] = nav;
        }
    }

    internal static void FocusFastLink(bool focus)
    {
        MHasFocus = focus;

        if (focus)
        {
            if (MServerList.Count > 0 && (MSelectedIndex < 0 || MSelectedIndex >= MServerList.Count))
                MSelectedIndex = 0;

            SyncJoinDataWithSelection();
            UpdateServerListGui(false);
        }

        UpdatePanelFocusChrome(focus);
    }

    internal static void MoveSelection(int delta)
    {
        if (!MHasFocus || MServerList.Count == 0) return;
        int next = Mathf.Clamp((MSelectedIndex < 0 ? 0 : MSelectedIndex) + delta, 0, MServerList.Count - 1);
        if (next == MSelectedIndex) return;
        MSelectedIndex = next;
        SyncJoinDataWithSelection();
        UpdateServerListGui(true);
    }

    internal static void SubmitSelection()
    {
        if (!MHasFocus || MSelectedIndex < 0 || MSelectedIndex >= MServerList.Count) return;

        var sel = MServerList[MSelectedIndex];
        MJoinServer = sel;


        Connect(new Definition
        {
            serverName = sel.serverName,
            address = sel.address,
            port = sel.port,
            password = sel.password
        });

        UpdateServerListGui(false);
    }

    private static void SyncJoinDataWithSelection()
    {
        if (MSelectedIndex < 0 || MSelectedIndex >= MServerList.Count) return;
        var def = MServerList[MSelectedIndex];

        // Use the string ctor so game can resolve DNS/host later (mirrors manual-add path)
        var dedicated = new ServerJoinDataDedicated($"{def.address}:{def.port}");
        var join = new ServerJoinData(dedicated);

        FejdStartup.instance?.SetServerToJoin(join);
    }

    public static void BuildPanelFocusChrome(GameObject panel)
    {
        // Clean up if rebuilt
        if (_panelChrome) Object.Destroy(_panelChrome);

        var panelRt = panel.GetComponent<RectTransform>();

        // Root overlay, stretches to the whole panel, no raycasts
        _panelChrome = new GameObject("FastLinkPanelChrome", typeof(RectTransform), typeof(CanvasGroup));
        var chromeRt = _panelChrome.GetComponent<RectTransform>();
        chromeRt.SetParent(panelRt.Find("ServerList"), false);
        chromeRt.anchorMin = Vector2.zero;
        chromeRt.anchorMax = Vector2.one;
        chromeRt.offsetMin = Vector2.zero;
        chromeRt.offsetMax = Vector2.zero;
        _panelChrome.transform.SetAsLastSibling(); // draw on top

        _panelChromeCg = _panelChrome.GetComponent<CanvasGroup>();
        _panelChromeCg.interactable = false;
        _panelChromeCg.blocksRaycasts = false;
        _panelChromeCg.alpha = 0f;

        // 4 thin edges
        CreateEdge("Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 2));
        CreateEdge("Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 2));
        CreateEdge("Left", new Vector2(0, 0), new Vector2(0, 1), new Vector2(2, 0));
        CreateEdge("Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(2, 0));

        // Small hint next to the header ("topic")
        var topic = panel.transform.Find("topic");
        if (topic != null)
        {
            var topicText = topic.GetComponent<TMP_Text>();
            if (!topicText) return;

            var hintText = GameObject.Instantiate(topicText, topic, false);
            hintText.name = "FastLinkGamepadHint";

            var hrt = hintText.rectTransform;
            hrt.anchorMin = new Vector2(0.5f, 1f);
            hrt.anchorMax = new Vector2(0.5f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = new Vector2(0f, -44f);

            hintText.text = "Focused • A: Join  B/LB: Back  D-Pad/LS: Navigate";
            hintText.fontSize = 18f;
            hintText.alignment = TextAlignmentOptions.CenterGeoAligned;
            hintText.enableWordWrapping = false;
            hintText.color = new Color(1f, 0.85f, 0.20f, 0.95f);
            hintText.raycastTarget = false;

            _focusHint = hintText;
            _focusHint.gameObject.SetActive(false);
        }


        // Gentle pulse while focused
        _panelPulse = _panelChrome.AddComponent<FastLinkPanelPulse>();
        _panelPulse.Init(_panelChromeCg);
    }

    private static void CreateEdge(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        var img = go.GetComponent<Image>();
        rt.SetParent(_panelChrome.transform, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = Vector2.zero;
        img.raycastTarget = false;
        img.color = PanelLineColor;
    }

    public static void UpdatePanelFocusChrome(bool focused)
    {
        if (!_panelChromeCg) return;

        // toggle pulse + alpha
        _panelPulse?.SetOn(focused);
        _panelChromeCg.alpha = focused ? 0.6f : 0f;

        // show hint only when a gamepad is active
        if (!_focusHint) return;
        _focusHint.text = focused ? "Focused • A: Join  B/LB: Back  D-Pad/LS: Navigate" : "Press RB to focus";
        _focusHint.gameObject.SetActive(ZInput.IsGamepadActive());
    }

    // tiny helper MB to animate the alpha smoothly (no GC)
    private sealed class FastLinkPanelPulse : MonoBehaviour
    {
        CanvasGroup _cg;
        bool _on;

        public void Init(CanvasGroup cg) => _cg = cg;

        public void SetOn(bool on)
        {
            _on = on;
            if (!on && _cg) _cg.alpha = 0f;
        }

        void Update()
        {
            if (!_on || !_cg) return;
            // 0.45–0.85 alpha pulse, unscaled so menus feel snappy
            float a = 0.45f + 0.40f * Mathf.PingPong(Time.unscaledTime * 1.6f, 1f);
            _cg.alpha = a;
        }
    }

    internal static void ApplyStatusToRow(int index, Definition server, ServerListEntryData entry)
    {
        if (index < 0 || index >= SetupGui.MServerListElements.Count) return;
        var row = SetupGui.MServerListElements[index];
        if (row == null) return;

        // Server name (left text in vanilla UI)
        var nameText = row.GetComponentInChildren<TMPro.TMP_Text>();
        if (nameText == null) return;
        nameText.text = $"{index + 1}. {entry.m_serverName}";
        row.GetComponentInChildren<TMP_Text>().text = $"{index + 1}. {server.serverName}";
        //serverListElement.GetComponentInChildren<UITooltip>().m_tooltipPrefab = FastlinkTooltip;
        row.GetComponentInChildren<UITooltip>().Set(server.serverName, server.ToString());


        // players
        var playersText = row.transform.Find("players").GetComponent<TMPro.TMP_Text>();
        if (entry.IsOnline && entry.HasMatchmakingData)
            playersText.text = $"{entry.m_playerCount} / {entry.m_playerLimit}";
        else if (entry.IsAvailable)
            playersText.text = "—";
        else
            playersText.text = ""; // unknown / not available

        // version (right text in vanilla UI)
        var versionText = row.transform.Find("version").GetComponent<TMPro.TMP_Text>();
        if (entry.HasMatchmakingData)
            versionText.text = $"{entry.m_gameVersion}";
        else
            versionText.text = FastLinkPlugin.HideIP.Value == FastLinkPlugin.Toggle.On ? "" : server.port.ToString();

        // modifiers (left small text). Keep your address, unless hidden.
        var modifiersText = row.transform.Find("modifiers").GetComponent<TMPro.TMP_Text>();
        if (FastLinkPlugin.HideIP.Value == FastLinkPlugin.Toggle.On)
            modifiersText.text = "Hidden";
        else
            modifiersText.text = server.address;

        // private/password icon: prefer live info; fall back to file
        row.transform.Find("Private").gameObject.SetActive((entry.HasMatchmakingData && entry.IsPasswordProtected) || (!entry.HasMatchmakingData && server.password.Length > 1));

        // crossplay icon: you control via config; dedicated internet pings won't report crossplay in Valheim
        row.transform.Find("crossplay").gameObject.SetActive(entry.IsCrossplay);

        // status “dot” (if your prefab has one). If yours is just an Image, we can toggle it when online.
        var status = row.transform.Find("status");
        if (status)
        {
            status.gameObject.SetActive(true);
            var slg = GameObject.Find("GUI/StartGui/StartGame/Panel/JoinPanel")?.GetComponent<ServerListGui>();
            if (slg != null)
            {
                var icons = slg.m_connectIcons;
                status.GetComponent<Image>().sprite = !entry.HasMatchmakingData ? icons.m_trying : (!entry.IsOnline ? (!entry.IsAvailable ? icons.m_unknown : icons.m_failed) : icons.m_success);
            }
        }
    }

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
        // Destroy all ServerListTab(Clone) objects
        foreach (Transform child in thing.transform)
        {
            if (child.name.StartsWith("ServerListTab(Clone)"))
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        // Destroy the new buttons for now
        Object.DestroyImmediate(thing.transform.Find("Add server").gameObject);
        Object.DestroyImmediate(thing.transform.Find("FavoriteButton").gameObject);
    }


    internal static void PopulateServerList(GameObject linkpanel)
    {
        FastLinkPlugin.FastLinkLogger.LogDebug("POPULATE SERVER LIST");
        MServerListElement = linkpanel.transform.Find("ServerList/ServerElement").gameObject;
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
            serverListElement.transform.Find("status").gameObject.SetActive(false);
            Transform target = serverListElement.transform.Find("selected");

            FastLink.Net.ServerStatusService.RequestStatus(server, index, ApplyStatusToRow);

            bool isFocusedSelect = MHasFocus && (MSelectedIndex == index);
            bool isMouseSelect = !MHasFocus && (MJoinServer != null && MJoinServer.Equals(server));
            target.gameObject.SetActive(isFocusedSelect || isMouseSelect);
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
        ZSteamMatchmaking.instance.m_joinData = new ServerJoinData(new ServerJoinDataDedicated(networkingIpAddr.GetIPv4(), port));
        return true;
    }


    public static string? CurrentPass() => Connecting?.password;

    public static void AbortConnect()
    {
        FastLinkPlugin.FastLinkLogger.LogDebug("ABORT CONNECT");
        Connecting = null;
        ResolveTask = null;
    }

    private static void OnSelectedServer(GameObject gameObject)
    {
        int idx = FindSelectedServer(gameObject);
        if (idx >= 0) MSelectedIndex = idx;

        MJoinServer = MServerList[idx];
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