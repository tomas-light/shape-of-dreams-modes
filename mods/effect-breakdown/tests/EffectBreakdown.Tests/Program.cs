using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DewInternal;
using EffectBreakdown;
using ShapeOfDreamsMods;

int checks = 0;
var russian = CultureInfo.GetCultureInfo("ru-RU");
var english = CultureInfo.GetCultureInfo("en-US");
var french = TooltipText.GetCulture("fr-FR");
var japanese = TooltipText.GetCulture("ja-JP");
Check(TooltipBreakdown.FormatModifier(new EffectModifier("Brise", 1f / 1.15f, 15f), french) ==
    "Brise : +15 d’accélération → −13,04% au temps de recharge", "Haste points and cooldown reduction are formatted in French.");
Check(TooltipText.Get(japanese, "Heading") == "補正込みの計算" && TooltipText.Format(japanese, "Seconds", "4.21") == "4.21秒",
    "Japanese tooltip text and seconds must use the Japanese catalog.");
Check(TooltipText.Get(TooltipText.GetCulture("zh-HK"), "AbilityCooldown") == "技能冷卻時間" &&
    TooltipText.Get(TooltipText.GetCulture("zh-CN"), "AbilityCooldown") == "技能冷却时间" &&
    TooltipText.Get(TooltipText.GetCulture(null), "Heading") == "Modifier breakdown",
    "Chinese script variants and the default language must resolve independently.");
Check(TooltipText.Matches(french, "DarkToken", "Dégâts de Ténèbres") &&
    TooltipText.Matches(japanese, "DamageDealtToken", "ダメージ量の") &&
    TooltipText.Matches(TooltipText.GetCulture("tr-TR"), "LightToken", "Işık Hasarı"),
    "Element and damage-based healing keywords must follow the language of the game description.");
var wounds = new[]
{
    new EffectModifier("Регенерация кожи", 1.18f),
    new EffectModifier("Смертельные раны", 0.5f)
};

var multiplier = TooltipBreakdown.Combine(wounds);
Check(Math.Abs(73 * multiplier - 43.07f) < 0.0001f, "Healing modifiers must multiply.");
string healing = Render(russian, new TooltipRow("Исцеление на себя", "<color=#abcdef>73</color>",
    "<gradient=AP>43,1</gradient>", new[] { "Регенерация кожи +18%", "Смертельные раны −50%" }, true));
Check(healing.StartsWith("Описание.\n\n<b>Расчёт с модификаторами</b>\n<b>Исцеление на себя</b>  73 → <color=#ff6565>43,1</color>\n"),
    "Append the calculation below the original description and color only the penalty result.");
Check(healing.IndexOf("Регенерация кожи", StringComparison.Ordinal) > healing.IndexOf("43,1", StringComparison.Ordinal),
    "Source names go below the result.");

var canceled = new[] { new EffectModifier("Bonus", 2f), new EffectModifier("Penalty", 0.5f) };
Check(TooltipBreakdown.Combine(canceled) == 1f &&
    Render(english, new TooltipRow("Self-healing", "73", "73", new[] { "Bonus +100%", "Penalty −50%" }, false))
        .Contains("Bonus +100% · Penalty −50%"),
    "Canceled effects still appear as separate sources.");
Check(TooltipBreakdown.Append("Описание.", new List<TooltipRow>(), russian) == "Описание.",
    "An empty breakdown must leave the tooltip untouched.");
var unreadable = new List<string>();
var discovered = EffectDiscovery.Read(Path.Combine(AppContext.BaseDirectory, "Dew.Contents.dll"), unreadable.Add);
var expressions = new EffectExpressions(discovered);
Check(expressions.Classify(Expression("Ai_C_BeamOfLight::tickHeal", "Ai_C_BeamOfLight::tickInterval")) ==
    EffectKind.Heal, "The per-second healing expression must be recognized.");
Check(expressions.Classify(Expression("Se_Gem_Regeneration::healPerTick", "Se_Gem_Regeneration::totalTicks")) ==
    EffectKind.Heal, "The essence healing expression must be recognized.");
Check(expressions.Classify(Expression("Ai_C_FlashFreeze::shieldAmount")) ==
    EffectKind.Barrier, "The barrier amount must be recognized.");
Check(expressions.Classify(Expression("Ai_C_BeamOfLight::tickDamage", "Ai_C_BeamOfLight::tickInterval")) ==
    EffectKind.Damage, "The adjacent expression must be classified independently as damage.");
Check(expressions.Classify(Expression("Ai_C_FlashFreeze::shieldDuration")) ==
    EffectKind.None, "Barrier duration must remain untouched.");

Check(expressions.Classify(Expression("Ai_R_SanctuaryOfEl_Ground::startShield")) ==
    EffectKind.Barrier, "Sanctuary of El's initial barrier must be recognized.");
Check(expressions.Classify(Expression("Ai_R_SanctuaryOfEl_Ground::shieldDuration")) ==
    EffectKind.None, "Sanctuary of El's barrier duration must remain untouched.");

Check(expressions.Classify(Expression("Gem_R_Snow::shieldAmount")) ==
    EffectKind.Barrier, "Snow's HealData feeds the barrier processors.");

DiscoveryFixture.Verify();
static float JellyfishHasteFactor(float points) => 0.5f + 0.5f / (1f + points * 0.01f);
var breezeModifiers = TooltipBreakdown.HasteModifiers(new[] { ("Лёгкий ветерок", 15f) }, JellyfishHasteFactor).ToArray();
string breeze = TooltipBreakdown.FormatModifier(breezeModifiers[0], russian);
Check(breeze == "Лёгкий ветерок: +15 к ускорению → −6,52% ко времени перезарядки",
    "A haste source must display both its flat points and its effective reduction.");
var combinedHaste = TooltipBreakdown.HasteModifiers(new[] { ("Breeze", 15f), ("Second star", 10f) }, JellyfishHasteFactor).ToArray();
Check(TooltipBreakdown.FormatModifier(combinedHaste[1], english) ==
    "Second star: +10 haste → −3.72% cooldown time" &&
    Math.Abs(TooltipBreakdown.Combine(combinedHaste) - JellyfishHasteFactor(25f)) < 0.000001f,
    "Successive haste contributions must describe the combined result without stacking independent percentages.");
var liveHaste = TooltipBreakdown.HasteModifiers(new[] { ("Breeze", 15f) }, JellyfishHasteFactor, 10f, "Other haste").ToArray();
Check(TooltipBreakdown.FormatModifier(liveHaste[1], english) == "Breeze: +15 haste → −5.71% cooldown time" &&
    Math.Abs(TooltipBreakdown.Combine(liveHaste) - JellyfishHasteFactor(25f)) < 0.000001f,
    "A star's contribution must account for haste the hero already has from other sources.");
string cooldowns = Render(russian,
    new TooltipRow("Перезарядка навыка", "4,5 с", "4,21 с", new[] { breeze }, false),
    new TooltipRow("Урон тьмы по боссу", "245%", "284,2%", new[] { "Отважный претендент +16%" }, false),
    new TooltipRow("Сокращение перезарядки при усилении", "50%", "50%", new[] { "Осторожная медуза · не влияет" }, false));
Check(cooldowns.Contains("Перезарядка навыка</b>  4,5 с → 4,21 с") &&
    !cooldowns.Contains("#ff6565"), "A shorter cooldown and an increased damage result must use the default color.");
Check(cooldowns.Contains("Урон тьмы по боссу</b>  245% → 284,2%"), "A boss condition belongs in the row label.");
Check(cooldowns.Contains("Сокращение перезарядки при усилении</b>  50% → 50%") &&
    cooldowns.Contains("Осторожная медуза · не влияет"), "An unchanged proc reduction must have its own row and source.");
Check(EffectExpressions.FormLabel(Expression("Ai_Q_Laceration_Dash::dmgFactor"), russian) == " · красная форма" &&
    EffectExpressions.FormLabel(Expression("Ai_Q_Laceration_Circle::slashDamage"), russian) == " · синяя форма" &&
    EffectExpressions.FormLabel(Expression("Ai_Q_Laceration_Circle::slashDamage"), japanese) == " · 青型",
    "Laceration's damage lines must keep their form names.");
var onCrit = Expression("Gem_E_Fangs::cooldownReductionOnCrit");
onCrit.raw = "cooldownReductionOnCrit";
var onEmpoweredCast = Expression("Ai_C_DarkBolt::empoweredCooldownReductionRatio");
onEmpoweredCast.raw = "empoweredCooldownReductionRatio";
Check(EffectExpressions.CooldownReductionLabel(onCrit, russian) == "Сокращение перезарядки при критическом ударе" &&
    EffectExpressions.CooldownReductionLabel(onEmpoweredCast, english) == "Cooldown reduction on empowered cast",
    "Critical hits and empowered casts must have distinct trigger labels.");
var perHit = Expression("Ai_Q_Laceration_Circle::cooldownReductionPerHit");
var maximum = Expression("Ai_Q_Laceration_Circle::cooldownReductionMax");
var jellyfish = new[] { "Осторожная медуза · не влияет" };
string blueForm = Render(russian,
    new TooltipRow(EffectExpressions.CooldownReductionLabel(perHit, russian) + EffectExpressions.FormLabel(perHit, russian),
        "15%", "15%", jellyfish, false),
    new TooltipRow(EffectExpressions.CooldownReductionLabel(maximum, russian) + EffectExpressions.FormLabel(maximum, russian),
        "60%", "60%", jellyfish, false));
Check(expressions.Classify(perHit) == EffectKind.CooldownRatio && expressions.Classify(maximum) == EffectKind.CooldownRatio &&
    blueForm.Contains("Сокращение за каждого поражённого врага · синяя форма</b>  15% → 15%") &&
    blueForm.Contains("Максимальное сокращение · синяя форма</b>  60% → 60%") && !blueForm.Contains("· эффект "),
    "Laceration's per-hit reduction and cap must have distinct labels and keep their values.");
Check(Render(english, new TooltipRow("Ability cooldown", "8 s", "10 s", new[] { "Penalty +25%" }, true))
    .Contains("→ <color=#ff6565>10 s</color>"), "A longer cooldown must be red.");
Check(Render(english, new TooltipRow("Cooldown reduction on trigger", "4 s", "2,8 s", new[] { "Penalty −30%" }, true))
    .Contains("→ <color=#ff6565>2,8 s</color>"), "A smaller reduction must be red even though its result is shorter.");
Check(Render(english, new TooltipRow("Cold damage", "100%", "120%", new[] { "Bonus +20%" }, false),
    new TooltipRow("Cold damage", "200%", "240%", new[] { "Bonus +20%" }, false))
    .Contains("Cold damage · effect 2"), "Separate hits must not share an ambiguous label.");
Check(expressions.Classify(Expression("Gem_R_Flow::reduceRatio")) == EffectKind.CooldownRatio,
    "Flow passes a ratio to the game, so flat cooldown reduction penalties do not apply.");
Check(expressions.Classify(Expression("Ai_C_DarkSpear::dmgFactor")) == EffectKind.Damage,
    "Dark Spear's tooltip references a damage field inherited from a base class.");
Check(expressions.Classify(Expression("Ai_C_Starfall::dmgPerStar")) == EffectKind.Damage,
    "Starfall passes its tooltip damage to a projectile field before the hit.");
Check(expressions.Classify(Expression("Ai_C_Starfall::baseCount")) == EffectKind.None,
    "Starfall's projectile count must remain separate from damage.");
Check(expressions.Classify(Expression("Ai_C_IceClaw::dmgFactor")) == EffectKind.Damage &&
    expressions.Classify(Expression("Ai_C_IceClaw_Second::dmgFactor")) == EffectKind.Damage,
    "Both Ice Claw stages inherit the damage field.");
Check(expressions.Classify(Expression("Gem_C_Efficiency::reducedRatio")) == EffectKind.CooldownMultiplier,
    "Efficiency's displayed ratio is a persistent cooldown multiplier, so Jellyfish does not scale it.");
Check(expressions.Classify(Expression("Gem_C_Efficiency::skillHaste")) == EffectKind.CooldownMultiplier,
    "Efficiency's haste field feeds the same multiplier through its getter.");
Check(expressions.Classify(Expression("Ai_Q_Lunge::dodgeCooldownReductionRatioOnHit")) == EffectKind.CooldownRatio,
    "Lunge sends a percentage through CooldownReductionByRatioSettings.");
Check(expressions.Classify(Expression("Se_R_Tranquility::totalReducedSeconds")) == EffectKind.SharedCooldownSeconds,
    "Tranquility uses the same magnitude for Memories and Essence cooldowns.");
Check(expressions.Classify(Expression("Se_R_Tranquility::ticks")) == EffectKind.None,
    "The tick count is a divisor, not an amount in seconds.");
Check(expressions.Classify(Expression("Gem_E_Flexibility::dmgTickCount")) == EffectKind.None,
    "The number of damage ticks is not the amount of damage.");
Check(expressions.Classify(Expression("Gem_U_GlacialCore::maxShootCount")) == EffectKind.None,
    "The projectile count does not inherit the damage classification.");
Check(expressions.Classify(Expression("Gem_E_Direness::maxAbilityHasteHpThreshold")) == EffectKind.None &&
    expressions.Classify(Expression("Gem_E_Direness::maxReducedRatio")) == EffectKind.CooldownMultiplier,
    "A health threshold is not a cooldown multiplier.");
Check(expressions.Classify(Expression("Gem_L_Embertail::skillHastePerBurning")) == EffectKind.CooldownMultiplier,
    "Embertail's cooldown formula uses 100 as its reciprocal numerator.");
Check(expressions.Classify(Expression("Se_Gem_R_Panic::memoryHaste")) == EffectKind.CooldownMultiplier,
    "Panic's cooldown formula uses 100 as its reciprocal numerator.");
Check(expressions.Classify(Expression("Ai_E_FinalExplosion_Explosion::selfDamageRatioCalculated")) == EffectKind.None,
    "The explosion's health cost damages the caster and is not boosted by boss damage bonuses.");
foreach (string diagnostic in unreadable)
    Console.WriteLine(diagnostic);
Console.WriteLine($"{checks} checks passed. Discovered {discovered.Count} numeric fields, skipped {unreadable.Count} methods with malformed exception handlers.");

static string Render(CultureInfo culture, params TooltipRow[] rows) =>
    TooltipBreakdown.Append("Описание.", new List<TooltipRow>(rows), culture);

static ExpressionData Expression(params string[] fields)
{
    var nodes = new List<ExpressionChildNode>();
    foreach (var field in fields)
        nodes.Add(new ExpressionChildNode { type = ExpressionChildNodeType.FieldName, value = field });
    return new ExpressionData { nodes = nodes };
}

void Check(bool condition, string failure)
{
    if (!condition)
        throw new InvalidOperationException(failure);
    checks++;
}
