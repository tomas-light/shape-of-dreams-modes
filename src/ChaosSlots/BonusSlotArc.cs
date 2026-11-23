using System;
using System.Collections.Generic;

namespace ChaosSlots
{
    public readonly struct SlotPoint
    {
        public SlotPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }
    }

    public static class BonusSlotArc
    {
        public static IReadOnlyList<SlotPoint> BuildUpper(
            IReadOnlyList<SlotPoint> lowerFour, SlotPoint center, int bonusCount, float clearance)
            => BuildUpper(lowerFour, center, bonusCount, clearance, 0f, useCircularClearance: false);

        public static IReadOnlyList<SlotPoint> BuildUpper(
            IReadOnlyList<SlotPoint> lowerFour, SlotPoint center, int bonusCount,
            float circularClearance, float minimumHorizontalSpacing)
            => BuildUpper(lowerFour, center, bonusCount, circularClearance, minimumHorizontalSpacing,
                useCircularClearance: true);

        internal static IReadOnlyList<SlotPoint> BuildUpper(
            IReadOnlyList<SlotPoint> lowerFour, SlotPoint center, int bonusCount,
            float clearance, float minimumHorizontalSpacing, bool useCircularClearance)
        {
            if (lowerFour == null)
                throw new ArgumentNullException(nameof(lowerFour));
            if (lowerFour.Count != SlotRules.BonusLimit)
                throw new ArgumentException("The native arc must have four slot centers.", nameof(lowerFour));
            if (bonusCount < 0 || bonusCount > SlotRules.BonusLimit)
                throw new ArgumentOutOfRangeException(nameof(bonusCount));
            if (clearance < 0f || float.IsNaN(clearance) || float.IsInfinity(clearance))
                throw new ArgumentOutOfRangeException(nameof(clearance));
            if (minimumHorizontalSpacing < 0f || float.IsNaN(minimumHorizontalSpacing)
                || float.IsInfinity(minimumHorizontalSpacing))
                throw new ArgumentOutOfRangeException(nameof(minimumHorizontalSpacing));
            if (bonusCount == 0)
                return Array.Empty<SlotPoint>();

            var points = new SlotPoint[bonusCount];
            float offset = (lowerFour.Count - bonusCount) * 0.5f;
            for (int i = 0; i < points.Length; i++)
            {
                var sample = Sample(lowerFour, offset + i);
                points[i] = new SlotPoint(sample.X, 2f * center.Y - sample.Y);
            }

            float midpoint = (points[0].X + points[points.Length - 1].X) * 0.5f;
            float minimumGap = float.PositiveInfinity;
            for (int i = 1; i < points.Length; i++)
                minimumGap = Math.Min(minimumGap, points[i].X - points[i - 1].X);
            float widthScale = minimumGap > 0f ? Math.Max(1f, minimumHorizontalSpacing / minimumGap) : 1f;
            float yShift = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                float x = minimumGap > 0f || minimumHorizontalSpacing == 0f
                    ? (points[i].X - midpoint) * widthScale
                    : (i - (points.Length - 1) * 0.5f) * minimumHorizontalSpacing;
                float requiredY = useCircularClearance
                    ? PerpendicularClearance(clearance, x)
                    : clearance;
                yShift = Math.Max(yShift, center.Y + requiredY - points[i].Y);
                points[i] = new SlotPoint(center.X + x, points[i].Y);
            }
            for (int i = 0; i < points.Length; i++)
                points[i] = new SlotPoint(points[i].X, points[i].Y + yShift);
            return points;
        }

        internal static float MinimumHorizontalSpacingForRings(
            IReadOnlyList<SlotPoint> lowerFour, int bonusCount, float minimumCenterDistance)
        {
            if (bonusCount < 2)
                return 0f;

            float offset = (lowerFour.Count - bonusCount) * 0.5f;
            var previous = Sample(lowerFour, offset);
            float minimumGap = float.PositiveInfinity;
            float widthScale = 1f;
            for (int i = 1; i < bonusCount; i++)
            {
                var point = Sample(lowerFour, offset + i);
                float gap = point.X - previous.X;
                if (gap <= 0f)
                    return minimumCenterDistance;
                float neededGap = PerpendicularClearance(minimumCenterDistance, point.Y - previous.Y);
                widthScale = Math.Max(widthScale, neededGap / gap);
                minimumGap = Math.Min(minimumGap, gap);
                previous = point;
            }
            return minimumGap * widthScale;
        }

        private static SlotPoint Sample(IReadOnlyList<SlotPoint> points, float index)
        {
            int leftIndex = (int)index;
            var left = points[leftIndex];
            var right = points[Math.Min(leftIndex + 1, points.Count - 1)];
            float fraction = index - leftIndex;
            return new SlotPoint(left.X + (right.X - left.X) * fraction, left.Y + (right.Y - left.Y) * fraction);
        }

        private static float PerpendicularClearance(float radius, float offset)
            => (float)Math.Sqrt(Math.Max(0f, radius * radius - offset * offset));
    }
}
