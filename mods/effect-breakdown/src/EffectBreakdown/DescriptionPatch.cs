using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DewInternal;
using HarmonyLib;
using ShapeOfDreamsMods;

namespace EffectBreakdown
{
    [HarmonyPatch(typeof(DewLocalization), nameof(DewLocalization.ConvertDescriptionNodesToText))]
    internal static class DescriptionPatch
    {
        private static EffectExpressions _expressions;
        [System.ThreadStatic]
        private static Layout _layout;

        private sealed class Layout
        {
            internal readonly IReadOnlyList<LocaleNode> Nodes;
            internal readonly List<TooltipRow> Rows = new List<TooltipRow>();
            internal readonly CultureInfo Culture = TooltipCulture();

            internal Layout(IReadOnlyList<LocaleNode> nodes) => Nodes = nodes;
        }

        private static void Prefix(IReadOnlyList<LocaleNode> nodes, DewLocalization.DescriptionSettings settings, out Layout __state)
        {
            __state = _layout;
            _layout = settings.capturedFields == null && (settings.contextObject is SkillTrigger || settings.contextObject is Gem)
                ? new Layout(nodes) : null;
            if (_layout != null && settings.contextObject is SkillTrigger skill)
                CooldownBreakdown.AddRows(_layout.Rows, skill, settings.currentLevel, settings.previousLevel, _layout.Culture);
        }

        private static void Postfix(ref string __result)
        {
            if (_layout != null)
                __result = TooltipBreakdown.Append(__result, _layout.Rows, _layout.Culture);
        }

        private static System.Exception Finalizer(Layout __state, System.Exception __exception)
        {
            _layout = __state;
            return __exception;
        }

        internal static void Initialize(string contentsPath) =>
            _expressions = new EffectExpressions(EffectDiscovery.Read(contentsPath,
                message => UnityEngine.Debug.LogWarning("[Effect Breakdown] " + message)));

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = AccessTools.Method(typeof(DewLocalization), nameof(DewLocalization.EvaluateAndRenderExpression));
            var replacement = AccessTools.Method(typeof(DescriptionPatch), nameof(Render));
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(original))
                    instruction.operand = replacement;
                yield return instruction;
            }
        }

        internal static string Render(ExpressionData expression, DewLocalization.DescriptionSettings settings,
            bool showDetails)
        {
            string original = DewLocalization.EvaluateAndRenderExpression(expression, settings, showDetails);
            if (_layout == null)
                return original;

            var kind = _expressions.Classify(expression);
            var hero = settings.contextEntity as Hero;
            if (kind == EffectKind.None || (hero == null || !hero.isActive) && !LobbyPreview.IsRendering)
                return original;

            var skill = settings.contextObject as SkillTrigger;
            var culture = _layout.Culture;
            string label = EffectLabel(expression, kind, culture);
            string form = EffectExpressions.FormLabel(expression, culture);
            if (kind == EffectKind.CooldownRatio || kind == EffectKind.CooldownMultiplier ||
                kind == EffectKind.GemCooldownSeconds)
            {
                AddUnchangedRow(label + form, original, culture);
                return original;
            }

            bool shared = kind == EffectKind.SharedCooldownSeconds;
            if (shared)
                kind = EffectKind.CooldownSeconds;
            var modifiers = LobbyPreview.IsRendering
                ? LobbyPreview.Modifiers(kind)
                : EffectModifiers.ForEffect(hero, skill, kind);
            AddEffectRow(label + form, original, expression, settings, showDetails, modifiers, culture);

            if (shared && modifiers.Count != 0 && AppliesReductionToEssences(hero, skill))
                AddUnchangedRow(TooltipText.Get(culture, "EssenceReduction"), original, culture);

            if (kind == EffectKind.Damage)
            {
                var boss = new List<EffectModifier>(modifiers);
                if (LobbyPreview.IsRendering)
                    LobbyPreview.AddBossDamageModifiers(boss);
                else
                    EffectModifiers.AddBossDamageModifiers(hero, boss);
                if (boss.Count > modifiers.Count)
                    AddEffectRow(TooltipText.Format(culture, "AgainstBoss", label) + form, original, expression,
                        settings, showDetails, boss, culture);
            }

            return original;
        }

        private static readonly string[] DamageElements = { "Dark", "Light", "Cold", "Fire" };

        private static string EffectLabel(ExpressionData expression, EffectKind kind, CultureInfo culture)
        {
            switch (kind)
            {
                case EffectKind.Damage:
                    var (before, after) = AdjacentText(expression);
                    foreach (string element in DamageElements)
                        if (TooltipText.Matches(culture, element + "Token", after))
                            return TooltipText.Get(culture, element + "Damage");
                    foreach (string element in DamageElements)
                        if (TooltipText.Matches(culture, element + "Token", before))
                            return TooltipText.Get(culture, element + "Damage");
                    return TooltipText.Get(culture, "Damage");
                case EffectKind.Heal:
                    var (healBefore, healAfter) = AdjacentText(expression);
                    return TooltipText.Get(culture, TooltipText.Matches(culture, "DamageDealtToken", healAfter) ||
                        TooltipText.Matches(culture, "DamageDealtToken", healBefore) ? "HealFromDamage" : "SelfHeal");
                case EffectKind.Barrier:
                    return TooltipText.Get(culture, "SelfBarrier");
                case EffectKind.CooldownMultiplier:
                    return TooltipText.Get(culture, "CooldownMultiplier");
                case EffectKind.GemCooldownSeconds:
                    return TooltipText.Get(culture, "EssenceReduction");
                case EffectKind.SharedCooldownSeconds:
                    return TooltipText.Get(culture, "MemoryReduction");
                default:
                    return EffectExpressions.CooldownReductionLabel(expression, culture);
            }
        }

        private static readonly char[] SentenceEnds = { '.', '!', '?', '\n', '。', '！', '？' };

        private static (string Before, string After) AdjacentText(ExpressionData expression)
        {
            var before = new StringBuilder();
            var after = new StringBuilder();
            bool found = false;
            foreach (var node in _layout.Nodes)
            {
                if (node.type == LocaleNodeType.Expression)
                {
                    if (found)
                        break;
                    found = ReferenceEquals(node.expressionData, expression);
                    if (!found)
                        before.Clear();
                }
                else if (node.type == LocaleNodeType.Text)
                {
                    string part = node.textData ?? "";
                    if (found)
                    {
                        int stop = part.IndexOfAny(SentenceEnds);
                        after.Append(stop == -1 ? part : part.Substring(0, stop));
                        if (stop != -1 || after.Length >= 90)
                            break;
                    }
                    else
                    {
                        int previousStop = part.LastIndexOfAny(SentenceEnds);
                        if (previousStop != -1)
                            before.Clear();
                        before.Append(part.Substring(previousStop + 1));
                    }
                }
            }
            return found ? (before.ToString(Math.Max(0, before.Length - 90), Math.Min(90, before.Length)), after.ToString())
                : ("", "");
        }

        private static bool AppliesReductionToEssences(Hero hero, SkillTrigger skill)
        {
            if (LobbyPreview.IsRendering)
                return LobbyPreview.AppliesTranquilityToEssences(skill);
            if (hero == null || skill == null)
                return false;

            foreach (var star in hero.Status.GetStatusEffects<Se_Star_Yubar_F_TQ_EssenceCooldown>())
                if (star != null && star.isActive && star.IsSkillRelated(skill))
                    return true;
            return false;
        }

        private static void AddUnchangedRow(string label, string value, CultureInfo culture)
        {
            if (EffectModifiers.GetJellyfish(LobbyPreview.IsRendering) == null)
                return;
            string name = DewLocalization.GetUIValue(nameof(LucidDream_PrudentJellyfish) + "_Name");
            string source = name + " · " + TooltipText.Get(culture, "NoEffect");
            _layout.Rows.Add(new TooltipRow(label, value, value, new[] { source }, false));
        }

        private static void AddEffectRow(string label, string baseline, ExpressionData expression,
            DewLocalization.DescriptionSettings settings, bool showDetails, List<EffectModifier> modifiers, CultureInfo culture)
        {
            if (modifiers.Count == 0)
                return;

            float multiplier = TooltipBreakdown.Combine(modifiers);
            var adjusted = Scale(expression, multiplier);
            string value = DewLocalization.EvaluateAndRenderExpression(adjusted, settings, showDetails);
            if (string.IsNullOrWhiteSpace(value))
                return;

            var sources = new List<string>(modifiers.Count);
            foreach (var modifier in modifiers)
                sources.Add(TooltipBreakdown.FormatModifier(modifier, culture));
            _layout.Rows.Add(new TooltipRow(label, baseline, value, sources, multiplier < 1f - 0.000001f));
        }
        internal static ExpressionData Scale(ExpressionData expression, float multiplier)
        {
            var nodes = new List<ExpressionChildNode>(expression.nodes.Count + 2)
            {
                new ExpressionChildNode { type = ExpressionChildNodeType.Expression, value = "(" }
            };
            nodes.AddRange(expression.nodes);
            nodes.Add(new ExpressionChildNode
            {
                type = ExpressionChildNodeType.Expression,
                value = ")*" + multiplier.ToString("0.#########", CultureInfo.InvariantCulture)
            });

            string format = expression.format == "#,##0" ? "#,##0.#" : expression.format;
            return new ExpressionData { raw = expression.raw, format = format, nodes = nodes };
        }

        internal static CultureInfo TooltipCulture() => TooltipText.GetCulture(DewSave.profileMain?.language);
    }
}
