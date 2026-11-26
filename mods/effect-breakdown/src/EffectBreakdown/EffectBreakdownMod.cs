using HarmonyLib;

namespace EffectBreakdown
{
    public sealed class EffectBreakdownMod : ModBehaviour
    {
        private void Awake()
        {
            DescriptionPatch.Initialize(typeof(LucidDream_GrievousWounds).Assembly.Location);
            harmony.PatchAll(typeof(EffectBreakdownMod).Assembly);
        }

        private void OnDestroy()
        {
            harmony.UnpatchAll(harmony.Id);
            CooldownSources.Clear();
        }
    }
}
