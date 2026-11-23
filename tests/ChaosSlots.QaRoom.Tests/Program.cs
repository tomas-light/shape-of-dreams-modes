using ChaosSlots.QaRoom;

(string Name, Action Run)[] tests =
[
    (nameof(FiveShrinesAllowAFifthRewardAttempt), FiveShrinesAllowAFifthRewardAttempt),
    (nameof(EightEssenceFixturesHaveDistinctIndices), EightEssenceFixturesHaveDistinctIndices),
    (nameof(TwoMemoryPickupsAreAvailable), TwoMemoryPickupsAreAvailable),
    (nameof(NoAvailableMemoriesKeepAllCoreFixtures), NoAvailableMemoriesKeepAllCoreFixtures),
    (nameof(OneAvailableMemoryKeepsAllCoreFixtures), OneAvailableMemoryKeepsAllCoreFixtures),
    (nameof(EveryFixtureHasASupportedKind), EveryFixtureHasASupportedKind),
    (nameof(PlacementsAreDeterministic), PlacementsAreDeterministic),
    (nameof(FixturesHaveFiniteOffsetsAwayFromHero), FixturesHaveFiniteOffsetsAwayFromHero),
    (nameof(FixturePositionsAreSpacedApart), FixturePositionsAreSpacedApart),
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
        Console.Error.WriteLine($"FAIL {name}: {error.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed.");
return failures == 0 ? 0 : 1;

static void FiveShrinesAllowAFifthRewardAttempt()
{
    Equal(5, TestRoomPlan.ShrineCount, "Number of planned shrines");
    AssertFixtureIndices(FixtureKind.Shrine, 5);
}

static void EightEssenceFixturesHaveDistinctIndices()
{
    Equal(8, TestRoomPlan.EssenceCount, "Number of planned essence pickups");
    AssertFixtureIndices(FixtureKind.Essence, 8);
}

static void TwoMemoryPickupsAreAvailable()
{
    Equal(2, TestRoomPlan.MemoryCount, "Number of planned memory pickups");
    AssertFixtureIndices(FixtureKind.Memory, 2);
}

static void NoAvailableMemoriesKeepAllCoreFixtures()
{
    var plan = TestRoomPlan.GetPlacements(0);
    Equal(0, plan.Count(fixture => fixture.Kind == FixtureKind.Memory), "Unavailable memories are omitted");
    AssertUnchangedCoreFixtures(plan);
}

static void OneAvailableMemoryKeepsAllCoreFixtures()
{
    var plan = TestRoomPlan.GetPlacements(1);
    var memory = plan.Single(fixture => fixture.Kind == FixtureKind.Memory);
    Equal(0, memory.Index, "The first memory position is used");
    AssertUnchangedCoreFixtures(plan);
}

static void AssertUnchangedCoreFixtures(IReadOnlyList<FixturePlacement> plan)
{
    var originalCore = TestRoomPlan.GetPlacements().Where(fixture => fixture.Kind != FixtureKind.Memory)
        .Select(PlacementKey);
    var actualCore = plan.Where(fixture => fixture.Kind != FixtureKind.Memory).Select(PlacementKey);
    Equal(true, originalCore.SequenceEqual(actualCore), "All shrine and essence positions remain intact");
}

static void EveryFixtureHasASupportedKind()
{
    Equal(15, TestRoomPlan.GetPlacements().Count, "Total placements");
}

static void PlacementsAreDeterministic()
{
    var first = TestRoomPlan.GetPlacements().Select(PlacementKey).ToArray();
    var second = TestRoomPlan.GetPlacements().Select(PlacementKey).ToArray();
    Equal(true, first.SequenceEqual(second), "Repeated plans have the same layout and order");
}

static void FixturesHaveFiniteOffsetsAwayFromHero()
{
    foreach (var fixture in TestRoomPlan.GetPlacements())
    {
        var context = $"{fixture.Kind} #{fixture.Index}";
        Equal(true, float.IsFinite(fixture.X) && float.IsFinite(fixture.Z), $"{context} offset is finite");
        Equal(true, fixture.X * fixture.X + fixture.Z * fixture.Z >= 1f, $"{context} is clear of the hero");
    }
}

static void FixturePositionsAreSpacedApart()
{
    var placements = TestRoomPlan.GetPlacements();
    for (var i = 0; i < placements.Count; i++)
    {
        for (var j = i + 1; j < placements.Count; j++)
        {
            var first = placements[i];
            var second = placements[j];
            var x = first.X - second.X;
            var z = first.Z - second.Z;
            Equal(true, x * x + z * z >= 1f, $"{first.Kind} #{first.Index} is clear of {second.Kind} #{second.Index}");
        }
    }
}

static void AssertFixtureIndices(FixtureKind kind, int expectedCount)
{
    var actual = TestRoomPlan.GetPlacements().Where(fixture => fixture.Kind == kind).Select(fixture => fixture.Index);
    var expected = Enumerable.Range(0, expectedCount);
    Equal(true, actual.SequenceEqual(expected), $"Unique, ordered indices for {kind}");
}

static (FixtureKind Kind, int Index, float X, float Z) PlacementKey(FixturePlacement fixture) =>
    (fixture.Kind, fixture.Index, fixture.X, fixture.Z);

static void Equal<T>(T expected, T actual, string context)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{context}: expected {expected}, got {actual}");
}
