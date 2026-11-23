using ChaosSlots;

internal static class BonusSlotArcTests
{
    internal static void NoBonusHasNoUpperArc()
    {
        AssertPoints(BonusSlotArc.BuildUpper(StockArc(), new SlotPoint(0, 0), 0, 0));
    }

    internal static void FourSlotsMirrorTheStockArc()
    {
        var stock = StockArc();
        var upper = BonusSlotArc.BuildUpper(stock, new SlotPoint(0, 0), 4, 0);

        AssertPoints(upper, (-27, 24), (-9, 34), (9, 34), (27, 24));
        AssertPoints(stock, (-27, -24), (-9, -34), (9, -34), (27, -24));
    }

    internal static void FewerSlotsUseASymmetricCentralSubarc()
    {
        var stock = StockArc();
        var center = new SlotPoint(0, 0);

        AssertPoints(BonusSlotArc.BuildUpper(stock, center, 1, 0), (0, 34));
        AssertPoints(BonusSlotArc.BuildUpper(stock, center, 2, 0), (-9, 34), (9, 34));
        AssertPoints(BonusSlotArc.BuildUpper(stock, center, 3, 0), (-18, 29), (0, 34), (18, 29));
    }

    internal static void UpperArcCentersOnTheMemoryCircle()
    {
        var stock = StockArc().Select(point => new SlotPoint(point.X + 65, point.Y + 100)).ToArray();
        var upper = BonusSlotArc.BuildUpper(stock, new SlotPoint(60, 100), 3, 0);

        AssertPoints(upper, (42, 129), (60, 134), (78, 129));
    }

    internal static void ClearanceShiftsTheWholeArcWithoutFlattening()
    {
        var stock = StockArc().Select(point => new SlotPoint(point.X, point.Y + 100)).ToArray();
        var upper = BonusSlotArc.BuildUpper(stock, new SlotPoint(0, 100), 4, 50);

        AssertPoints(upper, (-27, 150), (-9, 160), (9, 160), (27, 150));
    }

    internal static void TwoThroughFourSlotsAreSpacedAndCentered()
    {
        var stock = StockArc().Select(point => new SlotPoint(point.X + 60, point.Y + 100)).ToArray();
        var center = new SlotPoint(60, 100);

        AssertPoints(BonusSlotArc.BuildUpper(stock, center, 2, 0, 22), (49, 134), (71, 134));
        AssertPoints(BonusSlotArc.BuildUpper(stock, center, 3, 0, 22), (38, 129), (60, 134), (82, 129));
        var upper = BonusSlotArc.BuildUpper(stock, center, 4, 0, 22);

        AssertPoints(upper, (27, 124), (49, 134), (71, 134), (93, 124));
    }

    internal static void RoundClearanceDoesNotLiftClearOuterSlots()
    {
        var upper = BonusSlotArc.BuildUpper(StockArc(), new SlotPoint(0, 0), 4, 35, 18);

        AssertPoints(upper, (-27, 24), (-9, 34), (9, 34), (27, 24));
    }

    internal static void RoundClearanceOnlyLiftsAsFarAsNeeded()
    {
        const float clearance = 37;
        var upper = BonusSlotArc.BuildUpper(StockArc(), new SlotPoint(0, 0), 4, clearance, 18);
        var minDistance = upper.Min(point => MathF.Sqrt(point.X * point.X + point.Y * point.Y));

        AssertNear(clearance, minDistance, "Nearest point sits at the required clearance");
        AssertNear(10, upper[1].Y - upper[0].Y, "The upper arc retains its height difference");
        AssertNear(10, upper[2].Y - upper[3].Y, "The upper arc retains its mirrored height difference");
    }

    internal static void RoundClearanceUsesWidenedPositions()
    {
        var upper = BonusSlotArc.BuildUpper(StockArc(), new SlotPoint(0, 0), 4, 40, 46);

        AssertPoints(upper, (-69, 24), (-23, 34), (23, 34), (69, 24));
    }

    private static void AssertNear(float expected, float actual, string context)
    {
        if (!(MathF.Abs(expected - actual) <= 0.0001f))
            throw new InvalidOperationException($"{context}: expected {expected}, got {actual}");
    }

    private static SlotPoint[] StockArc() =>
        [new(-27, -24), new(-9, -34), new(9, -34), new(27, -24)];

    private static void AssertPoints(IReadOnlyList<SlotPoint> actual, params (float X, float Y)[] expected)
    {
        if (actual.Count != expected.Length)
            throw new InvalidOperationException($"Expected {expected.Length} points, got {actual.Count}");

        for (int i = 0; i < expected.Length; i++)
        {
            var point = actual[i];
            var coordinates = (point.X, point.Y);
            if (coordinates != expected[i])
                throw new InvalidOperationException($"Point {i}: expected {expected[i]}, got {coordinates}");
        }
    }
}
