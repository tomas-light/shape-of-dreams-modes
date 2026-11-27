using HarmonyLib;

namespace EffectBreakdown
{
    public sealed class EffectBreakdownMod : ModBehaviour
    {
        private static EffectBreakdownMod _active;

        public EffectBreakdownConfig config = new EffectBreakdownConfig();

        internal static bool HideItemNotifications => _active != null && _active.config?.hideItemNotifications == true;

        private void Awake()
        {
            DescriptionPatch.Initialize(typeof(LucidDream_GrievousWounds).Assembly.Location);
            harmony.PatchAll(typeof(EffectBreakdownMod).Assembly);
            _active = this;
        }

        private void OnDestroy()
        {
            harmony.UnpatchAll(harmony.Id);
            if (_active == this)
                _active = null;
            CooldownSources.Clear();
        }
    }
}
