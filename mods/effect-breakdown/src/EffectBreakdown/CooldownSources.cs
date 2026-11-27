using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using ShapeOfDreamsMods;

namespace EffectBreakdown
{
    // The game keeps per-source bonuses on the host and synchronizes their aggregate.
    // Retain source names on registration without changing the bonus or its lifetime.
    [HarmonyPatch]
    internal static class CooldownSources
    {
        private static ConditionalWeakTable<SkillBonus, Source> _sources = new ConditionalWeakTable<SkillBonus, Source>();
        private static ConditionalWeakTable<SkillTrigger, List<SkillBonus>> _bonuses =
            new ConditionalWeakTable<SkillTrigger, List<SkillBonus>>();
        private static ConditionalWeakTable<EntityStatus, Dictionary<StarEffect, float>> _conditionalHaste =
            new ConditionalWeakTable<EntityStatus, Dictionary<StarEffect, float>>();
        private static readonly FieldInfo StackingHasteBonus =
            AccessTools.Field(typeof(Se_Star_Cetus_D_StackStatsOnTakenDamage), "_bonus");
        private static readonly FieldInfo ShieldedHasteBonus =
            AccessTools.Field(typeof(Se_Star_Cetus_D_StatsUpWhileShielded), "_bonus");

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StarEffect), nameof(StarEffect.DoSkillBonusAll))]
        private static void NameStarBonus(StarEffect __instance, SkillBonus __0) => Remember(__0, __instance.GetType());

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SkillBonus), nameof(SkillBonus.Clone))]
        private static void NameClone(SkillBonus __instance, SkillBonus __result)
        {
            if (_sources.TryGetValue(__instance, out var source))
                Remember(__result, source.Type);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SkillTrigger), nameof(SkillTrigger.AddSkillBonus))]
        private static void RecordSkillBonus(SkillTrigger __instance, SkillBonus __result)
        {
            if (__result == null || __result.parent != __instance)
                return;

            if (!_sources.TryGetValue(__result, out _))
            {
                foreach (var frame in new StackTrace().GetFrames() ?? Array.Empty<StackFrame>())
                {
                    var type = frame.GetMethod()?.DeclaringType;
                    while (type?.DeclaringType != null)
                        type = type.DeclaringType;
                    if (type == null || !IsNamedSource(type))
                        continue;
                    Remember(__result, type);
                    break;
                }
            }
            _bonuses.GetOrCreateValue(__instance).Add(__result);
        }

        internal static float PermanentStarHaste(StarEffect star, Func<StarScalingValue, float> scale)
        {
            switch (star)
            {
                case Se_Star_Bismuth_D_SkillHasteAndAutoCast bismuth:
                    return scale(bismuth.bonusAmount);
                case Se_Star_Nachia_D_SkillHaste nachia:
                    return scale(nachia.bonusAmount);
                case Se_Star_Yubar_D_SkillHaste yubar:
                    return scale(yubar.bonusAmount);
                default:
                    return 0f;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(EntityStatus), nameof(EntityStatus.CalculateStats))]
        private static void CaptureConditionalHaste(EntityStatus __instance)
        {
            if (!__instance.isServer || !(__instance.entity is Hero))
                return;

            if (_conditionalHaste.TryGetValue(__instance, out var applied))
                applied.Clear();

            foreach (var star in __instance.GetStatusEffects<StarEffect>())
            {
                if (star == null || !star.isActive)
                    continue;

                FieldInfo field = star is Se_Star_Cetus_D_StackStatsOnTakenDamage ? StackingHasteBonus
                    : star is Se_Star_Cetus_D_StatsUpWhileShielded ? ShieldedHasteBonus : null;
                if (field?.GetValue(star) is StatBonus bonus && bonus.abilityHasteFlat != 0f)
                {
                    applied ??= _conditionalHaste.GetOrCreateValue(__instance);
                    applied[star] = bonus.abilityHasteFlat;
                }
            }
        }

        internal static void AddHasteEntries(Hero hero, float total, List<string> parts, CultureInfo culture)
        {
            float accounted = 0f;
            var stars = new List<(string Name, float Points)>();
            // Conditional StatBonus values are host-only; clients use the synchronized aggregate.
            _conditionalHaste.TryGetValue(hero.Status, out var applied);
            foreach (var star in hero.Status.GetStatusEffects<StarEffect>())
            {
                if (star == null || !star.isActive)
                    continue;

                float points = applied != null && applied.TryGetValue(star, out float conditional)
                    ? conditional : PermanentStarHaste(star, value => star.GetValue(value));
                if (Math.Abs(points) <= 0.000001f)
                    continue;

                accounted += points;
                stars.Add((DewLocalization.GetStarName(star.GetType()), points));
            }

            string otherName = TooltipText.Get(culture, stars.Count == 0 ? "AbilityHaste" : "OtherHaste");
            foreach (var modifier in TooltipBreakdown.HasteModifiers(stars,
                AbilityTrigger.GetCooldownTimeMultiplierByAbilityHaste, total - accounted, otherName))
                parts.Add(TooltipBreakdown.FormatModifier(modifier, culture));
        }

        internal static (float Multiplier, float Offset) AddEntries(SkillTrigger skill, bool canReceiveReduction,
            List<string> parts, CultureInfo culture)
        {
            float multiplier = 1f;
            float offset = 0f;
            if (!_bonuses.TryGetValue(skill, out var list))
                return skill.isServer ? (multiplier, offset) : AddReplicatedEntries(skill, canReceiveReduction, parts, culture);

            list.RemoveAll(bonus => bonus == null || bonus.parent != skill);
            foreach (var bonus in list)
            {
                if ((!canReceiveReduction && !bonus.ignoreReceiveCooldownReductionFlag) ||
                    !_sources.TryGetValue(bonus, out var source))
                    continue;

                string name = source.Name;
                if (Math.Abs(bonus.cooldownMultiplier - 1f) > 0.000001f && bonus.cooldownMultiplier > 0.000001f)
                {
                    parts.Add(TooltipBreakdown.FormatModifier(new EffectModifier(name, bonus.cooldownMultiplier), culture));
                    multiplier *= bonus.cooldownMultiplier;
                }
                if (Math.Abs(bonus.cooldownOffset) > 0.000001f)
                {
                    parts.Add(name + " " + TooltipText.Format(culture, "Seconds", TooltipBreakdown.FormatSigned(bonus.cooldownOffset, culture)));
                    offset += bonus.cooldownOffset;
                }
            }
            return (multiplier, offset);
        }

        private static (float Multiplier, float Offset) AddReplicatedEntries(SkillTrigger skill, bool canReceiveReduction,
            List<string> parts, CultureInfo culture)
        {
            float multiplier = 1f;
            float offset = 0f;
            var hero = skill.owner;
            foreach (var star in hero.Status.GetStatusEffects<StarEffect>())
            {
                if (star == null || !star.isActive || !LobbyPreview.CooldownStarApplies(star, skill))
                    continue;
                // Conditional host-only effects remain in the synchronized residual.
                var (factor, adjustment) = LobbyPreview.CooldownStar(star, value => star.GetValue(value), canReceiveReduction);
                string name = DewLocalization.GetStarName(star.GetType());
                if (Math.Abs(factor - 1f) > 0.000001f)
                {
                    parts.Add(TooltipBreakdown.FormatModifier(new EffectModifier(name, factor), culture));
                    multiplier *= factor;
                }
                if (Math.Abs(adjustment) > 0.000001f)
                {
                    parts.Add(name + " " + TooltipText.Format(culture, "Seconds", TooltipBreakdown.FormatSigned(adjustment, culture)));
                    offset += adjustment;
                }
            }

            var actors = ActorManager.softInstance;
            if (canReceiveReduction && actors != null)
                foreach (var actor in actors.allActors)
                    if (actor is Gem_C_Efficiency gem && gem != null && gem.isActive && gem.isValid && gem.skill == skill)
                    {
                        float factor = 1f / (1f + gem.GetValue(gem.skillHaste) * 0.01f);
                        if (Math.Abs(factor - 1f) > 0.000001f)
                        {
                            parts.Add(TooltipBreakdown.FormatModifier(new EffectModifier(DewLocalization.GetGemName(gem), factor), culture));
                            multiplier *= factor;
                        }
                    }
            return (multiplier, offset);
        }

        private static bool IsNamedSource(Type type) =>
            (type != typeof(StarEffect) && typeof(StarEffect).IsAssignableFrom(type)) ||
            (type != typeof(Gem) && typeof(Gem).IsAssignableFrom(type));

        private static void Remember(SkillBonus bonus, Type type)
        {
            if (bonus != null)
                _sources.GetValue(bonus, _ => new Source(type));
        }

        internal static void Clear()
        {
            _sources = new ConditionalWeakTable<SkillBonus, Source>();
            _bonuses = new ConditionalWeakTable<SkillTrigger, List<SkillBonus>>();
            _conditionalHaste = new ConditionalWeakTable<EntityStatus, Dictionary<StarEffect, float>>();
        }

        private sealed class Source
        {
            internal readonly Type Type;
            internal Source(Type type) => Type = type;

            internal string Name => typeof(StarEffect).IsAssignableFrom(Type)
                ? DewLocalization.GetStarName(Type)
                : DewLocalization.GetGemName(DewLocalization.GetGemKey(Type));
        }
    }
}
