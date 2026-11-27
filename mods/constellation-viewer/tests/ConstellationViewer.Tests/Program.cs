using ConstellationViewer;
using ShapeOfDreamsMods;

(string Name, Action Run)[] tests =
[
    (nameof(WindowUsesTheGameLanguage), WindowUsesTheGameLanguage),
    (nameof(ShowsAllCategoriesInSlotOrder), ShowsAllCategoriesInSlotOrder),
    (nameof(SkipsEmptySlotsAndMissingCategories), SkipsEmptySlotsAndMissingCategories),
    (nameof(SkipsUnequippedLevelZeroStars), SkipsUnequippedLevelZeroStars),
    (nameof(EmptyLoadoutProducesNoRows), EmptyLoadoutProducesNoRows),
    (nameof(ReadingTheRowsLeavesTheRunLoadoutIntact), ReadingTheRowsLeavesTheRunLoadoutIntact),
    (nameof(DescriptionLevelUsesTheEquippedLevel), DescriptionLevelUsesTheEquippedLevel),
    (nameof(DescriptionLevelStaysInBounds), DescriptionLevelStaysInBounds),
    (nameof(RecognizesTheSavedRunLoadout), RecognizesTheSavedRunLoadout),
    (nameof(MismatchedSlotReducesEffectStrength), MismatchedSlotReducesEffectStrength),
];

var failures = 0;
foreach (var (name, run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception error)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {error}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed.");
return failures == 0 ? 0 : 1;

static void WindowUsesTheGameLanguage()
{
    Equal("装備中の星座", TooltipText.Get(TooltipText.GetCulture("ja-JP"), "Equipped"), "Japanese window title");
    Equal("已裝備的星座", TooltipText.Get(TooltipText.GetCulture("zh-TW"), "Equipped"), "Traditional Chinese window title");
    Equal("Nenhuma constelação equipada", TooltipText.Get(TooltipText.GetCulture("pt-BR"), "Empty"), "Portuguese empty state");
}

static void ShowsAllCategoriesInSlotOrder()
{
    var loadout = new HeroLoadoutData
    {
        cDestruction = [Star("SecondAlphabetically", 3), Star("FirstAlphabetically", 2)],
        cLife = [Star("LifeStar", 1)],
        cImagination = [Star("ImaginationStar", 4)],
        cFlexible = [Star("FlexibleStar", 1)],
    };

    ExpectRows(loadout,
        (StarType.Destruction, "SecondAlphabetically", 3),
        (StarType.Destruction, "FirstAlphabetically", 2),
        (StarType.Life, "LifeStar", 1),
        (StarType.Imagination, "ImaginationStar", 4),
        (StarType.Flexible, "FlexibleStar", 1));
}

static void SkipsEmptySlotsAndMissingCategories()
{
    var loadout = new HeroLoadoutData
    {
        cDestruction = null,
        cLife = [default, Star("LifeStar", 2), Star(string.Empty, 3)],
        cFlexible = [default, Star("FlexibleStar", 1)]
    };

    ExpectRows(loadout, (StarType.Life, "LifeStar", 2), (StarType.Flexible, "FlexibleStar", 1));
}

static void SkipsUnequippedLevelZeroStars()
{
    var loadout = new HeroLoadoutData
    {
        cFlexible = [Star("InactiveStar", 0), Star("EquippedStar", 1)]
    };

    ExpectRows(loadout, (StarType.Flexible, "EquippedStar", 1));
}

static void EmptyLoadoutProducesNoRows() => ExpectRows(new HeroLoadoutData());

static void ReadingTheRowsLeavesTheRunLoadoutIntact()
{
    var loadout = new HeroLoadoutData { cLife = [default, Star("LifeStar", 3), default] };
    var originalSlots = loadout.cLife.ToArray();

    var row = EquippedStars.Enumerate(loadout).Single();
    Equal(2, EquippedStars.GetDisplayLevel(row.Entry, 2), "Display level");
    ExpectRows(loadout, (StarType.Life, "LifeStar", 3));
    Equal(true, originalSlots.SequenceEqual(loadout.cLife), "Original slots and levels");
}

static void DescriptionLevelUsesTheEquippedLevel() =>
    Equal(3, EquippedStars.GetDisplayLevel(Star("LifeStar", 3), 5), "Equipped level");

static void DescriptionLevelStaysInBounds()
{
    Equal(1, EquippedStars.GetDisplayLevel(Star("LifeStar", -4), 5), "Below minimum");
    Equal(5, EquippedStars.GetDisplayLevel(Star("LifeStar", 10), 5), "Above maximum");
    Equal(1, EquippedStars.GetDisplayLevel(Star("LifeStar", 3), 0), "Missing maximum");
}

static void RecognizesTheSavedRunLoadout()
{
    Equal(false, EquippedStars.IsRestoredLoadout(new HeroLoadoutData()), "A new run with empty slots");
    var loadout = new HeroLoadoutData { skillQ = -1, skillR = -1, skillTrait = -1, skillMovement = -1 };
    Equal(true, EquippedStars.IsRestoredLoadout(loadout), "A run restored by DewPersistence");
    loadout.skillMovement = 0;
    Equal(false, EquippedStars.IsRestoredLoadout(loadout), "A loadout with a configured movement skill");
}

static void MismatchedSlotReducesEffectStrength()
{
    Equal(0.5f, EquippedStars.GetStrength(StarType.Life, StarType.Destruction), "Mismatched slot");
    Equal(1f, EquippedStars.GetStrength(StarType.Life, StarType.Life), "Matching slot");
    Equal(1f, EquippedStars.GetStrength(StarType.Flexible, StarType.Destruction), "Flexible slot");
    Equal(1f, EquippedStars.GetStrength(StarType.Destruction, StarType.Flexible), "Flexible star");
}

static LoadoutStarItem Star(string name, int level) => new() { name = name, level = level };

static void ExpectRows(HeroLoadoutData loadout, params (StarType Category, string Name, int Level)[] expected)
{
    var actual = EquippedStars.Enumerate(loadout).Select(row => (row.Category, row.Entry.name, row.Entry.level)).ToArray();
    if (!expected.SequenceEqual(actual))
        throw new InvalidOperationException($"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");
}

static void Equal<T>(T expected, T actual, string context)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{context}: expected {expected}, got {actual}");
}
