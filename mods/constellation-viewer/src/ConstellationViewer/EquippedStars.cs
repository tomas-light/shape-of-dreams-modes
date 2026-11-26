using System;
using System.Collections.Generic;

namespace ConstellationViewer
{
    internal static class EquippedStars
    {
        private static readonly StarType[] Categories =
        {
            StarType.Destruction, StarType.Life, StarType.Imagination, StarType.Flexible
        };

        public static IEnumerable<(StarType Category, LoadoutStarItem Entry)> Enumerate(HeroLoadoutData loadout)
        {
            foreach (var category in Categories)
            {
                var slots = loadout.GetStarList(category);
                if (slots == null)
                    continue;

                foreach (var slot in slots)
                {
                    if (!string.IsNullOrEmpty(slot.name) && slot.level != 0)
                        yield return (category, slot);
                }
            }
        }

        public static int GetDisplayLevel(LoadoutStarItem entry, int maxStarLevel) =>
            Math.Min(Math.Max(entry.level, 1), Math.Max(maxStarLevel, 1));

        // The game restores saved skills and constellation effects separately from the hero's loadout.
        public static bool IsRestoredLoadout(HeroLoadoutData loadout) =>
            loadout.skillQ == -1 && loadout.skillR == -1 && loadout.skillTrait == -1 && loadout.skillMovement == -1;

        public static float GetStrength(StarType slot, StarType star) =>
            slot == star || slot == StarType.Flexible || star == StarType.Flexible ? 1f : 0.5f;
    }
}
