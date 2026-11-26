using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EffectBreakdown
{
    internal readonly struct EffectModifier
    {
        internal readonly string Name;
        internal readonly float Multiplier;
        internal readonly float? Haste;

        internal EffectModifier(string name, float multiplier, float? haste = null)
        {
            Name = name;
            Multiplier = multiplier;
            Haste = haste;
        }
    }

    internal readonly struct TooltipRow
    {
        internal readonly string Label;
        internal readonly string Baseline;
        internal readonly string Result;
        internal readonly IReadOnlyList<string> Sources;
        internal readonly bool Penalty;

        internal TooltipRow(string label, string baseline, string result, IReadOnlyList<string> sources, bool penalty)
        {
            Label = label;
            Baseline = baseline;
            Result = result;
            Sources = sources;
            Penalty = penalty;
        }
    }

    internal static class TooltipBreakdown
    {
        private static readonly Regex ValueColors =
            new Regex(@"</?(?:color|gradient)(?:=[^>]*)?>|<sprite=5>", RegexOptions.IgnoreCase);

        internal static float Combine(IReadOnlyList<EffectModifier> modifiers)
        {
            float multiplier = 1f;
            foreach (var modifier in modifiers)
                multiplier *= modifier.Multiplier;
            return multiplier;
        }

        internal static IEnumerable<EffectModifier> HasteModifiers(IReadOnlyList<(string Name, float Points)> stars,
            Func<float, float> cooldownFactor, float otherPoints = 0f, string otherName = "")
        {
            float points = otherPoints;
            float factor = cooldownFactor(points);
            if (Math.Abs(otherPoints) > 0.000001f)
                yield return new EffectModifier(otherName, factor, otherPoints);

            foreach (var star in stars)
            {
                points += star.Points;
                float next = cooldownFactor(points);
                yield return new EffectModifier(star.Name, next / factor, star.Points);
                factor = next;
            }
        }

        internal static string FormatModifier(EffectModifier modifier, CultureInfo culture)
        {
            string name = modifier.Name;
            string percent = FormatSigned(100f * (modifier.Multiplier - 1f), culture) + "%";
            if (!modifier.Haste.HasValue)
                return $"{name} {percent}";

            string points = FormatSigned(modifier.Haste.Value, culture);
            return culture.TwoLetterISOLanguageName == "ru"
                ? $"{name}: {points} к ускорению → {percent} ко времени перезарядки"
                : $"{name}: {points} haste → {percent} cooldown time";
        }

        internal static string FormatSigned(float amount, CultureInfo culture) =>
            (amount < 0f ? "−" : "+") + Math.Abs(amount).ToString("0.##", culture);

        internal static string Append(string description, List<TooltipRow> rows, CultureInfo culture)
        {
            if (rows.Count == 0)
                return description;

            var result = new StringBuilder(description.TrimEnd());
            bool russian = culture.TwoLetterISOLanguageName == "ru";
            result.Append("\n\n<b>").Append(russian
                ? "Расчёт с модификаторами" : "Modifier breakdown").Append("</b>");
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                totals.TryGetValue(row.Label, out int count);
                totals[row.Label] = count + 1;
            }
            foreach (var row in rows)
            {
                result.Append("\n<b>").Append(row.Label);
                if (totals[row.Label] > 1)
                {
                    occurrences.TryGetValue(row.Label, out int index);
                    occurrences[row.Label] = index + 1;
                    result.Append(russian ? " · эффект " : " · effect ").Append(index + 1);
                }
                result.Append("</b>  ")
                    .Append(ValueColors.Replace(row.Baseline, "").Trim()).Append(" → ");
                if (row.Penalty)
                    result.Append("<color=#ff6565>");
                result.Append(ValueColors.Replace(row.Result, "").Trim());
                if (row.Penalty)
                    result.Append("</color>");
                result.Append("\n<size=88%><color=#b9b5ad>")
                    .Append(string.Join(" · ", row.Sources)).Append("</color></size>");
            }
            return result.ToString();
        }
    }
}
