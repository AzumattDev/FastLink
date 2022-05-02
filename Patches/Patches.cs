using System.Reflection;
using FastLink.Util;
using HarmonyLib;

namespace FastLink.Patches;

[HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.OnSelelectCharacterBack))]
internal class PatchCharacterBack
{
    private static void Postfix()
    {
        if (!PatchUiInit.Fastlink)
        {
            return;
        }

        Functions.AbortConnect();
    }
}

[HarmonyPatch(typeof(ZSteamMatchmaking), nameof(ZSteamMatchmaking.OnJoinServerFailed))]
internal class PatchConnectFailed
{
    private static void Postfix()
    {
        if (!PatchUiInit.Fastlink)
        {
            return;
        }

        JoinServerFailed();
    }

    private static void JoinServerFailed()
    {
        FastLinkPlugin.FastLinkLogger.LogError("Server connection failed");
        PatchUiInit.Connecting = null;
    }
}

[HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_ClientHandshake))]
internal class PatchPasswordPrompt
{
    private static bool Prefix(ZNet __instance, ZRpc rpc, bool needPassword)
    {
        string? str = Functions.CurrentPass();
        if (str == null) return true;
        if (needPassword)
        {
            FastLinkPlugin.FastLinkLogger.LogDebug("Authenticating with saved password...");
            __instance.m_connectingDialog.gameObject.SetActive(false);
            typeof(ZNet).GetMethod("SendPeerInfo", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(__instance, new object[2]
                {
                    rpc,
                    str
                });
            return false;
        }

        FastLinkPlugin.FastLinkLogger.LogDebug("Server didn't want password?");
        return true;
    }
}