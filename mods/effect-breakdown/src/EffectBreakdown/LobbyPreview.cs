using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace EffectBreakdown
{
    [HarmonyPatch]
    internal static class LobbyPreview
    {
        [ThreadStatic]
        internal static bool IsRendering;
        [ThreadStatic]
        private static HeroSkillLocation? _loadoutSlot;
        [ThreadStatic]
        private static HeroSkillLocation? _previewSlot;
        private static readonly ConditionalWeakTable<IReadOnlyList<object>, LoadoutTooltip> LoadoutTooltips =
            new ConditionalWeakTable<IReadOnlyList<object>, LoadoutTooltip>();

        private sealed class LoadoutTooltip
        {
            internal readonly HeroSkillLocation Slot;
            internal LoadoutTooltip(HeroSkillLocation slot) => Slot = slot;
        }

        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(UI_Tooltip_SkillDescription), "OnSetup");
            yield return AccessTools.Method(typeof(UI_Tooltip_GemDescription), "DoLobbyDejavuTooltip");
        }

        private static void Prefix(UI_Tooltip_BaseObj __instance, out (bool Rendering, HeroSkillLocation? Slot) __state)
        {
            __state = (IsRendering, _previewSlot);
            _previewSlot = null;
            var objects = __instance.currentObjects;
            if (__instance is UI_Tooltip_SkillDescription && objects?.Count == 1 &&
                __instance.currentObject is SkillTrigger)
            {
                // The tooltip section periodically rebuilds its text using the same object list.
                if (_loadoutSlot.HasValue)
                    LoadoutTooltips.GetValue(objects, _ => new LoadoutTooltip(_loadoutSlot.Value));
                if (LoadoutTooltips.TryGetValue(objects, out var tooltip))
                    _previewSlot = tooltip.Slot;
            }
            IsRendering = _previewSlot.HasValue || IsDejavu(__instance);
        }

        private static Exception Finalizer((bool Rendering, HeroSkillLocation? Slot) __state, Exception __exception)
        {
            (IsRendering, _previewSlot) = __state;
            return __exception;
        }

        internal static bool IsDejavu(UI_Tooltip_BaseObj tooltip) =>
            tooltip.currentObjects?.Count >= 2 && tooltip.currentObjects[1] is string id && id == "Dejavu";

        [HarmonyPatch(typeof(UI_TooltipManager), nameof(UI_TooltipManager.Hide))]
        private static class ClearLoadoutTooltip
        {
            private static void Prefix(UI_TooltipManager __instance) => LoadoutTooltips.Remove(__instance.currentObjects);
        }

        [HarmonyPatch]
        private static class LoadoutTooltipScope
        {
            private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(UI_Lobby_Loadout_SkillSlot), "ShowTooltip");
                yield return AccessTools.Method(typeof(UI_Lobby_Loadout_AvailableSkills_Item), "ShowTooltip");
                yield return AccessTools.Method(typeof(UI_Lobby_Constellations_Skills_Slot), "ShowTooltip");
                yield return AccessTools.Method(typeof(UI_Lobby_Constellations_Skills_ContextMenu_Item), "ShowTooltip");
            }

            private static void Prefix(UnityEngine.Component __instance, out HeroSkillLocation? __state)
            {
                __state = _loadoutSlot;
                switch (__instance)
                {
                    case UI_Lobby_Loadout_SkillSlot slot:
                        _loadoutSlot = slot.type;
                        break;
                    case UI_Lobby_Loadout_AvailableSkills_Item item:
                        _loadoutSlot = item.GetComponentInParent<UI_Lobby_Loadout_AvailableSkills>()?.type;
                        break;
                    case UI_Lobby_Constellations_Skills_Slot constellationSlot:
                        _loadoutSlot = constellationSlot.type;
                        break;
                    case UI_Lobby_Constellations_Skills_ContextMenu_Item item:
                        _loadoutSlot = item.GetComponentInParent<UI_Lobby_Constellations_Skills_ContextMenu>()?.type;
                        break;
                }
            }

            private static Exception Finalizer(HeroSkillLocation? __state, Exception __exception)
            {
                _loadoutSlot = __state;
                return __exception;
            }
        }

        internal static List<EffectModifier> Modifiers(EffectKind kind)
        {
            var modifiers = new List<EffectModifier>();
            var player = DewPlayer.local;
            if (player == null || GameSettingsManager.softInstance == null)
                return modifiers;

            if (kind == EffectKind.Damage)
            {
                foreach (var (star, _, _) in EquippedStars(player.selectedLoadout))
                    if (star is Se_Star_Cetus_L_MaxHpDamagePenalty cetus)
                        EffectModifiers.AddStar(modifiers, cetus, EffectModifiers.Reduction(cetus.damagePenaltyReduction));
            }
            else if (kind == EffectKind.Heal || kind == EffectKind.Barrier)
            {
                foreach (var (star, level, strength) in EquippedStars(player.selectedLoadout))
                {
                    var multiplier = EffectModifiers.StarMultiplier(star, kind == EffectKind.Heal,
                        scaling => scaling.GetValue(level, strength), false);
                    EffectModifiers.AddStar(modifiers, star, multiplier);
                }
            }

            var settings = GameSettingsManager.softInstance;
            if (kind == EffectKind.Heal && !string.IsNullOrEmpty(settings.difficulty))
            {
                var difficulty = DewResources.GetByName<DewDifficultySettings>(settings.difficulty, ResourceLoadSettings.Light);
                if (difficulty != null)
                    EffectModifiers.AddDifficulty(modifiers, difficulty.healRawMultiplier);
            }

            if ((kind == EffectKind.Heal || kind == EffectKind.Barrier) &&
                settings.activeLucidDreams.Contains(nameof(LucidDream_GrievousWounds)))
            {
                var wounds = DewResources.GetByShortTypeName<LucidDream_GrievousWounds>(
                    nameof(LucidDream_GrievousWounds), ResourceLoadSettings.Light);
                if (wounds != null)
                    EffectModifiers.AddStatus(modifiers, nameof(LucidDream_GrievousWounds),
                        kind == EffectKind.Heal ? wounds.healMultiplier : wounds.shieldMultiplier);
            }

            if (kind == EffectKind.CooldownSeconds)
            {
                var dream = EffectModifiers.GetJellyfish(true);
                if (dream != null)
                    EffectModifiers.AddStatus(modifiers, nameof(LucidDream_PrudentJellyfish), dream.cooldownReductionStrength);
            }

            return modifiers;
        }

        internal static void AddBossDamageModifiers(List<EffectModifier> modifiers)
        {
            var player = DewPlayer.local;
            if (player == null)
                return;

            foreach (var (star, level, strength) in EquippedStars(player.selectedLoadout))
                if (star is Se_Star_D_BossDamageAmp boss)
                    EffectModifiers.AddStar(modifiers, boss, 1f + boss.damageToBossAmp.GetValue(level, strength));
        }

        internal static bool AppliesTranquilityToEssences(SkillTrigger skill)
        {
            var player = DewPlayer.local;
            if (player == null || skill == null)
                return false;

            foreach (var (star, _, _) in EquippedStars(player.selectedLoadout))
                if (star is Se_Star_Yubar_F_TQ_EssenceCooldown && star.IsSkillRelated(skill))
                    return true;
            return false;
        }

        internal static List<EffectModifier> CooldownModifiers(SkillTrigger skill, bool canReceiveReduction,
            List<(string Name, float Seconds)> offsets)
        {
            var modifiers = new List<EffectModifier>();
            var player = DewPlayer.local;
            if (player == null)
                return modifiers;

            foreach (var (star, level, strength) in EquippedStars(player.selectedLoadout))
            {
                if (!CooldownStarApplies(star, skill, _previewSlot))
                    continue;

                var (multiplier, offset) = CooldownStar(star, scaling => scaling.GetValue(level, strength), canReceiveReduction);
                EffectModifiers.AddStar(modifiers, star, multiplier);
                if (Math.Abs(offset) > 0.000001f)
                    offsets.Add((DewLocalization.GetStarName(star.GetType()), offset));
            }
            if (canReceiveReduction)
                AddHasteModifiers(player.selectedLoadout, modifiers);
            return modifiers;
        }

        internal static bool CooldownStarApplies(StarEffect star, SkillTrigger skill, HeroSkillLocation? previewSlot = null)
        {
            if (!(star is Se_Star_Husk_D_MovementSkillHaste))
                return star.IsSkillRelated(skill);

            var location = previewSlot ?? skill.skillType;
            return (location == HeroSkillLocation.Q || location == HeroSkillLocation.W ||
                    location == HeroSkillLocation.E || location == HeroSkillLocation.R) &&
                   (skill.configs[0].selfValidator.isMovementAbility || (skill.tags & DescriptionTags.Mobility) != 0);
        }

        internal static (float Multiplier, float Offset) CooldownStar(StarEffect star,
            Func<StarScalingValue, float> scale, bool canReceiveReduction)
        {
            switch (star)
            {
                case Se_Star_L_DodgeCooldown dodge:
                    return (1f - scale(dodge.reductionAmount), 0f);
                case Se_Star_Yubar_L_DodgeChargeWithLongerCooldown dodge:
                    return (1f, scale(dodge.cooldownTimePenalty));
                case Se_Star_Mist_L_DodgeCooldownWithPenalty dodge:
                    return (1f, -scale(dodge.reducedCooldown));
                case Se_Star_Yubar_F_TQ_LowerCooldownNoInvul tranquility when canReceiveReduction:
                    return (1f, -tranquility.cooldownReduction);
                case Se_Star_Yubar_F_TQ_EssenceCooldown tranquility when canReceiveReduction:
                    return (1f, tranquility.cooldownPenalty);
                case Se_Star_Yubar_F_TQ_ReducedCooldownAndDuration tranquility when canReceiveReduction:
                    return (1f, -tranquility.cooldownReduction);
                case Se_Star_Aurena_F_RE_CooldownButNoHeal aurena when canReceiveReduction:
                    return (1f - aurena.cooldownReduction, 0f);
                case Se_Star_Bismuth_F_IN_LessCooldownLessMissiles bismuth when canReceiveReduction:
                    return (1f - bismuth.cooldownReduction, 0f);
                case Se_Star_Nachia_F_NW_LessCooldownLessAtkSpd nachia when canReceiveReduction:
                    return (1f - nachia.cooldownReduction, 0f);
                case Se_Star_Husk_D_MovementSkillHaste husk when canReceiveReduction:
                    return (1f - scale(husk.cooldownReduction), 0f);
                default:
                    return (1f, 0f);
            }
        }

        private static void AddHasteModifiers(HeroLoadoutData loadout, List<EffectModifier> modifiers)
        {
            var dream = EffectModifiers.GetJellyfish(true);
            var defaults = UnityEngine.Resources.Load<DewGameplayExperienceSettings>("GSS/GSS - Default");
            float floor = dream != null ? dream.cooldownFloorRatioByAbilityHaste
                : defaults != null ? defaults.cooldownFloorRatioByAbilityHaste : 0f;

            var stars = new List<(string Name, float Points)>();
            foreach (var (star, level, strength) in EquippedStars(loadout))
            {
                float added = CooldownSources.PermanentStarHaste(star, scaling => scaling.GetValue(level, strength));
                if (added != 0f)
                    stars.Add((DewLocalization.GetStarName(star.GetType()), added));
            }
            modifiers.AddRange(TooltipBreakdown.HasteModifiers(stars,
                points => UnityEngine.Mathf.Lerp(floor, 1f, 1f / (1f + 0.01f * points))));
        }

        private static readonly StarType[] Slots = { StarType.Destruction, StarType.Life, StarType.Imagination, StarType.Flexible };

        private static IEnumerable<(StarEffect Star, int Level, float Strength)> EquippedStars(HeroLoadoutData loadout)
        {
            foreach (var slot in Slots)
            {
                var stars = loadout.GetStarList(slot);
                if (stars == null)
                    continue;

                foreach (var entry in stars)
                {
                    if (string.IsNullOrEmpty(entry.name) || entry.level <= 0)
                        continue;

                    var star = DewResources.GetByShortTypeName<StarEffect>(entry.name, ResourceLoadSettings.Light);
                    if (star == null)
                        continue;

                    float strength = slot == star.type || slot == StarType.Flexible || star.type == StarType.Flexible ? 1f : 0.5f;
                    yield return (star, Math.Min(entry.level, Math.Max(1, star.maxStarLevel)), strength);
                }
            }
        }
    }
}
