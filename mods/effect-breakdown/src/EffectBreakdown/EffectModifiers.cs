using System;
using System.Collections.Generic;
using ShapeOfDreamsMods;
using UnityEngine;

namespace EffectBreakdown
{
    internal static class EffectModifiers
    {
        internal static List<EffectModifier> ForEffect(Hero hero, SkillTrigger skill, EffectKind kind)
        {
            bool isHeal = kind == EffectKind.Heal;
            var modifiers = new List<EffectModifier>();
            if (kind == EffectKind.CooldownSeconds)
            {
                var dream = GetJellyfish(false);
                if (dream != null)
                    AddStatus(modifiers, nameof(LucidDream_PrudentJellyfish), dream.cooldownReductionStrength);
                return modifiers;
            }

            if (kind == EffectKind.Damage)
            {
                foreach (var star in hero.Status.GetStatusEffects<StarEffect>())
                    if (star is Se_Star_Cetus_L_MaxHpDamagePenalty cetus && cetus != null && cetus.isActive)
                        AddStar(modifiers, cetus, Reduction(cetus.damagePenaltyReduction));

                AddGemModifiers(modifiers, hero, skill, kind);
                return modifiers;
            }

            foreach (var star in hero.Status.GetStatusEffects<StarEffect>())
            {
                if (star == null || !star.isActive)
                    continue;

                AddStar(modifiers, star, StarMultiplier(star, isHeal, scaling => star.GetValue(scaling),
                    star is Se_Star_Aurena_L_HealAmpOnLowHp lowHealth && lowHealth.isLowHealth));
            }

            foreach (var status in hero.Status.GetStatusEffects<StatusEffect>())
            {
                if (status == null || !status.isActive)
                    continue;

                switch (status)
                {
                    case Se_RoomMod_ToxicArea_Debuff toxicArea:
                        AddStatus(modifiers, "RoomMod_ToxicArea", Reduction(isHeal ? toxicArea.healReduction : toxicArea.shieldReduction));
                        break;
                    case Se_Mon_Special_BossPolaris_CleansingFlame flame:
                        AddStatus(modifiers, nameof(Se_Mon_Special_BossPolaris_CleansingFlame),
                            Reduction(isHeal ? flame.healReduction : flame.shieldReduction));
                        break;
                    case Se_StarlessPath_PowerOfGuidance guidance when isHeal:
                        AddStatus(modifiers, nameof(Se_StarlessPath_PowerOfGuidance), 1f + guidance.healAmpPerStack * guidance.stack);
                        break;
                    case Se_Curse_DreamAfflictionHallucinatory curse when isHeal:
                        float multiplier = Reduction(curse.GetValue(curse.reductionRatios));
                        if (IsModifier(multiplier))
                            modifiers.Add(new EffectModifier(DewLocalization.GetCurseName("DreamAfflictionHallucinatory"), multiplier));
                        break;
                }
            }

            if (isHeal)
            {
                var manager = GameManager.softInstance;
                var difficulty = manager != null ? manager.difficulty : null;
                if (difficulty != null)
                    AddDifficulty(modifiers, difficulty.healRawMultiplier);
            }

            var actors = ActorManager.softInstance;
            if (actors != null)
            {
                foreach (var actor in actors.allActors)
                {
                    if (actor is LucidDream_GrievousWounds wounds && wounds != null && wounds.isActive)
                        AddStatus(modifiers, nameof(LucidDream_GrievousWounds), isHeal ? wounds.healMultiplier : wounds.shieldMultiplier);
                }
            }

            AddGemModifiers(modifiers, hero, skill, kind);
            return modifiers;
        }

        internal static LucidDream_PrudentJellyfish GetJellyfish(bool lobby)
        {
            if (lobby)
            {
                var settings = GameSettingsManager.softInstance;
                return settings != null && settings.activeLucidDreams.Contains(nameof(LucidDream_PrudentJellyfish))
                    ? DewResources.GetByShortTypeName<LucidDream_PrudentJellyfish>(
                        nameof(LucidDream_PrudentJellyfish), ResourceLoadSettings.Light)
                    : null;
            }

            var actors = ActorManager.softInstance;
            if (actors != null)
                foreach (var actor in actors.allActors)
                    if (actor is LucidDream_PrudentJellyfish dream && dream != null && dream.isActive)
                        return dream;
            return null;
        }

        private static void AddGemModifiers(List<EffectModifier> modifiers, Hero hero, SkillTrigger skill, EffectKind kind)
        {
            var actors = ActorManager.softInstance;
            if (skill == null || actors == null)
                return;

            foreach (var actor in actors.allActors)
            {
                if (!(actor is Gem gem) || gem == null || !gem.isActive || !gem.isValid || gem.skill != skill)
                    continue;

                var multiplier = GemMultiplier(gem, hero, kind);
                if (IsModifier(multiplier))
                    modifiers.Add(new EffectModifier(DewLocalization.GetGemName(gem), multiplier));
            }
        }

        private static float GemMultiplier(Gem gem, Hero hero, EffectKind kind)
        {
            switch (gem)
            {
                case Gem_C_Guidance guidance when kind == EffectKind.Heal:
                    return 1f + guidance.GetValue(guidance.healAmp);
                case Gem_C_Vengeance vengeance when kind != EffectKind.Barrier && vengeance.Network_isAmpActive:
                    return 1f + vengeance.GetValue(vengeance.ampAmount);
                case Gem_L_DivineFaith faith when kind != EffectKind.Barrier:
                    return 1f + faith.currentStack * faith.dmgAmpPerStack;
                case Gem_L_SuppressedArcanum arcanum when kind == EffectKind.Barrier && hero.normalizedHealth <= arcanum.hpThresholdRatio:
                    return 1f + arcanum.shieldAmpOnLowHp;
                default:
                    return 1f;
            }
        }

        internal static float StarMultiplier(StarEffect star, bool isHeal, Func<StarScalingValue, float> scaling, bool isLowHealth)
        {
            switch (star)
            {
                case Se_Star_L_ReceivedHealingAndShieldBonus skin:
                    return 1f + scaling(skin.ampAmount);
                case Se_Star_Nachia_L_HealAndShieldAmp nachia:
                    return 1f + scaling(nachia.healShieldAmpSelf);
                case Se_Star_Cetus_L_IncreaseDealtShield cetus when !isHeal:
                    return 1f + scaling(cetus.shieldBonusAmp);
                case Se_Star_Aurena_L_HealAmpOnLowHp lowHealth when isHeal && isLowHealth:
                    return 1f + scaling(lowHealth.healAmp);
                case Se_Star_Aurena_L_HealAmpWithPenalty aurena when isHeal:
                    return 1f + scaling(aurena.healAmp);
                case Se_Star_L_MaxHealthUpHealDown maxHealth when isHeal:
                    return Reduction(maxHealth.healReduction);
                case Se_Star_Husk_D_DamageOverTimeCritWithPenalty husk when isHeal:
                    return Reduction(husk.healReduction);
                default:
                    return 1f;
            }
        }

        internal static void AddStar(List<EffectModifier> modifiers, StarEffect star, float multiplier)
        {
            if (IsModifier(multiplier))
                modifiers.Add(new EffectModifier(DewLocalization.GetStarName(star.GetType()), multiplier));
        }

        internal static void AddBossDamageModifiers(Hero hero, List<EffectModifier> modifiers)
        {
            foreach (var star in hero.Status.GetStatusEffects<StarEffect>())
                if (star is Se_Star_D_BossDamageAmp boss && boss != null && boss.isActive)
                    AddStar(modifiers, boss, 1f + boss.GetValue(boss.damageToBossAmp));
        }

        internal static void AddDifficulty(List<EffectModifier> modifiers, float multiplier)
        {
            if (IsModifier(multiplier))
                modifiers.Add(new EffectModifier(TooltipText.Get(DescriptionPatch.TooltipCulture(), "Difficulty"), multiplier));
        }

        internal static void AddStatus(List<EffectModifier> modifiers, string typeName, float multiplier)
        {
            if (IsModifier(multiplier))
                modifiers.Add(new EffectModifier(DewLocalization.GetUIValue(typeName + "_Name"), multiplier));
        }

        internal static float Reduction(float amount) => 1f - Mathf.Clamp01(amount);

        private static bool IsModifier(float multiplier) =>
            multiplier >= 0f && !float.IsInfinity(multiplier) && Math.Abs(multiplier - 1f) > 0.000001f;

    }
}
