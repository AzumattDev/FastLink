using System;
using System.Net;
using System.Net.Sockets;
using FastLink.Patches;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static FastLink.Patches.PatchUiInit;
using Object = UnityEngine.Object;

namespace FastLink.Util;

public static class Functions
{
    internal static void DestroyAll(GameObject? thing)
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


    internal static void PopulateServerList(GameObject? linkpanel)
    {
        FastLinkPlugin.FastLinkLogger.LogDebug("POPULATE SERVER LIST");
        _mServerListElement = linkpanel.transform.Find("ServerList/ServerElement").gameObject;
        linkpanel.transform.Find("ServerList").gameObject.GetComponent<Image>().enabled = false;
        GameObject? listRoot = GameObject.Find("GuiRoot/GUI/StartGui/FastLink/JoinPanel(Clone)/ServerList/ListRoot")
            .gameObject;
        listRoot.gameObject.transform.localScale = new Vector3(1, (float)0.8, 1);
        _mServerListRoot = listRoot
            .GetComponent<RectTransform>();

        _mServerCount = linkpanel.transform.Find("serverCount").gameObject.GetComponent<Text>();
        _mServerListBaseSize = _mServerListRoot.rect.height;
    }

    internal static void UpdateServerList()
    {
        FastLinkPlugin.FastLinkLogger.LogDebug("UPDATE SERVER LIST");
        MServerList?.Clear();
        if (Connecting != null)
        {
            FastLinkPlugin.FastLinkLogger.LogDebug("Connecting not null");
            AbortConnect();
        }
        else if (Servers.entries.Count > 0)
        {
            foreach (Servers.Entry? entry in Servers.entries)
            {
                FastLinkPlugin.FastLinkLogger.LogError($"{entry.ToString()}");
                MServerList?.Add(entry);
                DoConnect(entry);
            }
        }
        else
        {
            FastLinkPlugin.FastLinkLogger.LogError("No servers defined");
            FastLinkPlugin.FastLinkLogger.LogError($"Please create this file {Servers.ConfigPath}");
        }

        MServerList?.Sort((Comparison<Servers.Entry?>)((a, b) =>
            string.Compare(a?.m_name, b?.m_name, StringComparison.Ordinal)));
        if (m_joinServer != null && !MServerList.Contains(m_joinServer))
        {
            FastLinkPlugin.FastLinkLogger.LogDebug(
                "Serverlist does not contain selected server, clearing selected server");
            m_joinServer = MServerList.Count <= 0 ? null : MServerList[0];
        }

        UpdateServerListGui(false);
    }

    private static void UpdateServerListGui(bool centerSelection)
    {
        if (MServerListElements != null && MServerList != null &&
            MServerList.Count != MServerListElements.Count)
        {
            FastLinkPlugin.FastLinkLogger.LogDebug("UPDATE SERVER LIST GUI");
            foreach (GameObject? serverListElement in MServerListElements)
                Object.Destroy(serverListElement);
            MServerListElements.Clear();
            _mServerListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(_mServerListBaseSize,
                    MServerList.Count * m_serverListElementStep));
            for (int index = 0; index < MServerList.Count; ++index)
            {
                GameObject? gameObject = Object.Instantiate(_mServerListElement,
                    _mServerListRoot);
                gameObject?.SetActive(true);
                ((gameObject?.transform as RectTransform)!).anchoredPosition =
                    new Vector2(0.0f, index * -m_serverListElementStep);
                gameObject.GetComponent<Button>().onClick.AddListener(OnSelectedServer);
                MServerListElements.Add(gameObject);
                if (MServerListElements.Count > 1)
                {
                    if (_mServerCount != null) _mServerCount.text = MServerListElements.Count + " Servers";
                }
                else
                {
                    if (_mServerCount != null) _mServerCount.text = MServerListElements.Count + " Server";
                }
            }
        }

        if (MServerList == null) return;
        FastLinkPlugin.FastLinkLogger.LogDebug($"ServerList count: {MServerList.Count}");
        for (int index = 0; index < MServerList.Count; ++index)
        {
            Servers.Entry? server = MServerList[index];
            GameObject? serverListElement = MServerListElements?[index];
            if (serverListElement == null) continue;
            serverListElement.GetComponentInChildren<Text>().text =
                index + 1 + ". " + server?.m_name;
            serverListElement.GetComponent<Button>().onClick.AddListener(() => DoConnect(new Servers.Entry
            {
                m_name = server.m_name, m_ip = server.m_ip, m_port = server.m_port, m_pass = server.m_pass
            }));
            serverListElement.GetComponentInChildren<UITooltip>().m_text = server?.ToString();
            serverListElement.transform.Find("version").GetComponent<Text>().text = server.m_ip;
            serverListElement.transform.Find("players").GetComponent<Text>().text = server.m_port.ToString();
            serverListElement.transform.Find("Private").gameObject
                .SetActive(server.m_pass.Length > 1);
            Transform target = serverListElement.transform.Find("selected");

            bool flag = m_joinServer != null && m_joinServer.Equals(server);
            target.gameObject.SetActive(flag);
        }
    }

    private static void DoConnect(Servers.Entry? server)
    {
        FastLinkPlugin.FastLinkLogger.LogDebug("DO CONNECT");
        Connecting = server;
        try
        {
            if (server?.m_ip == null) return;
            IPAddress.Parse(server?.m_ip);
            FastLinkPlugin.FastLinkLogger.LogDebug(
                $"Server and Port passed into DoConnect: {server.m_ip}:{server.m_port}");
            try
            {
                ZSteamMatchmaking.instance.QueueServerJoin($"{server.m_ip}:{server.m_port}");
            }
            catch (Exception e)
            {
                FastLinkPlugin.FastLinkLogger.LogDebug($"Error queueServerJoin: {e}");
            }
        }
        catch (FormatException ex)
        {
            FastLinkPlugin.FastLinkLogger.LogDebug("Resolving: " + server.m_ip);
            try
            {
                _resolveTask = Dns.GetHostEntryAsync(server.m_ip);
                FastLinkPlugin.FastLinkLogger.LogDebug("Resolving after task: " +
                                                       _resolveTask.Result.AddressList[0]);
            }
            catch (Exception e)
            {
                FastLinkPlugin.FastLinkLogger.LogError(
                    $"You are trying to resolve the IP : {server.m_ip}, but something is happening causing it to not work properly.");
            }

            if (_resolveTask == null)
            {
                FastLinkPlugin.FastLinkLogger.LogError("Your resolve task was null, fix it you idiot");
                return;
            }

            if (_resolveTask.IsFaulted)
            {
                FastLinkPlugin.FastLinkLogger.LogError($"Error resolving IP: {_resolveTask.Exception}");
                FastLinkPlugin.FastLinkLogger.LogError(_resolveTask.Exception != null
                    ? _resolveTask.Exception.InnerException.Message
                    : _resolveTask.Exception.Message);
                _resolveTask = null;
                Connecting = null;
            }
            else if (_resolveTask.IsCanceled)
            {
                FastLinkPlugin.FastLinkLogger.LogError($"Error CANCELED: {_resolveTask.Result.HostName}");
                _resolveTask = null;
                Connecting = null;
            }
            else if (_resolveTask.IsCompleted)
            {
                FastLinkPlugin.FastLinkLogger.LogDebug("COMPLETE: " + server.m_ip);
                foreach (IPAddress address in _resolveTask.Result.AddressList)
                {
                    if (address.AddressFamily != AddressFamily.InterNetwork) return;
                    FastLinkPlugin.FastLinkLogger.LogDebug($"Resolved Completed: {address}");
                    _resolveTask = null;

                    try
                    {
                        ZSteamMatchmaking.instance.QueueServerJoin($"{address}:{Connecting.m_port}");
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
                _resolveTask = null;
                Connecting = null;
                FastLinkPlugin.FastLinkLogger.LogError("Server DNS resolved to no valid addresses");
            }
        }
    }

    public static string? CurrentPass() => Connecting?.m_pass;

    public static void AbortConnect()
    {
        FastLinkPlugin.FastLinkLogger.LogDebug("ABORT CONNECT");
        Connecting = null;
        _resolveTask = null;
    }

    private static void OnSelectedServer()
    {
        FastLinkPlugin.FastLinkLogger.LogDebug("SELECTED SERVER");
        m_joinServer = MServerList[FindSelectedServer(EventSystem.current.currentSelectedGameObject)];
        UpdateServerListGui(false);
    }

    private static int FindSelectedServer(GameObject button)
    {
        FastLinkPlugin.FastLinkLogger.LogDebug("FIND SELETECED");
        for (int index = 0; index < MServerListElements.Count; ++index)
        {
            if ((Object)MServerListElements[index] == button)
                return index;
        }

        return -1;
    }
}