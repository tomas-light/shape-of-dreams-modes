using System;
using HarmonyLib;
using ShapeOfDreamsMods;

namespace ChaosSlots
{
    internal static class ChaosRewards
    {
        private const string RewardKey = "Shrine_CorruptedChaos_AddedEssenceSlot_";

        [HarmonyPatch(typeof(Shrine_CorruptedChaos), nameof(Shrine_CorruptedChaos.GetTargetInfo),
            new[] { typeof(DewPlayer), typeof(HeroSkillLocation), typeof(SkillTrigger) })]
        private static class TargetInfoPatch
        {
            private static bool Prefix(
                Shrine_CorruptedChaos __instance, DewPlayer player, HeroSkillLocation loc, ref EditSkillTargetInfo __result)
            {
                if (!ChaosSlotNetwork.IsEnabledFor(player) || !ChoosingSlotReward(__instance, player))
                    return true;

                __result = new EditSkillTargetInfo
                {
                    actionTypeRawText = DewLocalization.GetUIValue(RewardKey + "ActionVerb")
                };
                if (!CanAdd(player, loc))
                {
                    __result.rejectReasonRawText = GetRejectionText(loc);
                    return false;
                }

                int count = player.hero.Skill.GetMaxGemCount(loc);
                __result.tooltipRawText = string.Format("{0} {1}<sprite=0>{2}",
                    DewLocalization.GetUIValue(RewardKey + "Tooltip"), count, count + 1);
                return false;
            }
        }

        [HarmonyPatch(typeof(Shrine_CorruptedChaos), "OnActivateEditSkill",
            new[] { typeof(DewPlayer), typeof(HeroSkillLocation), typeof(SkillTrigger) })]
        private static class ActivatePatch
        {
            private static bool Prefix(
                Shrine_CorruptedChaos __instance, DewPlayer player, HeroSkillLocation loc, ref bool __result, out int? __state)
            {
                __state = null;
                if (!ChaosSlotNetwork.IsEnabledFor(player) || !ChoosingSlotReward(__instance, player))
                    return true;

                if (!CanAdd(player, loc))
                {
                    __result = false;
                    return false;
                }

                // The original still consumes the shrine and performs its normal feedback and state updates.
                __state = __instance.addedEssenceSlotMax;
                __instance.addedEssenceSlotMax = SlotRules.BaseSlots + SlotRules.BonusLimit;
                return true;
            }

            private static Exception Finalizer(Shrine_CorruptedChaos __instance, int? __state, Exception __exception)
            {
                if (__state.HasValue)
                    __instance.addedEssenceSlotMax = __state.Value;
                return __exception;
            }
        }

        [HarmonyPatch(typeof(Shrine_CorruptedChaos), nameof(Shrine_CorruptedChaos.GetEditSkillIndicatorRawText))]
        private static class EditIndicatorPatch
        {
            private static void Postfix(Shrine_CorruptedChaos __instance, ref string __result)
            {
                DewPlayer player = DewPlayer.local;
                if (!ChaosSlotState.IsLocalPlayer(player) || !ChoosingSlotReward(__instance, player))
                    return;

                int total = ChaosSlotState.Read(ChaosSlotState.FindBonus(player.hero)).Total;
                var culture = TooltipText.GetCulture(DewSave.profileMain?.language);
                __result = TooltipText.Format(culture, "ChooseMemoryWithSlotCount", total, SlotRules.BonusLimit);
            }
        }

        private static bool ChoosingSlotReward(Shrine_CorruptedChaos shrine, DewPlayer player) =>
            int.TryParse(shrine.GetCustomData(player), out int index)
            && shrine.rewards.TryGetValue(player.guid, out var rewards)
            && index >= 0 && index < rewards.Length
            && rewards[index] == CorruptedChaosRewardType.AddedEssenceSlot;

        private static bool CanAdd(DewPlayer player, HeroSkillLocation location) =>
            ChaosSlotState.TryMemorySlot(location, out var memory)
            && SlotRules.CanGrant(ChaosSlotState.Read(ChaosSlotState.FindBonus(player.hero)), memory);

        private static string GetRejectionText(HeroSkillLocation location)
        {
            var culture = TooltipText.GetCulture(DewSave.profileMain?.language);
            return TooltipText.Get(culture, ChaosSlotState.TryMemorySlot(location, out _) ? "SlotLimit" : "ChooseOrdinaryMemory");
        }
    }
}
