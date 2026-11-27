using System;
using System.Collections.Generic;

namespace ChaosSlots
{
    internal readonly struct ScoreboardChargeBadge
    {
        internal ScoreboardChargeBadge(float left, float right, float top)
        {
            Left = left;
            Right = right;
            Top = top;
        }

        internal float Left { get; }
        internal float Right { get; }
        internal float Top { get; }
    }

    internal static class ScoreboardSlotLayout
    {
        private const int FirstNativeCapacity = 2;
        private const float MemoryGap = 0.065f;
        private const float RingGap = 0.04f;
        internal const int ThreeSlotGroup = SlotRules.BaseSlots - FirstNativeCapacity;
        internal const int FourSlotGroup = ThreeSlotGroup + 1;
        internal const int GroupCount = ThreeSlotGroup + 1 + SlotRules.BonusLimit;

        internal static int GroupIndex(int capacity) => capacity - FirstNativeCapacity;

        internal static int BonusCount(int capacity, bool usesExpandedFour)
        {
            int bonuses = capacity - SlotRules.BaseSlots;
            return bonuses <= SlotRules.BonusLimit && (bonuses > 1 || (bonuses == 1 && usesExpandedFour)) ? bonuses : 0;
        }

        internal static int BonusSlotIndex(int offset) => SlotRules.BaseSlots + offset;

        internal static IReadOnlyList<SlotPoint> PositionBonusSlots(
            IReadOnlyList<SlotPoint> lowerFour, SlotPoint center, int bonusCount, float memoryHeight, float diameter,
            ScoreboardChargeBadge? chargeBadge = null)
        {
            float clearance = memoryHeight * (0.5f + MemoryGap) + diameter * 0.5f;
            float gap = memoryHeight * RingGap;
            float spacing = BonusSlotArc.MinimumHorizontalSpacingForRings(lowerFour, bonusCount, diameter + gap);
            var positions = BonusSlotArc.BuildUpper(lowerFour, center, bonusCount, clearance, spacing,
                useCircularClearance: bonusCount == SlotRules.BonusLimit);
            if (!chargeBadge.HasValue)
                return positions;

            var badge = chargeBadge.Value;
            float radius = diameter * 0.5f + gap;
            float lift = 0f;
            foreach (var point in positions)
            {
                float horizontalDistance = Math.Max(0f, Math.Max(badge.Left - point.X, point.X - badge.Right));
                if (horizontalDistance >= radius)
                    continue;
                float verticalClearance = (float)Math.Sqrt(radius * radius - horizontalDistance * horizontalDistance);
                lift = Math.Max(lift, badge.Top + verticalClearance - point.Y);
            }
            if (lift <= 0f)
                return positions;

            var lifted = new SlotPoint[positions.Count];
            for (int i = 0; i < lifted.Length; i++)
                lifted[i] = new SlotPoint(positions[i].X, positions[i].Y + lift);
            return lifted;
        }

        internal static float ExtraHeight(IReadOnlyList<SlotPoint> positions, float diameter,
            float memoryHeight, float stockFrameTop)
        {
            float extraHeight = 0f;
            foreach (var point in positions)
                extraHeight = Math.Max(extraHeight, point.Y + diameter * 0.5f + memoryHeight * MemoryGap - stockFrameTop);
            return extraHeight;
        }

        internal static float RowExtraHeight<T>(IReadOnlyList<T> memories, Func<T, float> extraHeight)
        {
            float extra = 0f;
            for (int i = 0; i < memories.Count; i++)
                extra = Math.Max(extra, extraHeight(memories[i]));
            return extra;
        }
    }
}
