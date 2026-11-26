using System;
using System.Collections.Generic;
using System.Globalization;

namespace EffectBreakdown
{
    internal static class CooldownBreakdown
    {
        internal static void AddRows(List<TooltipRow> rows, SkillTrigger skill, int? currentLevel, int? previousLevel, CultureInfo culture)
        {
            if (skill == null)
                return;

            bool russian = culture.TwoLetterISOLanguageName == "ru";
            string label = russian ? "Перезарядка навыка" : "Ability cooldown";
            if (LobbyPreview.IsRendering)
            {
                AddLobbyRow(rows, skill, culture, label);
                return;
            }

            if (skill.owner == null || !skill.owner.isActive || skill.netIdentity == null ||
                !skill.currentConfig.isActive)
                return;

            int config = skill.currentConfigIndex;
            if (skill.type == SkillType.Ultimate && config == 0)
                return;

            if (currentLevel.HasValue)
            {
                if (previousLevel.HasValue && previousLevel != currentLevel)
                {
                    var previous = RenderLive(skill, 0, skill.GetCooldownTimeOnLevel(previousLevel.Value), culture, true,
                        label + (russian ? " · уровень " : " · level ") + previousLevel.Value);
                    if (previous.HasValue)
                        rows.Add(previous.Value);
                    label += (russian ? " · уровень " : " · level ") + currentLevel.Value;
                }

                var selected = RenderLive(skill, 0, skill.GetCooldownTimeOnLevel(currentLevel.Value), culture, true, label);
                if (selected.HasValue)
                    rows.Add(selected.Value);
            }
            else
            {
                var current = RenderLive(skill, config, skill.currentConfigMaxCooldownTime, culture, false, label);
                if (current.HasValue)
                    rows.Add(current.Value);
            }
        }

        private static TooltipRow? RenderLive(SkillTrigger skill, int config, float current,
            System.Globalization.CultureInfo culture, bool fromLevel, string label)
        {
            var trigger = skill.configs[config];
            float baseTime = trigger.cooldownTime;
            if (baseTime <= 0f || current < 0.01f)
                return null;

            bool canReceiveReduction = trigger.canReceiveCooldownReduction;
            float hastePoints = canReceiveReduction ? skill.owner.Status.abilityHaste : 0f;
            float haste = canReceiveReduction
                ? AbilityTrigger.GetCooldownTimeMultiplierByAbilityHaste(hastePoints)
                : 1f;
            float derived = skill.GetCooldownTimeMultiplier(config) / Math.Max(0.000001f, haste);
            float offset = skill.GetCooldownTimeOffset(config);

            bool russian = culture.TwoLetterISOLanguageName == "ru";
            string seconds = russian ? "с" : "s";
            var parts = new List<string>(3);
            if (canReceiveReduction && (Math.Abs(haste - 1f) > 0.000001f || Math.Abs(hastePoints) > 0.000001f))
                CooldownSources.AddHasteEntries(skill.owner, hastePoints, parts, culture);

            var named = CooldownSources.AddEntries(skill, canReceiveReduction, parts, culture, seconds);
            if (fromLevel && !canReceiveReduction)
                return null;
            if (fromLevel && baseTime + offset > 0.000001f)
                derived = current / (baseTime + offset) / Math.Max(0.000001f, haste);
            derived /= named.Multiplier;
            offset -= named.Offset;
            if (Math.Abs(derived - 1f) > 0.000001f)
                parts.Add(TooltipBreakdown.FormatModifier(new EffectModifier(
                    russian ? "Прочие множители" : "Other multipliers", derived), culture));
            if (Math.Abs(offset) > 0.000001f)
                parts.Add((russian ? "Поправка навыка " : "Skill offset ") +
                    TooltipBreakdown.FormatSigned(offset, culture) + " " + seconds);

            if (parts.Count == 0)
                return null;

            return CooldownRow(label, baseTime, current, parts, culture);
        }

        private static void AddLobbyRow(List<TooltipRow> rows, SkillTrigger skill, CultureInfo culture, string label)
        {
            if (skill.type == SkillType.Ultimate || skill.configs.Length == 0 ||
                !skill.configs[0].isActive || skill.configs[0].cooldownTime <= 0f)
                return;

            var offsets = new List<(string Name, float Seconds)>();
            var modifiers = LobbyPreview.CooldownModifiers(skill, skill.configs[0].canReceiveCooldownReduction, offsets);
            if (modifiers.Count == 0 && offsets.Count == 0)
                return;

            string seconds = culture.TwoLetterISOLanguageName == "ru" ? "с" : "s";
            float multiplier = TooltipBreakdown.Combine(modifiers);
            float totalOffset = 0f;
            var parts = new List<string>();
            foreach (var (name, amount) in offsets)
            {
                parts.Add(name + " " + TooltipBreakdown.FormatSigned(amount, culture) + " " + seconds);
                totalOffset += amount;
            }
            foreach (var modifier in modifiers)
                parts.Add(TooltipBreakdown.FormatModifier(modifier, culture));
            float baseTime = skill.configs[0].cooldownTime;
            float current = Math.Max(0f, (baseTime + totalOffset) * multiplier);
            rows.Add(CooldownRow(label, baseTime, current, parts, culture));
        }

        private static TooltipRow CooldownRow(string label, float baseline, float current, List<string> parts, CultureInfo culture)
        {
            string seconds = culture.TwoLetterISOLanguageName == "ru" ? " с" : " s";
            return new TooltipRow(label, baseline.ToString("0.##", culture) + seconds, current.ToString("0.##", culture) + seconds,
                parts, current > baseline + 0.000001f);
        }
    }
}
