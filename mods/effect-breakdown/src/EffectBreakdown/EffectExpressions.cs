using System;
using System.Collections.Generic;
using DewInternal;

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

        internal static string FormLabel(ExpressionData expression, bool russian)
        {
            foreach (var node in expression.nodes)
            {
                if (node.type != ExpressionChildNodeType.FieldName)
                    continue;
                if (node.value.StartsWith("Ai_Q_Laceration_Dash::", StringComparison.Ordinal))
                    return russian ? " · красная форма" : " · red form";
                if (node.value.StartsWith("Ai_Q_Laceration_Circle::", StringComparison.Ordinal))
                    return russian ? " · синяя форма" : " · blue form";
            }
            return "";
        }

        internal static string TriggerLabel(ExpressionData expression, bool russian)
        {
            string raw = expression.raw ?? "";
            if (raw.IndexOf("empower", StringComparison.OrdinalIgnoreCase) >= 0)
                return russian ? "при усилении" : "on empowered cast";
            if (raw.IndexOf("onCrit", StringComparison.OrdinalIgnoreCase) >= 0)
                return russian ? "при критическом ударе" : "on critical hit";
            return russian ? "при срабатывании" : "on trigger";
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
