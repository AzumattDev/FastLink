﻿using System;
using System.Net;
using System.Net.Sockets;
using FastLink.Patches;
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
        Object.Destroy(thing.transform.Find("Join manually").gameObject);
        Object.Destroy(thing.transform.Find("FilterField").gameObject);
        Object.Destroy(thing.transform.Find("Refresh").gameObject);
        Object.Destroy(thing.transform.Find("FriendGames").gameObject);
        Object.Destroy(thing.transform.Find("PublicGames").gameObject);
        Object.Destroy(thing.transform.Find("Server help").gameObject);
        Object.Destroy(thing.transform.Find("Back").gameObject);
        Object.Destroy(thing.transform.Find("Join").gameObject);
    }


    internal static void PopulateServerList(GameObject linkpanel)
    {
        FastLinkPlugin.FastLinkLogger.LogDebug("POPULATE SERVER LIST");
        MServerListElement = linkpanel.transform.Find("ServerList/ServerElement").gameObject;
        linkpanel.transform.Find("ServerList").gameObject.GetComponent<Image>().enabled = false;
        GameObject? listRoot = GameObject.Find("GuiRoot/GUI/StartGui/FastLink/JoinPanel(Clone)/ServerList/ListRoot")
            .gameObject;
        listRoot.gameObject.transform.localScale = new Vector3(1, (float)0.8, 1);
        MServerListRoot = listRoot
            .GetComponent<RectTransform>();

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
            foreach (Servers.Entry entry in Servers.entries)
            {
                MServerList.Add(entry);
            }
        }
        else
        {
            FastLinkPlugin.FastLinkLogger.LogError("No servers defined");
            FastLinkPlugin.FastLinkLogger.LogError($"Please create this file {Servers.ConfigPath}");
        }

        MServerList.Sort((Comparison<Servers.Entry>)((a, b) =>
            string.Compare(a?.MName, b?.MName, StringComparison.Ordinal)));
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
                Object.Destroy(serverListElement);
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
                gameObject.GetComponent<Button>().onClick.AddListener(OnSelectedServer);
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
            Servers.Entry server = MServerList[index];
            GameObject? serverListElement = MServerListElements?[index];
            if (serverListElement == null) continue;
            serverListElement.GetComponentInChildren<Text>().text =
                index + 1 + ". " + server.MName;
            serverListElement.GetComponentInChildren<UITooltip>().m_text = server.ToString();
            serverListElement.transform.Find("version").GetComponent<Text>().text = server.Mip;
            serverListElement.transform.Find("players").GetComponent<Text>().text = server.MPort.ToString();
            serverListElement.transform.Find("Private").gameObject
                .SetActive(server.MPass.Length > 1);
            Transform target = serverListElement.transform.Find("selected");

            bool flag = MJoinServer != null && MJoinServer.Equals(server);
            target.gameObject.SetActive(flag);
        }
    }

    private static void Connect(Servers.Entry server)
    {
        FastLinkPlugin.FastLinkLogger.LogDebug("DO CONNECT");
        Connecting = server;
        try
        {
            IPAddress.Parse(server.Mip);
            FastLinkPlugin.FastLinkLogger.LogDebug(
                $"Server and Port passed into Connect: {server.Mip}:{server.MPort}");
            try
            {
                ZSteamMatchmaking.instance.QueueServerJoin($"{server.Mip}:{server.MPort}");
            }
            catch (Exception e)
            {
                FastLinkPlugin.FastLinkLogger.LogDebug($"Error queueServerJoin: {e}");
            }
        }
        catch (FormatException)
        {
            FastLinkPlugin.FastLinkLogger.LogDebug("Resolving: " + server.Mip);
            try
            {
                ResolveTask = Dns.GetHostEntryAsync(server.Mip);
                FastLinkPlugin.FastLinkLogger.LogDebug("Resolving after task: " +
                                                       ResolveTask.Result.AddressList[0]);
            }
            catch (Exception)
            {
                FastLinkPlugin.FastLinkLogger.LogError(
                    $"You are trying to resolve the IP : {server.Mip}, but something is happening causing it to not work properly.");
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
                FastLinkPlugin.FastLinkLogger.LogDebug("COMPLETE: " + server.Mip);
                foreach (IPAddress address in ResolveTask.Result.AddressList)
                {
                    if (address.AddressFamily != AddressFamily.InterNetwork) return;
                    FastLinkPlugin.FastLinkLogger.LogDebug($"Resolved Completed: {address}");
                    ResolveTask = null;

                    try
                    {
                        ZSteamMatchmaking.instance.QueueServerJoin($"{address}:{Connecting.MPort}");
                    }
                    catch (Exception e)
                    {
                        FastLinkPlugin.FastLinkLogger.LogDebug($"ERROR: {e}");
                        return;
                    }
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

    public static string? CurrentPass() => Connecting?.MPass;

    public static void AbortConnect()
    {
        FastLinkPlugin.FastLinkLogger.LogDebug("ABORT CONNECT");
        Connecting = null;
        ResolveTask = null;
    }

    private static void OnSelectedServer()
    {
        FastLinkPlugin.FastLinkLogger.LogDebug("SELECTED SERVER");
        MJoinServer = MServerList[FindSelectedServer(EventSystem.current.currentSelectedGameObject)];
        Connect(new Servers.Entry
        {
            MName = MJoinServer.MName, Mip = MJoinServer.Mip, MPort = MJoinServer.MPort, MPass = MJoinServer.MPass
        });
        UpdateServerListGui(false);
    }

    private static int FindSelectedServer(Object button)
    {
        FastLinkPlugin.FastLinkLogger.LogDebug("FIND SELECTED");
        for (int index = 0; index < MServerListElements.Count; ++index)
        {
            if (MServerListElements[index] == button)
                return index;
        }

        return -1;
    }
}