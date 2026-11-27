using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace EffectBreakdown
{
    [HarmonyPatch]
    internal static class NotificationVisibilityPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var notification = typeof(UI_InGame_SkillNotifications);
            yield return AccessTools.Method(notification, "ShowSkillNotification");
            yield return AccessTools.Method(notification, "ShowSkillUpgrade");
            yield return AccessTools.Method(notification, "ShowGemUpgrade");
            yield return AccessTools.Method(notification, nameof(UI_InGame_SkillNotifications.FrameUpdate));
        }

        private static void Postfix(UI_InGame_SkillNotifications __instance, CanvasGroup ____canvasGroup,
            ref float ____currentElapsedTime)
        {
            if (!EffectBreakdownMod.HideItemNotifications || ____canvasGroup.alpha <= 0f)
                return;

            var edit = ManagerBase<EditSkillManager>.softInstance;
            if (edit == null || edit.mode == EditSkillManager.ModeType.EquipSkill ||
                edit.mode == EditSkillManager.ModeType.EquipGem)
                return;

            // The game also uses this panel for slot selection and plays upgrade feedback in the show methods.
            ____currentElapsedTime = float.PositiveInfinity;
            ____canvasGroup.alpha = 0f;
            __instance.skillObjects.SetActive(false);
            __instance.gemObjects.SetActive(false);
        }
    }

    [HarmonyPatch]
    internal static class GemNotificationVisibilityPatch
    {
        // Both slot selection and standalone essence notices call ShowGemNotification.
        [ThreadStatic]
        private static UI_InGame_SkillNotifications _equipPreview;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UI_InGame_SkillNotifications), "OnStateChanged")]
        private static void EnterStateChange(UI_InGame_SkillNotifications __instance, EditSkillManager.ModeType mode,
            out UI_InGame_SkillNotifications __state)
        {
            __state = _equipPreview;
            _equipPreview = mode == EditSkillManager.ModeType.EquipGem ? __instance : null;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(UI_InGame_SkillNotifications), "OnStateChanged")]
        private static void LeaveStateChange(UI_InGame_SkillNotifications __state) => _equipPreview = __state;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UI_InGame_SkillNotifications), "ShowGemNotification")]
        private static bool ShowGem(UI_InGame_SkillNotifications __instance) =>
            !EffectBreakdownMod.HideItemNotifications || ReferenceEquals(__instance, _equipPreview);
    }
}
