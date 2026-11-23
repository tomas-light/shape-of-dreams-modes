using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChaosSlots
{
    [HarmonyPatch(typeof(UI_InGame_SkillButton_GemGroup), nameof(UI_InGame_SkillButton_GemGroup.LogicUpdate))]
    internal static class ChaosGemGroups
    {
        private static readonly AccessTools.FieldRef<UI_InGame_SkillButton_GemGroup, UI_InGame_SkillButton> Button =
            AccessTools.FieldRefAccess<UI_InGame_SkillButton_GemGroup, UI_InGame_SkillButton>("_button");
        private static readonly AccessTools.FieldRef<UI_InGame_SkillButton_GemGroup, int> LastAppliedCount =
            AccessTools.FieldRefAccess<UI_InGame_SkillButton_GemGroup, int>("_lastAppliedGemCount");
        private static readonly System.Reflection.MethodInfo OnEditModeChanged =
            AccessTools.Method(typeof(UI_InGame_GemSlot_EditSkill), "OnModeChanged");
        private static readonly Dictionary<UI_InGame_SkillButton_GemGroup, GroupChanges> Changes = new();

        private sealed class GroupChanges
        {
            internal GameObject[] OriginalGroups { get; }
            internal List<GameObject> AddedGroups { get; } = new();

            internal GroupChanges(GameObject[] originalGroups) => OriginalGroups = originalGroups;
        }

        private static void Prefix(UI_InGame_SkillButton_GemGroup __instance)
        {
            var player = DewPlayer.local;
            if (!ChaosSlotState.IsLocalPlayer(player))
                return;

            var button = Button(__instance);
            if (button == null || !ChaosSlotState.TryMemorySlot(button.skillType, out _))
                return;

            int count = player.hero.Skill.GetMaxGemCount(button.skillType);
            if (count <= SlotRules.BaseSlots || count > SlotRules.BaseSlots + SlotRules.BonusLimit)
                return;

            if (Changes.ContainsKey(__instance) || ChaosSlotState.FindBonus(player.hero) == null)
                return;

            var originals = __instance.groups;
            if (originals.Length < SlotRules.BaseSlots + 1)
                return;
            var nativeThree = originals[SlotRules.BaseSlots - 1];
            var nativeFour = originals[SlotRules.BaseSlots];
            var nativeArc = nativeFour.GetComponentsInChildren<UI_InGame_GemSlot>(true);
            if (nativeArc.Length != SlotRules.BonusLimit)
                return;

            ForgetDestroyedGroups();
            var changes = new GroupChanges(originals);
            try
            {
                int maxCount = SlotRules.BaseSlots + SlotRules.BonusLimit;
                var groups = new GameObject[Math.Max(originals.Length, maxCount)];
                Array.Copy(originals, groups, originals.Length);
                for (int bonuses = 1; bonuses <= SlotRules.BonusLimit; bonuses++)
                    groups[SlotRules.BaseSlots + bonuses - 1] = CreateLayout(nativeThree, nativeArc, button, bonuses, changes);

                for (int i = SlotRules.BaseSlots; i < Math.Min(maxCount, originals.Length); i++)
                    originals[i].SetActive(false);
                __instance.groups = groups;
                LastAppliedCount(__instance) = int.MinValue;
                Changes.Add(__instance, changes);
            }
            catch
            {
                foreach (var added in changes.AddedGroups)
                    Object.Destroy(added);
                throw;
            }
        }

        private static GameObject CreateLayout(
            GameObject nativeThree, UI_InGame_GemSlot[] nativeArc, UI_InGame_SkillButton button,
            int bonusCount, GroupChanges changes)
        {
            var staging = new GameObject("Chaos slot layout setup", typeof(RectTransform));
            staging.SetActive(false);
            try
            {
                var expanded = Object.Instantiate(nativeThree, staging.transform, false);
                changes.AddedGroups.Add(expanded);
                expanded.name = $"ChaosSlots {SlotRules.BaseSlots + bonusCount}";
                expanded.SetActive(false);
                expanded.transform.SetParent(nativeThree.transform.parent, false);
                var baseSlots = expanded.GetComponentsInChildren<UI_InGame_GemSlot>(true);
                var sample = Array.Find(baseSlots, slot => slot.slotIndex == 1);
                if (sample == null)
                    throw new InvalidOperationException("The native 3-slot layout has no middle slot.");

                var bonusSlots = new UI_InGame_GemSlot[bonusCount];
                var handles = new ChaosSlotHandle[bonusCount];
                for (int i = 0; i < bonusSlots.Length; i++)
                {
                    var clone = Object.Instantiate(sample, sample.transform.parent, false);
                    clone.slotIndex = SlotRules.BaseSlots + i;
                    clone.name = $"Bonus Gem Slot {i + 1}";
                    bonusSlots[i] = clone;
                    handles[i] = ChaosSlotHandle.Create(clone);
                }
                expanded.AddComponent<ChaosBonusLayout>().Initialize(button, nativeArc, bonusSlots, handles);
                return expanded;
            }
            finally
            {
                Object.Destroy(staging);
            }
        }

        private static void ForgetDestroyedGroups()
        {
            var destroyed = new List<UI_InGame_SkillButton_GemGroup>();
            foreach (var group in Changes.Keys)
                if (group == null)
                    destroyed.Add(group);
            foreach (var group in destroyed)
                Changes.Remove(group);
        }

        internal static void RestoreAll()
        {
            var edit = ManagerBase<EditSkillManager>.softInstance;
            foreach (var entry in Changes)
            {
                if (entry.Key == null)
                    continue;

                foreach (var added in entry.Value.AddedGroups)
                {
                    if (added == null)
                        continue;
                    if (edit != null && OnEditModeChanged != null)
                    {
                        foreach (var slotEdit in added.GetComponentsInChildren<UI_InGame_GemSlot_EditSkill>(true))
                        {
                            var handler = (Action<EditSkillManager.ModeType>)OnEditModeChanged.CreateDelegate(
                                typeof(Action<EditSkillManager.ModeType>), slotEdit);
                            edit.onModeChanged -= handler;
                        }
                    }
                    added.SetActive(false);
                    Object.Destroy(added);
                }

                entry.Key.groups = entry.Value.OriginalGroups;
                LastAppliedCount(entry.Key) = int.MinValue;
            }
            Changes.Clear();
        }
    }
}
