using System;
using System.Collections.Generic;

namespace ChaosSlots.QaRoom
{
    public enum FixtureKind
    {
        Shrine,
        Essence,
        Memory
    }

    public readonly struct FixturePlacement
    {
        internal FixturePlacement(FixtureKind kind, int index, float x, float z)
        {
            Kind = kind;
            Index = index;
            X = x;
            Z = z;
        }

        public FixtureKind Kind { get; }
        public int Index { get; }
        public float X { get; }
        public float Z { get; }
    }

    public static class TestRoomPlan
    {
        public const int ShrineCount = 5;
        public const int EssenceCount = 8;
        public const int MemoryCount = 2;

        private static readonly IReadOnlyList<FixturePlacement> Placements = BuildPlacements(MemoryCount);

        public static IReadOnlyList<FixturePlacement> GetPlacements() => Placements;

        public static IReadOnlyList<FixturePlacement> GetPlacements(int availableMemoryCount)
        {
            if (availableMemoryCount < 0 || availableMemoryCount > MemoryCount)
                throw new ArgumentOutOfRangeException(nameof(availableMemoryCount));

            return availableMemoryCount == MemoryCount ? Placements : BuildPlacements(availableMemoryCount);
        }

        private static IReadOnlyList<FixturePlacement> BuildPlacements(int memoryCount)
        {
            var placements = new List<FixturePlacement>(ShrineCount + EssenceCount + memoryCount);
            for (int i = 0; i < ShrineCount; i++)
                placements.Add(new FixturePlacement(FixtureKind.Shrine, i, (i - 2) * 2.8f, 5.5f));
            for (int i = 0; i < EssenceCount; i++)
                placements.Add(new FixturePlacement(FixtureKind.Essence, i,
                    (i % 4 - 1.5f) * 1.5f, i < 4 ? 2.2f : -2.2f));
            for (int i = 0; i < memoryCount; i++)
                placements.Add(new FixturePlacement(FixtureKind.Memory, i, i == 0 ? -4.4f : 4.4f, 1.6f));
            return Array.AsReadOnly(placements.ToArray());
        }
    }
}
