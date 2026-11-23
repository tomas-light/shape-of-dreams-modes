using HarmonyLib;

namespace ChaosSlots
{
    [HarmonyPatch(typeof(EditSkillManager), nameof(EditSkillManager.IsSlotHighlighted), new[] { typeof(HeroSkillLocation) })]
    internal static class ChaosSlotHighlight
    {
        private static void Postfix(HeroSkillLocation skillLoc, ref bool __result)
        {
            if (ChaosSlotHandle.TryGetDragOrigin(out var origin))
                __result = ChaosSlotState.CanMove(DewPlayer.local, origin, skillLoc);
        }
    }
}
