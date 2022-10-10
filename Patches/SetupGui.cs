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
    public static GameObject Fastlink = null!;
    public static GameObject FastlinkTooltip = null!;
    public static GameObject FastlinkRootGo = null!;
    public static readonly List<Definition> MServerList = new();
    public static Definition? MJoinServer = new();
    public static readonly List<GameObject> MServerListElements = new();
    public static Text? MServerCount;
    public static GameObject MServerListElement = new();
    public static float m_serverListElementStep = 28f;
    public static RectTransform MServerListRoot = new();
    public static float MServerListBaseSize;

    public static Task<IPHostEntry>? ResolveTask;
    public static Definition? Connecting;

    private static void Postfix(FejdStartup __instance)
    {
        Connecting = null;
        foreach (GameObject serverListElement in MServerListElements)
            Object.DestroyImmediate(serverListElement);
        MServerListElements.Clear();

        Servers.Init();
        FastlinkRootGo = new GameObject("FastLink");
        FastlinkRootGo.AddComponent<RectTransform>();
        FastlinkRootGo.AddComponent<DragControl>();
        FastlinkRootGo.transform.SetParent(GameObject.Find("GuiRoot/GUI/StartGui").transform);

        Fastlink = Object.Instantiate(GameObject.Find("GUI/StartGui/StartGame/Panel/JoinPanel").gameObject,
            FastlinkRootGo.transform);
        Object.DestroyImmediate(Fastlink.gameObject.GetComponent<TabHandler>());
        Object.DestroyImmediate(Fastlink.gameObject.GetComponent<ServerList>());
        Object.DestroyImmediate(Fastlink.gameObject.GetComponent<UIGamePad>());
        Fastlink.transform.SetParent(FastlinkRootGo.transform);
        Fastlink.gameObject.transform.localScale = FastLinkPlugin.LocalScale.Value;
        FastlinkRootGo.transform.position =
            new Vector2(FastLinkPlugin.UIAnchor.Value.x, FastLinkPlugin.UIAnchor.Value.y);
        if (!Fastlink.activeSelf)
            Fastlink.SetActive(true);

        /* Set Mod Text */
        Fastlink.transform.Find("topic").GetComponent<Text>().text = "Fast Link";
        FastlinkTooltip.gameObject.SetActive(false); // disable it otherwise it always shows on the side.

        try
        {
            Functions.DestroyAll(Fastlink);
        }
        catch (Exception e)
        {
            FastLinkPlugin.FastLinkLogger.LogError("Problem in the destroying of things!" + e);
            throw;
        }

        Functions.PopulateServerList(Fastlink);
        Functions.UpdateServerList();
    }
}