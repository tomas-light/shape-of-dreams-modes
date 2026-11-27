using ChaosSlots;

internal static class ScoreboardSlotLayoutTests
{
    private const float MemoryHeight = 100f;
    private const float SlotDiameter = 40f;
    private const float StockFrameTop = 75.4f;
    private static readonly SlotPoint[] NativeFour =
    [
        new(-50f, -55.5f), new(-18f, -75.5f), new(18f, -75.5f), new(50f, -55.5f)
    ];

    internal static void FiveSlotMemoryKeepsItsBaseSlotsAndShowsBothBonuses()
    {
        int count = ScoreboardSlotLayout.BonusCount(5, usesExpandedFour: false);
        var upper = PositionSlots(count);
        Check(ScoreboardSlotLayout.ThreeSlotGroup == 1, "The stock three-slot group supplies the base slots.");
        Check(count == 2 && upper.Count == 2, "The upper arc contains both extra slots.");
        Check(ScoreboardSlotLayout.BonusSlotIndex(0) == 3 && ScoreboardSlotLayout.BonusSlotIndex(1) == 4,
            "The two bonus icons read essence indices 3 and 4.");
        Check(upper[0].X < 0f && upper[1].X == -upper[0].X, "The bonus slots are centered on the memory.");
        Check(upper.All(point => point.Y - SlotDiameter * 0.5f > MemoryHeight * 0.5f),
            "The two slot rings clear the top of the memory.");
    }

    internal static void EverySupportedCapacitySelectsItsOwnGroup()
    {
        for (int capacity = 2; capacity <= 7; capacity++)
        {
            int index = ScoreboardSlotLayout.GroupIndex(capacity);
            int count = ScoreboardSlotLayout.BonusCount(capacity, usesExpandedFour: true);
            Check(index >= 0 && index < ScoreboardSlotLayout.GroupCount && index + 2 == capacity,
                $"Stock update can select the group for capacity {capacity}.");
            Check(count == Math.Max(0, capacity - 3), $"The group at capacity {capacity} has the expected bonus count.");
        }
        Check(ScoreboardSlotLayout.BonusCount(0, true) == 0 && ScoreboardSlotLayout.BonusCount(8, true) == 0,
            "Unsupported counts do not request an expanded group.");
    }

    internal static void ExpandedFrameFitsTheUpperArcAtEveryBonusCount()
    {
        for (int count = 1; count <= SlotRules.BonusLimit; count++)
        {
            var upper = PositionSlots(count);
            float extra = ScoreboardSlotLayout.ExtraHeight(upper, SlotDiameter, MemoryHeight, StockFrameTop);
            Check(extra > 0f, $"The frame expands for {count} bonus slots.");
            float margin = StockFrameTop + extra - upper.Max(point => point.Y + SlotDiameter * 0.5f);
            Check(margin > 6f, $"All {count} rings fit inside the frame with padding.");
            for (int i = 1; i < upper.Count; i++)
            {
                var left = upper[i - 1];
                var right = upper[i];
                Check(float.Hypot(right.X - left.X, right.Y - left.Y) > SlotDiameter,
                    $"Adjacent bonus rings in the group of {count} do not overlap.");
            }
        }
    }

    internal static void UpperArcClearsTheChargeBadge()
    {
        var badge = new ScoreboardChargeBadge(15f, 55f, 55f);
        foreach (int count in new[] { 3, 4 })
        {
            var before = PositionSlots(count);
            var after = ScoreboardSlotLayout.PositionBonusSlots(NativeFour, new SlotPoint(0f, 0f), count,
                MemoryHeight, SlotDiameter, badge);
            float lift = after[0].Y - before[0].Y;
            Check(lift > 0f, $"The charge badge lifts the arc with {count} bonus slots.");
            for (int i = 0; i < after.Count; i++)
            {
                var point = after[i];
                float horizontalDistance = Math.Max(0f, Math.Max(badge.Left - point.X, point.X - badge.Right));
                Check(point.Y >= badge.Top &&
                    float.Hypot(horizontalDistance, point.Y - badge.Top) >= SlotDiameter * 0.5f + 3.9f,
                    $"Bonus ring {i} in the group of {count} clears the badge.");
                Check(point.X == before[i].X && Math.Abs(point.Y - before[i].Y - lift) < 0.001f,
                    "The badge moves the entire arc up without reshaping it.");
            }
            float extra = ScoreboardSlotLayout.ExtraHeight(after, SlotDiameter, MemoryHeight, StockFrameTop);
            Check(StockFrameTop + extra > after.Max(point => point.Y) + SlotDiameter * 0.5f,
                "The taller arc still fits within the expanded frame.");
        }
    }

    internal static void UnmodifiedPlayerKeepsTheStockFourSlotGroup()
    {
        Check(ScoreboardSlotLayout.BonusCount(4, usesExpandedFour: false) == 0,
            "An unmodified player uses the stock four-slot group.");
        Check(ScoreboardSlotLayout.BonusCount(4, usesExpandedFour: true) == 1,
            "A registered player uses the group with one bonus above the memory.");
        for (int capacity = 5; capacity <= 7; capacity++)
            Check(ScoreboardSlotLayout.BonusCount(capacity, usesExpandedFour: false) == capacity - SlotRules.BaseSlots,
                $"A capacity of {capacity} remains visible before capability information arrives.");
    }

    internal static void RowHeightFollowsTheTallestMemory()
    {
        float tall = ExtraHeightFor(6, usesExpandedFour: true);
        float shortArc = ExtraHeightFor(4, usesExpandedFour: true);
        float stockFour = ExtraHeightFor(4, usesExpandedFour: false);
        float noBonuses = ExtraHeightFor(3, usesExpandedFour: true);
        Check(tall > shortArc && shortArc > 0f, "The active arcs need different heights.");
        Check(RowExtraHeight(tall, shortArc) == tall, "The row grows enough for its tallest memory.");
        Check(RowExtraHeight(noBonuses, shortArc) == shortArc, "Removing the taller arc leaves the shorter one visible.");
        Check(RowExtraHeight(noBonuses, stockFour) == 0f, "The row restores its stock height when expanded groups are gone.");
        Check(RowExtraHeight(stockFour, tall) == tall, "A second player's row can stay expanded independently.");
    }

    internal static void OnlyRegisteredPeersUseTheExpandedFourSlotGroup()
    {
        string peers = ScoreboardPeers.Format(new uint[] { 0, 12, 34 });
        Check(ScoreboardPeers.Contains(peers, 12) && ScoreboardPeers.Contains(peers, 34), "Both registered peers are present.");
        Check(!ScoreboardPeers.Contains(peers, 2), "A partial ID must not match a registered peer.");
        Check(!ScoreboardPeers.Contains(peers, 0), "A connection without a player identity is excluded.");
        Check(!ScoreboardPeers.Contains(null!, 12), "An older host without an advertised roster leaves remote guests unknown.");
        Check(!ScoreboardPeers.Contains(ScoreboardPeers.Format(new uint[] { 34 }), 12),
            "A departed peer loses the expanded four-slot layout.");
    }

    private static float ExtraHeightFor(int capacity, bool usesExpandedFour) =>
        ScoreboardSlotLayout.ExtraHeight(PositionSlots(ScoreboardSlotLayout.BonusCount(capacity, usesExpandedFour)),
            SlotDiameter, MemoryHeight, StockFrameTop);

    private static float RowExtraHeight(params float[] memoryHeights) =>
        ScoreboardSlotLayout.RowExtraHeight(memoryHeights, static height => height);

    private static IReadOnlyList<SlotPoint> PositionSlots(int count) =>
        ScoreboardSlotLayout.PositionBonusSlots(NativeFour, new SlotPoint(0f, 0f), count, MemoryHeight, SlotDiameter);

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
