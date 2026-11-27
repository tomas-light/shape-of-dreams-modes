using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ChaosSlots
{
    internal static class ScoreboardPeers
    {
        private const string Separator = ",";

        internal static string Format(IEnumerable<uint> playerIds) =>
            Separator + string.Join(Separator, playerIds.Where(id => id != 0u)
                .Select(id => id.ToString(CultureInfo.InvariantCulture))) + Separator;

        internal static bool Contains(string peers, uint playerId) =>
            playerId != 0u && peers != null &&
            peers.IndexOf(Separator + playerId.ToString(CultureInfo.InvariantCulture) + Separator,
                StringComparison.Ordinal) >= 0;
    }
}
