using System;
using System.Net;
using System.Net.Sockets;
using FastLink.Patches;
using Steamworks;
using UnityEngine;
using UnityEngine.EventSystems;
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


    internal static void PopulateServerList(GameObject linkpanel)
    {
        FastLinkPlugin.FastLinkLogger.LogDebug("POPULATE SERVER LIST");
        MServerListElement = linkpanel.transform.Find("ServerList/ServerElementSteamCrossplay").gameObject;
        linkpanel.transform.Find("ServerList").gameObject.GetComponent<Image>().enabled = false;
        GameObject? listRoot = GameObject.Find("GuiRoot/GUI/StartGui/FastLink/JoinPanel(Clone)/ServerList/ListRoot")
            .gameObject;
        listRoot.gameObject.transform.localScale = new Vector3(1, (float)0.8, 1);
        MServerListRoot = listRoot
            .GetComponent<RectTransform>();
        listRoot.gameObject.GetComponent<RectTransform>().pivot =
            new Vector2(listRoot.gameObject.GetComponent<RectTransform>().pivot.x,
                1); // Literally here just because Valheim's UI forces scrollbar to halfway.
        MServerCount = linkpanel.transform.Find("serverCount").gameObject.GetComponent<Text>();
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

        MServerList.Sort((Comparison<Definition>)((a, b) =>
            string.Compare(a?.serverName, b?.serverName, StringComparison.Ordinal)));
        if (MJoinServer != null && !MServerList.Contains(MJoinServer))
        {
            FastLinkPlugin.FastLinkLogger.LogDebug(
                "Server list does not contain selected server, clearing selected server");
            MJoinServer = MServerList.Count <= 0 ? null : MServerList[0];
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
            MServerListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(MServerListBaseSize,
                    MServerList.Count * m_serverListElementStep));
            for (int index = 0; index < MServerList.Count; ++index)
            {
                GameObject? gameObject = Object.Instantiate(MServerListElement,
                    MServerListRoot);
                gameObject.SetActive(true);
                ((gameObject.transform as RectTransform)!).anchoredPosition =
                    new Vector2(0.0f, index * -m_serverListElementStep);
                gameObject.GetComponent<Button>().onClick.AddListener(() => OnSelectedServer(gameObject));
                MServerListElements.Add(gameObject);
                if (MServerListElements.Count > 1)
                {
                    if (MServerCount != null) MServerCount.text = MServerListElements.Count + " Servers";
                }
                else
                {
                    if (MServerCount != null) MServerCount.text = MServerListElements.Count + " Server";
                }
            }
        }

        FastLinkPlugin.FastLinkLogger.LogDebug($"ServerList count: {MServerList.Count}");
        for (int index = 0; index < MServerList.Count; ++index)
        {
            Definition server = MServerList[index];
            GameObject? serverListElement = MServerListElements?[index];
            if (serverListElement == null) continue;
            serverListElement.GetComponentInChildren<Text>().text =
                index + 1 + ". " + server.serverName;
            serverListElement.GetComponentInChildren<UITooltip>().m_text = server.ToString();
            serverListElement.transform.Find("version").GetComponent<Text>().text = server.address;
            serverListElement.transform.Find("players").GetComponent<Text>().text = server.port.ToString();
            serverListElement.transform.Find("Private").gameObject
                .SetActive(server.password.Length > 1);
            serverListElement.transform.Find("PVP").gameObject
                .SetActive(server.ispvp);
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
            FastLinkPlugin.FastLinkLogger.LogDebug("Resolving: " + server.address);
            try
            {
                ResolveTask = Dns.GetHostEntryAsync(server.address);
                FastLinkPlugin.FastLinkLogger.LogDebug("Resolving after task: " +
                                                       ResolveTask.Result.AddressList[0]);
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
                FastLinkPlugin.FastLinkLogger.LogDebug("COMPLETE: " + server.address);
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
        string target = (address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{address}]" : $"{address}") +
                        $":{port}";
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
        networkingIpAddr.SetIPv6(address.GetAddressBytes(), networkingIpAddr.m_port);
        ZSteamMatchmaking.instance.m_joinData =
            (ServerJoinData)new ServerJoinDataDedicated(networkingIpAddr.GetIPv4(), networkingIpAddr.m_port);


        /*ZSteamMatchmaking.instance.m_joinAddr.SetIPv6(address.GetAddressBytes(), port);
        ZSteamMatchmaking.instance.m_haveJoinAddr = true;*/
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
            FastLinkPlugin.FastLinkLogger.LogDebug("The issues were found here: " + e);
        }

        return 1;
    }
}