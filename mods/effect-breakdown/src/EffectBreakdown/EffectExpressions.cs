using System;
using System.Collections.Generic;
using System.Globalization;
using DewInternal;
using ShapeOfDreamsMods;

namespace EffectBreakdown
{
    [Flags]
    internal enum EffectKind
    {
        None = 0,
        Heal = 1,
        Barrier = 2,
        Damage = 4,
        CooldownSeconds = 8,
        CooldownRatio = 16,
        CooldownMultiplier = 32,
        GemCooldownSeconds = 64,
        SelfDamage = 128,
        SharedCooldownSeconds = CooldownSeconds | GemCooldownSeconds
    }

    internal sealed class EffectExpressions
    {
        private readonly IReadOnlyDictionary<string, EffectKind> _kinds;

        internal EffectExpressions(IReadOnlyDictionary<string, EffectKind> kinds) => _kinds = kinds;

        internal static string FormLabel(ExpressionData expression, CultureInfo culture)
        {
            foreach (var node in expression.nodes)
            {
                if (node.type != ExpressionChildNodeType.FieldName)
                    continue;
                if (node.value.StartsWith("Ai_Q_Laceration_Dash::", StringComparison.Ordinal))
                    return " · " + TooltipText.Get(culture, "RedForm");
                if (node.value.StartsWith("Ai_Q_Laceration_Circle::", StringComparison.Ordinal))
                    return " · " + TooltipText.Get(culture, "BlueForm");
            }
            return "";
        }

        internal static string CooldownReductionLabel(ExpressionData expression, CultureInfo culture)
        {
            foreach (var node in expression.nodes)
            {
                if (node.type != ExpressionChildNodeType.FieldName)
                    continue;
                switch (node.value)
                {
                    case "Ai_Q_Laceration_Circle::cooldownReductionPerHit":
                        return TooltipText.Get(culture, "ReductionPerEnemy");
                    case "Ai_Q_Laceration_Circle::cooldownReductionMax":
                        return TooltipText.Get(culture, "MaxReduction");
                }
            }

            string raw = expression.raw ?? "";
            if (raw.IndexOf("empower", StringComparison.OrdinalIgnoreCase) >= 0)
                return TooltipText.Get(culture, "ReductionEmpowered");
            if (raw.IndexOf("onCrit", StringComparison.OrdinalIgnoreCase) >= 0)
                return TooltipText.Get(culture, "ReductionOnCrit");
            return TooltipText.Get(culture, "ReductionOnTrigger");
        }

        internal EffectKind Classify(ExpressionData expression)
        {
            var result = EffectKind.None;
            foreach (var node in expression.nodes)
            {
                if (node.type == ExpressionChildNodeType.FieldName && _kinds.TryGetValue(node.value, out var kind))
                    result |= kind;
            }

            return result == EffectKind.Heal || result == EffectKind.Barrier || result == EffectKind.Damage ||
                result == EffectKind.CooldownSeconds || result == EffectKind.CooldownRatio ||
                result == EffectKind.CooldownMultiplier || result == EffectKind.GemCooldownSeconds ||
                result == EffectKind.SharedCooldownSeconds
                ? result
                : EffectKind.None;
        }
    }
}
