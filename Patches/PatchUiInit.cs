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
        public static GameObject? _fastlink;
        public static readonly List<Servers.Entry?>? MServerList = new();
        public static Servers.Entry? m_joinServer = new();
        public static readonly List<GameObject?>? MServerListElements = new();
        public static Text? _mServerCount;
        public static GameObject? _mServerListElement;
        public static float m_serverListElementStep = 28f;
        public static RectTransform? _mServerListRoot;
        public int MServerListRevision = -1;
        public static float _mServerListBaseSize;

        public static Task<IPHostEntry>? _resolveTask;
        public static Servers.Entry? Connecting;
        public string? _errorMsg;

        private static void Postfix(FejdStartup __instance)
        {
            Servers.Init();


            GameObject fastlinkGO = new("FastLink");
            fastlinkGO.AddComponent<RectTransform>();
            fastlinkGO.AddComponent<DragControl>();
            fastlinkGO.transform.SetParent(GameObject.Find("GuiRoot/GUI/StartGui").transform);

            _fastlink = Object.Instantiate(GameObject.Find("GUI/StartGui/StartGame/Panel/JoinPanel").gameObject,
                fastlinkGO.transform);
            _fastlink.transform.SetParent(fastlinkGO.transform);
            _fastlink.gameObject.transform.localScale = new Vector3((float)0.85, (float)0.85, (float)0.85);
            fastlinkGO.transform.position =
                new Vector2(FastLinkPlugin.UIAnchor.Value.x, FastLinkPlugin.UIAnchor.Value.y);
            _fastlink.gameObject.AddComponent<DragControl>();

            /* Set Mod Text */
            _fastlink.transform.Find("topic").GetComponent<Text>().text = "Fast Link";

            try
            {
                Functions.DestroyAll(_fastlink);
            }
            catch (Exception e)
            {
                FastLinkPlugin.FastLinkLogger.LogError(e);
                throw;
            }

            Functions.PopulateServerList(_fastlink);
            Functions.UpdateServerList();
        }
    }
}