using System;
using System.Collections.Generic;
using System.Globalization;
using ShapeOfDreamsMods;

namespace EffectBreakdown
{
    internal static class CooldownBreakdown
    {
        internal static void AddRows(List<TooltipRow> rows, SkillTrigger skill, int? currentLevel, int? previousLevel, CultureInfo culture)
        {
            if (skill == null)
                return;

            string label = TooltipText.Get(culture, "AbilityCooldown");
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
                        label + " · " + TooltipText.Format(culture, "Level", previousLevel.Value));
                    if (previous.HasValue)
                        rows.Add(previous.Value);
                    label += " · " + TooltipText.Format(culture, "Level", currentLevel.Value);
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
            CultureInfo culture, bool fromLevel, string label)
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

            var parts = new List<string>(3);
            if (canReceiveReduction && (Math.Abs(haste - 1f) > 0.000001f || Math.Abs(hastePoints) > 0.000001f))
                CooldownSources.AddHasteEntries(skill.owner, hastePoints, parts, culture);

            var named = CooldownSources.AddEntries(skill, canReceiveReduction, parts, culture);
            if (fromLevel && !canReceiveReduction)
                return null;
            if (fromLevel && baseTime + offset > 0.000001f)
                derived = current / (baseTime + offset) / Math.Max(0.000001f, haste);
            derived /= named.Multiplier;
            offset -= named.Offset;
            if (Math.Abs(derived - 1f) > 0.000001f)
                parts.Add(TooltipBreakdown.FormatModifier(new EffectModifier(
                    TooltipText.Get(culture, "OtherMultipliers"), derived), culture));
            if (Math.Abs(offset) > 0.000001f)
                parts.Add(TooltipText.Get(culture, "SkillOffset") + " " +
                    TooltipText.Format(culture, "Seconds", TooltipBreakdown.FormatSigned(offset, culture)));

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

            float multiplier = TooltipBreakdown.Combine(modifiers);
            float totalOffset = 0f;
            var parts = new List<string>();
            foreach (var (name, amount) in offsets)
            {
                parts.Add(name + " " + TooltipText.Format(culture, "Seconds", TooltipBreakdown.FormatSigned(amount, culture)));
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
            return new TooltipRow(label, TooltipText.Format(culture, "Seconds", baseline.ToString("0.##", culture)),
                TooltipText.Format(culture, "Seconds", current.ToString("0.##", culture)), parts, current > baseline + 0.000001f);
        }
    }
}
