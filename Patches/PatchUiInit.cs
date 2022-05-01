using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using FastLink.Util;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FastLink.Patches
{
    [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.SetupGui))]
    internal class PatchUiInit
    {
        public static GameObject? Fastlink;
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
        public string? _errorMsg;

        private static void Postfix(FejdStartup __instance)
        {
            Servers.Init();


            GameObject fastlinkGo = new("FastLink");
            fastlinkGo.AddComponent<RectTransform>();
            fastlinkGo.AddComponent<DragControl>();
            fastlinkGo.transform.SetParent(GameObject.Find("GuiRoot/GUI/StartGui").transform);

            Fastlink = Object.Instantiate(GameObject.Find("GUI/StartGui/StartGame/Panel/JoinPanel").gameObject,
                fastlinkGo.transform);
            Fastlink.transform.SetParent(fastlinkGo.transform);
            Fastlink.gameObject.transform.localScale = new Vector3((float)0.85, (float)0.85, (float)0.85);
            fastlinkGo.transform.position =
                new Vector2(FastLinkPlugin.UIAnchor.Value.x, FastLinkPlugin.UIAnchor.Value.y);
            Fastlink.gameObject.AddComponent<DragControl>();

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
}