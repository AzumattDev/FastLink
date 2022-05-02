using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using FastLink.Util;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FastLink.Patches;

[HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.SetupGui))]
internal class SetupGui
{
    public static GameObject? Fastlink;
    public static GameObject? FastlinkRootGo;
    public static readonly List<Servers.Entry> MServerList = new();
    public static Servers.Entry? MJoinServer = new();
    public static readonly List<GameObject> MServerListElements = new();
    public static Text? MServerCount;
    public static GameObject MServerListElement = new();
    public static float m_serverListElementStep = 28f;
    public static RectTransform MServerListRoot = new();
    public static float MServerListBaseSize;

    public static Task<IPHostEntry>? ResolveTask;
    public static Servers.Entry? Connecting;

    private static void Postfix(FejdStartup __instance)
    {
        Connecting = null;
        foreach (GameObject serverListElement in MServerListElements)
            Object.Destroy(serverListElement);
        MServerListElements.Clear();

        Servers.Init();

        FastlinkRootGo = new GameObject("FastLink");
        FastlinkRootGo.AddComponent<RectTransform>();
        FastlinkRootGo.AddComponent<DragControl>();
        FastlinkRootGo.transform.SetParent(GameObject.Find("GuiRoot/GUI/StartGui").transform);

        Fastlink = Object.Instantiate(GameObject.Find("GUI/StartGui/StartGame/Panel/JoinPanel").gameObject,
            FastlinkRootGo.transform);
        Fastlink.transform.SetParent(FastlinkRootGo.transform);
        Fastlink.gameObject.transform.localScale = new Vector3((float)0.85, (float)0.85, (float)0.85);
        FastlinkRootGo.transform.position =
            new Vector2(FastLinkPlugin.UIAnchor.Value.x, FastLinkPlugin.UIAnchor.Value.y);


        /* Set Mod Text */
        Fastlink.transform.Find("topic").GetComponent<Text>().text = "Fast Link";

        try
        {
            Functions.DestroyAll(Fastlink);
        }
        catch (Exception e)
        {
            FastLinkPlugin.FastLinkLogger.LogError(e);
            throw;
        }

        Functions.PopulateServerList(Fastlink);
        Functions.UpdateServerList();
    }
}