using System.Collections.Generic;
using System.Reflection;
using FastLink.Patches;
using FastLink.Util;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FastLink;

[HarmonyPatch]
public static class ZInput_Float_PreventMouseInput
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetJoyLeftStickX));
        yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetJoyLeftStickY));
        yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetJoyRTrigger));
        yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetJoyLTrigger));
        yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetJoyRightStickX));
        yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetJoyRightStickY));
        yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel));
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ref float __result)
    {
        if (Functions.MHasFocus)
            __result = 0f;
    }
}

[HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.UpdateGamepad))]
static class HandleGamepadPatch
{
    // Return false to skip vanilla UpdateGamepad on frames we consume input
    static bool Prefix(FejdStartup __instance)
    {
        var fastlink = SetupGui.Fastlink;
        if (fastlink == null || !fastlink.activeInHierarchy) return true;
        if (!ZInput.IsGamepadActive())
        {
            Functions.FocusFastLink(false);
            return true;
        }

        bool consumed = false;

        // RB -> focus FastLink (right bumper = JoystickButton5)
        if (Input.GetKeyDown(KeyCode.JoystickButton5))
        {
            Functions.FocusFastLink(true);
            consumed = true;
        }

        // If focused, handle navigation & submit entirely inside FastLink
        if (Functions.MHasFocus)
        {
            if (Functions.Buttons != null && Functions.Buttons.Count > 0)
            {
                // Disable navigation in the main menu when FastLink has focus for each button

                foreach (var button in Functions.Buttons)
                    button.navigation = new Navigation { mode = Navigation.Mode.None };
            }

            // D-pad or LS
            if (ZInput.GetButtonDown("JoyLStickDown") || ZInput.GetButtonDown("JoyDPadDown"))
            {
                Functions.MoveSelection(+1);
                consumed = true;
            }

            if (ZInput.GetButtonDown("JoyLStickUp") || ZInput.GetButtonDown("JoyDPadUp"))
            {
                Functions.MoveSelection(-1);
                consumed = true;
            }

            // A -> submit (JoystickButton0)
            if (Input.GetKeyDown(KeyCode.JoystickButton0))
            {
                Functions.SubmitSelection();
                Functions.FocusFastLink(false);
                consumed = true;
            }

            // B or LB -> defocus (B = JoystickButton1, LB = JoystickButton4)
            if (Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.JoystickButton4))
            {
                Functions.FocusFastLink(false);
                consumed = true;
            }

            // Skip vanilla UpdateGamepad when we handled input, so the world/character lists don't also move.
            return !consumed;
        }

        if (Functions.Buttons != null && Functions.Buttons.Count > 0)
        {
            // Restore navigation from cache for each button
            foreach (var button in Functions.Buttons)
            {
                if (Functions.ButtonNavigationCache.TryGetValue(button, out Navigation nav))
                    button.navigation = nav;
            }
        }

        return !consumed;
    }
}



[HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Awake))]
static class CacheButtonNavigationsInMenu
{
    static void Postfix(FejdStartup __instance)
    {
        Functions.CacheButtonNavigations(__instance);
    }
}