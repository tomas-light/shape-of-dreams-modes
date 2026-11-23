namespace ChaosSlots
{
    public readonly struct SlotMoveRequest
    {
        public SlotMoveRequest(MemorySlot source, int sourceIndex, MemorySlot destination,
            int expectedSourceBonus, int expectedDestinationBonus, uint expectedGemNetId, ulong sequence)
        {
            Source = source;
            SourceIndex = sourceIndex;
            Destination = destination;
            ExpectedSourceBonus = expectedSourceBonus;
            ExpectedDestinationBonus = expectedDestinationBonus;
            ExpectedGemNetId = expectedGemNetId;
            Sequence = sequence;
        }

        public MemorySlot Source { get; }
        public int SourceIndex { get; }
        public MemorySlot Destination { get; }
        public int ExpectedSourceBonus { get; }
        public int ExpectedDestinationBonus { get; }
        public uint ExpectedGemNetId { get; }
        public ulong Sequence { get; }
    }

    public static class SlotMoveRequestRules
    {
        public static bool TryPlan(SlotLayout layout, uint currentGemNetId, ulong lastSequence,
            SlotMoveRequest request, out SlotMove move)
        {
            move = default;
            if (request.Sequence <= lastSequence || currentGemNetId != request.ExpectedGemNetId
                || !SlotRules.TryPlanMove(layout, request.Source, request.SourceIndex, request.Destination, out var candidate)
                || layout[request.Source] != request.ExpectedSourceBonus
                || layout[request.Destination] != request.ExpectedDestinationBonus)
                return false;

            move = candidate;
            return true;
        }
    }
}
