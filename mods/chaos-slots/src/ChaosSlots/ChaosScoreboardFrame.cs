using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChaosSlots
{
    internal sealed class ChaosScoreboardFrame : MonoBehaviour
    {
        private readonly List<ChaosScoreboardSlots> _memories = new();
        private RectTransform _frame;
        private LayoutElement _playerRow;
        private float _originalFrameHeight;
        private float _originalRowHeight;
        private float _extraHeight;

        internal static ChaosScoreboardFrame GetOrCreate(UI_InGame_Scoreboard_PlayerItem_Skill memory)
        {
            var row = memory.GetComponentInParent<UI_InGame_Scoreboard_PlayerItem>(true);
            if (row == null || !(memory.transform.parent is RectTransform frame) || frame.parent != row.transform)
                return null;
            var existing = frame.GetComponent<ChaosScoreboardFrame>();
            if (existing != null)
                return existing;
            var layout = row.GetComponent<LayoutElement>();
            if (layout == null || layout.preferredHeight < 0f)
                return null;

            var result = frame.gameObject.AddComponent<ChaosScoreboardFrame>();
            result._frame = frame;
            result._playerRow = layout;
            result._originalFrameHeight = frame.rect.height;
            result._originalRowHeight = layout.preferredHeight;
            return result;
        }

        internal void Track(ChaosScoreboardSlots slots) => _memories.Add(slots);

        internal float StockTop(RectTransform memory) =>
            memory.InverseTransformPoint(_frame.TransformPoint(0f, _frame.rect.yMax - _extraHeight, 0f)).y;

        internal float ToFrameHeight(RectTransform memory, float height) =>
            _frame.InverseTransformVector(memory.TransformVector(0f, height, 0f)).y;

        private void LateUpdate() => SetExtraHeight(ScoreboardSlotLayout.RowExtraHeight(_memories,
            static slots => slots != null && slots.isActiveAndEnabled ? slots.ExtraHeight : 0f));

        private void SetExtraHeight(float extra)
        {
            if (_extraHeight == extra)
                return;
            float delta = extra - _extraHeight;
            _extraHeight = extra;
            _playerRow.preferredHeight = _originalRowHeight + extra;
            _frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _originalFrameHeight + extra);

            // The frame is top-anchored. Keep its centered foreground aligned with the bottom edge.
            foreach (RectTransform child in _frame)
                if (child.anchorMin.y == 0.5f && child.anchorMax.y == 0.5f)
                    child.anchoredPosition += Vector2.down * (delta * 0.5f);
        }

        internal static void RestoreAll()
        {
            foreach (var frame in FindObjectsByType<ChaosScoreboardFrame>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (frame._playerRow != null)
                    frame.SetExtraHeight(0f);
                Destroy(frame);
            }
        }
    }
}
