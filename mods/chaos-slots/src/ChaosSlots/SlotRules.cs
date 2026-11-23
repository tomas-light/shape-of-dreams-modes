namespace ChaosSlots
{
    public enum MemorySlot
    {
        Q,
        W,
        E,
        R
    }

    public readonly struct SlotLayout
    {
        private readonly int _q;
        private readonly int _w;
        private readonly int _e;
        private readonly int _r;

        public SlotLayout(int q, int w, int e, int r)
        {
            _q = q;
            _w = w;
            _e = e;
            _r = r;
        }

        public int this[MemorySlot slot] => slot switch
        {
            MemorySlot.Q => _q,
            MemorySlot.W => _w,
            MemorySlot.E => _e,
            MemorySlot.R => _r,
            _ => throw new System.ArgumentOutOfRangeException(nameof(slot))
        };

        public int Total => _q + _w + _e + _r;

        internal bool IsValid =>
            IsValidCount(_q) && IsValidCount(_w) && IsValidCount(_e) && IsValidCount(_r)
            && Total <= SlotRules.BonusLimit;

        private static bool IsValidCount(int count) => count >= 0 && count <= SlotRules.BonusLimit;
    }

    public readonly struct SlotMove
    {
        internal SlotMove(SlotLayout layout, int sourceLastIndex, int destinationIndex)
        {
            Layout = layout;
            SourceLastIndex = sourceLastIndex;
            DestinationIndex = destinationIndex;
        }

        public SlotLayout Layout { get; }
        public int SourceLastIndex { get; }
        public int DestinationIndex { get; }
    }

    public static class SlotRules
    {
        public const int BaseSlots = 3;
        public const int BonusLimit = 4;

        public static bool CanGrant(SlotLayout layout, MemorySlot slot) =>
            IsMemorySlot(slot) && layout.IsValid && layout.Total < BonusLimit;

        public static bool TryPlanMove(
            SlotLayout layout, MemorySlot source, int sourceIndex, MemorySlot destination, out SlotMove move)
        {
            move = default;
            if (source == destination || !IsMemorySlot(source) || !IsMemorySlot(destination) || !layout.IsValid)
                return false;

            int sourceLastIndex = BaseSlots + layout[source] - 1;
            if (sourceIndex < BaseSlots || sourceIndex > sourceLastIndex)
                return false;

            int MovedCount(MemorySlot slot) => layout[slot] + (slot == destination ? 1 : slot == source ? -1 : 0);
            var updatedLayout = new SlotLayout(
                MovedCount(MemorySlot.Q), MovedCount(MemorySlot.W),
                MovedCount(MemorySlot.E), MovedCount(MemorySlot.R));
            move = new SlotMove(updatedLayout, sourceLastIndex, BaseSlots + layout[destination]);
            return true;
        }

        private static bool IsMemorySlot(MemorySlot slot) => slot >= MemorySlot.Q && slot <= MemorySlot.R;
    }
}
