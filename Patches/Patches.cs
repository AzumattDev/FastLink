using FastLink.Util;
using HarmonyLib;

namespace FastLink.Patches;

[HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.OnSelelectCharacterBack))]
internal class PatchCharacterBack
{
    private static void Postfix()
    {
        if (!SetupGui.Fastlink)
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
        if (!SetupGui.Fastlink)
        {
            return;
        }

        JoinServerFailed();
    }

    private static void JoinServerFailed()
    {
        FastLinkPlugin.FastLinkLogger.LogError("Server connection failed");
        SetupGui.Connecting = null;
    }
}

[HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_ClientHandshake))]
internal class PatchPasswordPrompt
{
    private static bool Prefix(ZNet __instance, ZRpc rpc, bool needPassword, string serverPasswordSalt)
    {
        if (FastLinkPlugin.ShowPasswordPrompt.Value == FastLinkPlugin.Toggle.On) return true;
        string? str = Functions.CurrentPass();
        if (string.IsNullOrEmpty(str)) return true;
        ZNet.m_serverPasswordSalt = serverPasswordSalt;
        if (needPassword)
        {
            FastLinkPlugin.FastLinkLogger.LogDebug($"Authenticating with saved password...{str}");
            __instance.m_connectingDialog.gameObject.SetActive(false);
            __instance.SendPeerInfo(rpc, str);
            return false;
        }

        FastLinkPlugin.FastLinkLogger.LogDebug("Server didn't want password?");
        return true;
    }
}

[HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.LoadMainScene))]
static class FejdStartup_LoadMainScene_Patch
{
    static void Postfix(FejdStartup __instance)
    {
        if (SetupGui.Fastlink.activeSelf)
            SetupGui.Fastlink.SetActive(false);
    }
}