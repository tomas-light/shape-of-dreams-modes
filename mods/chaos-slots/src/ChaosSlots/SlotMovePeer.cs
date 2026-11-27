namespace ChaosSlots
{
    internal sealed class SlotMovePeer
    {
        internal const byte EnabledVersion = 1;
        internal const byte DisabledVersion = 0;

        private ulong _lastSequence;
        internal bool Enabled { get; private set; }
        internal uint PublishedPlayerId { get; set; }

        internal bool UpdateVersion(byte version)
        {
            if (version != EnabledVersion && version != DisabledVersion)
                return false;
            bool enabled = version == EnabledVersion;
            if (Enabled == enabled)
                return false;
            Enabled = enabled;
            return true;
        }

        internal bool TryAcceptSequence(ulong sequence, out ulong previousSequence)
        {
            previousSequence = _lastSequence;
            if (!Enabled || sequence <= previousSequence)
                return false;
            _lastSequence = sequence;
            return true;
        }
    }
}
