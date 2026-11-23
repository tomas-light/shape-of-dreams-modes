using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChaosSlots
{
    internal sealed class ChaosBonusLayout : MonoBehaviour
    {
        private static readonly Comparison<SlotPoint> ByX = (first, second) => first.X.CompareTo(second.X);
        private const float LayoutTolerance = 0.02f;
        private const float GapAboveMemory = 5f;
        private const float RingGapAt1080p = 3f;

        private readonly SlotPoint[] _nativePoints = new SlotPoint[SlotRules.BonusLimit];
        private readonly SlotPoint[] _appliedNativePoints = new SlotPoint[SlotRules.BonusLimit];
        private readonly Vector3[] _corners = new Vector3[4];
        private UI_InGame_SkillButton _button;
        private UI_InGame_GemSlot[] _nativeArc;
        private UI_InGame_GemSlot[] _bonusSlots;
        private ChaosSlotHandle[] _handles;
        private Canvas _canvas;
        private RectTransform _normalRing;
        private RectTransform _editingRing;
        private SlotPoint _appliedCenter;
        private float _appliedClearance = float.NaN;
        private float _appliedSpacing;

        internal void Initialize(UI_InGame_SkillButton button, UI_InGame_GemSlot[] nativeArc,
            UI_InGame_GemSlot[] bonusSlots, ChaosSlotHandle[] handles)
        {
            _button = button;
            _nativeArc = nativeArc;
            _bonusSlots = bonusSlots;
            _handles = handles;
            _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            var sample = _bonusSlots[0];
            _normalRing = FindRingRect(sample.normalFrame, (RectTransform)sample.transform);
            _editingRing = FindRingRect(sample.editingFrame, (RectTransform)sample.transform);
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (_button == null)
                return;

            var icon = _button.icon.rectTransform;
            var center = ToPoint(transform.InverseTransformPoint(WorldCenter(icon)));
            for (int i = 0; i < _nativeArc.Length; i++)
                _nativePoints[i] = ToPoint(transform.InverseTransformPoint(WorldCenter(VisualRect(_nativeArc[i]))));
            Array.Sort(_nativePoints, ByX);

            float memoryTop = Mathf.Max(center.Y, VerticalBound(icon, upper: true));
            memoryTop = GetVisibleFrameTop(_button.activeAbilityFrame, memoryTop);
            memoryTop = GetVisibleFrameTop(_button.passiveAbilityFrame, memoryTop);
            memoryTop = GetVisibleFrameTop(_button.noAbilityFrame, memoryTop);

            float lowerExtent = 0f;
            foreach (var slot in _bonusSlots)
            {
                var rect = VisualRect(slot);
                float y = transform.InverseTransformPoint(WorldCenter(rect)).y;
                float bottom = Mathf.Min(VerticalBound(rect, upper: false),
                    VerticalBound((RectTransform)slot.transform, upper: false));
                lowerExtent = Mathf.Max(lowerExtent, y - bottom);
            }
            var edit = ManagerBase<EditSkillManager>.softInstance;
            bool editing = edit != null && edit.mode != EditSkillManager.ModeType.None;
            float ringRadius = HorizontalRadius(editing ? _editingRing : _normalRing);
            int count = _bonusSlots.Length;
            bool circular = count == SlotRules.BonusLimit;
            float clearance = memoryTop - center.Y + (circular ? Mathf.Max(lowerExtent, ringRadius) : lowerExtent)
                + GapAboveMemory;
            float spacing = BonusSlotArc.MinimumHorizontalSpacingForRings(_nativePoints, count,
                2f * ringRadius + LocalRingGap());
            if (!GeometryChanged(center, clearance, spacing))
                return;

            var desired = BonusSlotArc.BuildUpper(_nativePoints, center, count, clearance, spacing, circular);
            for (int i = 0; i < _bonusSlots.Length; i++)
            {
                var slot = _bonusSlots[i];
                var visualCenter = WorldCenter(VisualRect(slot));
                float depth = transform.InverseTransformPoint(visualCenter).z;
                var target = transform.TransformPoint(desired[i].X, desired[i].Y, depth);
                slot.transform.position += target - visualCenter;

                float gap = float.PositiveInfinity;
                if (i > 0)
                    gap = desired[i].X - desired[i - 1].X;
                if (i + 1 < desired.Count)
                    gap = Mathf.Min(gap, desired[i + 1].X - desired[i].X);
                if (gap < float.PositiveInfinity)
                {
                    var width = transform.TransformVector(new Vector3(gap * 0.86f, 0f, 0f));
                    _handles[i].SetMaximumWidth(slot.transform.InverseTransformVector(width).magnitude);
                }
            }
            _appliedCenter = center;
            _appliedClearance = clearance;
            _appliedSpacing = spacing;
            Array.Copy(_nativePoints, _appliedNativePoints, _nativePoints.Length);
        }

        private bool GeometryChanged(SlotPoint center, float clearance, float spacing)
        {
            if (float.IsNaN(_appliedClearance) || Different(_appliedClearance, clearance)
                || Different(_appliedSpacing, spacing)
                || PointChanged(center, _appliedCenter))
                return true;
            for (int i = 0; i < _nativePoints.Length; i++)
                if (PointChanged(_nativePoints[i], _appliedNativePoints[i]))
                    return true;
            return false;
        }

        private RectTransform FindRingRect(CanvasGroup frame, RectTransform fallback)
        {
            if (frame == null)
                return fallback;

            float largestRadius = 0f;
            foreach (var image in frame.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null)
                    continue;
                var rect = image.rectTransform;
                float radius = HorizontalRadius(rect);
                if (radius > largestRadius)
                {
                    largestRadius = radius;
                    fallback = rect;
                }
            }
            return fallback;
        }

        private float HorizontalRadius(RectTransform rect)
        {
            rect.GetWorldCorners(_corners);
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            foreach (var corner in _corners)
            {
                float x = transform.InverseTransformPoint(corner).x;
                minimum = Mathf.Min(minimum, x);
                maximum = Mathf.Max(maximum, x);
            }
            return (maximum - minimum) * 0.5f;
        }

        private float LocalRingGap()
        {
            var camera = _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : _canvas.worldCamera;
            var start = RectTransformUtility.WorldToScreenPoint(camera, transform.position);
            var end = RectTransformUtility.WorldToScreenPoint(camera, transform.TransformPoint(Vector3.right));
            float pixelsPerUnit = (end - start).magnitude;
            return pixelsPerUnit > 0.001f ? RingGapAt1080p * Screen.height / 1080f / pixelsPerUnit : 0f;
        }

        private float GetVisibleFrameTop(GameObject frame, float previousTop) =>
            frame != null && frame.activeSelf && frame.transform is RectTransform rect
                ? Mathf.Max(previousTop, VerticalBound(rect, upper: true))
                : previousTop;

        private float VerticalBound(RectTransform rect, bool upper)
        {
            rect.GetWorldCorners(_corners);
            float bound = transform.InverseTransformPoint(_corners[0]).y;
            for (int i = 1; i < _corners.Length; i++)
            {
                float y = transform.InverseTransformPoint(_corners[i]).y;
                bound = upper ? Mathf.Max(bound, y) : Mathf.Min(bound, y);
            }
            return bound;
        }

        private static RectTransform VisualRect(UI_InGame_GemSlot slot) => slot.gemIconImage.rectTransform;
        private static Vector3 WorldCenter(RectTransform rect) => rect.TransformPoint(rect.rect.center);
        private static SlotPoint ToPoint(Vector3 point) => new SlotPoint(point.x, point.y);
        private static bool PointChanged(SlotPoint left, SlotPoint right) => Different(left.X, right.X) || Different(left.Y, right.Y);
        private static bool Different(float left, float right) => Mathf.Abs(left - right) > LayoutTolerance;
    }
}
