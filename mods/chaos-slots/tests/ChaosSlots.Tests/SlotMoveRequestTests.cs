using ChaosSlots;

internal static class SlotMoveRequestTests
{
    internal static void PlayerBudgetsRemainIndependent()
    {
        var sender = new SlotLayout(2, 1, 0, 1);
        var otherPlayer = new SlotLayout(0, 0, 2, 0);
        var request = new SlotMoveRequest(MemorySlot.Q, 3, MemorySlot.W, 2, 1, 42, 1);

        Assert(true, TryPlan(sender, request, out var move), "Sender can move at their cap");
        Assert(false, SlotRules.CanGrant(move.Layout, MemorySlot.E), "Sender still has four bonuses");
        Assert(true, SlotRules.CanGrant(otherPlayer, MemorySlot.E), "Other player can still gain bonuses");
        Assert(false, TryPlan(otherPlayer, request, out _), "Other player's layout cannot satisfy the sender's snapshot");
    }

    internal static void CanMoveAnEmptyBonusSlot()
    {
        var sender = new SlotLayout(1, 0, 0, 0);
        var request = new SlotMoveRequest(MemorySlot.Q, 3, MemorySlot.W, 1, 0, 0, 1);

        Assert(true, TryPlan(sender, request, out var move, gemNetId: 0), "Empty bonus can move");
        Assert(true, move.Layout[MemorySlot.Q] == 0 && move.Layout[MemorySlot.W] == 1, "Bonus moved to W");
    }

    internal static void InvalidSlotRequestsAreRejected()
    {
        var sender = new SlotLayout(1, 0, 0, 0);
        foreach (var (source, index, destination) in new[]
        {
            ((MemorySlot)(-1), 3, MemorySlot.W),
            (MemorySlot.Q, 3, (MemorySlot)4),
            (MemorySlot.Q, 2, MemorySlot.W),
            (MemorySlot.Q, 4, MemorySlot.W),
            (MemorySlot.Q, 3, MemorySlot.Q)
        })
        {
            var expectedDestinationBonus = destination == MemorySlot.Q ? 1 : 0;
            var request = new SlotMoveRequest(source, index, destination, 1, expectedDestinationBonus, 42, 1);
            Assert(false, TryPlan(sender, request, out _), $"Invalid request {source}[{index}] to {destination}");
        }
    }

    internal static void ChangedSourceCountRejectsTheRequest()
    {
        var current = new SlotLayout(1, 0, 0, 1);
        var request = new SlotMoveRequest(MemorySlot.Q, 3, MemorySlot.W, 2, 0, 42, 1);

        Assert(true, SlotRules.TryPlanMove(current, MemorySlot.Q, 3, MemorySlot.W, out _), "Chosen index still exists");
        Assert(false, TryPlan(current, request, out _), "Source no longer matches the client snapshot");
    }

    internal static void ChangedDestinationCountRejectsTheRequest()
    {
        var current = new SlotLayout(1, 1, 0, 0);
        var request = new SlotMoveRequest(MemorySlot.Q, 3, MemorySlot.W, 1, 0, 42, 1);

        Assert(true, SlotRules.TryPlanMove(current, MemorySlot.Q, 3, MemorySlot.W, out _), "Destination can still receive a transfer");
        Assert(false, TryPlan(current, request, out _), "Destination no longer matches the client snapshot");
    }

    internal static void TheSourceGemMustStillMatch()
    {
        var current = new SlotLayout(1, 0, 0, 0);
        var occupiedRequest = new SlotMoveRequest(MemorySlot.Q, 3, MemorySlot.W, 1, 0, 42, 1);
        var emptyRequest = new SlotMoveRequest(MemorySlot.Q, 3, MemorySlot.W, 1, 0, 0, 1);

        Assert(false, TryPlan(current, occupiedRequest, out _, gemNetId: 43), "Selected gem was replaced");
        Assert(false, TryPlan(current, occupiedRequest, out _, gemNetId: 0), "Selected gem was removed");
        Assert(false, TryPlan(current, emptyRequest, out _), "Empty slot is now occupied");
    }

    internal static void ImmediateRetryCannotMoveASecondSlot()
    {
        var before = new SlotLayout(2, 0, 0, 0);
        var request = new SlotMoveRequest(MemorySlot.Q, 3, MemorySlot.W, 2, 0, 0, 1);

        Assert(true, TryPlan(before, request, out var first, gemNetId: 0), "Initial request accepted");
        Assert(true, SlotRules.TryPlanMove(first.Layout, MemorySlot.Q, 3, MemorySlot.W, out _), "Another slot compacted into the same index");
        Assert(false, TryPlan(first.Layout, request, out _, gemNetId: 0, lastSequence: 1), "Retry must not move another bonus");
    }

    internal static void ARepeatedSequenceIsRejectedEvenWhenTheOldSnapshotMatches()
    {
        var restored = new SlotLayout(2, 0, 0, 0);
        foreach (ulong replayedSequence in new ulong[] { 0, 2, 3 })
        {
            var request = new SlotMoveRequest(MemorySlot.Q, 3, MemorySlot.W, 2, 0, 42, replayedSequence);
            Assert(false, TryPlan(restored, request, out _, lastSequence: 3), $"Sequence {replayedSequence} after 3");
        }

        var fresh = new SlotMoveRequest(MemorySlot.Q, 3, MemorySlot.W, 2, 0, 42, 4);
        Assert(true, TryPlan(restored, fresh, out _, lastSequence: 3), "A fresh request still works");
    }

    private static bool TryPlan(SlotLayout layout, SlotMoveRequest request, out SlotMove move,
        uint gemNetId = 42, ulong lastSequence = 0) =>
        SlotMoveRequestRules.TryPlan(layout, gemNetId, lastSequence, request, out move);

    private static void Assert(bool expected, bool actual, string context)
    {
        if (expected != actual)
            throw new InvalidOperationException($"{context}: expected {expected}, got {actual}");
    }
}
