using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ChaosSlots
{
    [HarmonyPatch(typeof(UI_InGame_ResultView), "Refresh")]
    internal sealed class ChaosResultFrame : MonoBehaviour
    {
        private const int ExtraTopPadding = 52;

        private HorizontalLayoutGroup _layout;
        private RectTransform _row;
        private int _originalTopPadding;
        private float _originalHeight;
        private bool _expanded;

        private static void Postfix(UI_InGame_ResultView __instance)
        {
            var slots = __instance.GetComponentsInChildren<ChaosResultSlots>(true);
            if (slots.Length == 0 || !(slots[0].transform.parent?.parent is RectTransform row))
                return;

            bool hasBonus = Array.Exists(slots, layout => layout.HasBonusSlots);
            var frame = row.GetComponent<ChaosResultFrame>();
            if (frame == null)
            {
                var layout = row.GetComponent<HorizontalLayoutGroup>();
                if (!hasBonus || layout == null)
                    return;

                frame = row.gameObject.AddComponent<ChaosResultFrame>();
                frame._layout = layout;
                frame._row = row;
                frame._originalTopPadding = layout.padding.top;
                frame._originalHeight = row.rect.height;
            }

            frame.SetExpanded(hasBonus);
        }

        private void SetExpanded(bool expanded)
        {
            if (_expanded == expanded)
                return;

            _expanded = expanded;
            int extra = expanded ? ExtraTopPadding : 0;
            _layout.padding.top = _originalTopPadding + extra;
            _row.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _originalHeight + extra);
            // The frame stretches with the row. The native layout moves every memory down together.
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_row.parent);
        }

        internal static void RestoreAll()
        {
            foreach (var frame in FindObjectsByType<ChaosResultFrame>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (frame._layout != null && frame._row != null)
                    frame.SetExpanded(false);
                Destroy(frame);
            }
        }
    }
}
