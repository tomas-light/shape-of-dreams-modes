using System;
using HarmonyLib;
using UnityEngine;

namespace ChaosSlots
{
    [HarmonyPatch(typeof(UI_InGame_Result_HeroSkillItem), nameof(UI_InGame_Result_HeroSkillItem.UpdateAndGetScore))]
    internal sealed class ChaosResultSlots : MonoBehaviour
    {
        private const int ThreeSlotGroup = SlotRules.BaseSlots - 2;
        private const int FourSlotGroup = SlotRules.BaseSlots - 1;
        private const float GapAboveMemory = 0.065f;
        private const float RingGap = 0.04f;

        private readonly UI_InGame_Result_HeroGemItem[] _slots = new UI_InGame_Result_HeroGemItem[SlotRules.BonusLimit];
        private UI_InGame_Result_HeroSkillItem _item;
        private UI_InGame_Result_HeroGemItem _template;
        private UI_InGame_Result_HeroGemItem[] _nativeFour;
        private int _capacity;

        internal bool HasBonusSlots => _capacity > SlotRules.BaseSlots && _capacity <= SlotRules.BaseSlots + SlotRules.BonusLimit;

        private static void Postfix(UI_InGame_Result_HeroSkillItem __instance, DewGameResult data,
            int playerIndex, float scoreMultiplier)
        {
            if (!ChaosSlotState.TryMemorySlot(__instance.type, out _))
                return;

            int capacity = data.players[playerIndex].maxGemCounts[(int)__instance.type];
            var layout = __instance.GetComponent<ChaosResultSlots>();
            int bonusCount = capacity - SlotRules.BaseSlots;
            if (bonusCount <= 0 || bonusCount > SlotRules.BonusLimit)
            {
                if (layout != null)
                {
                    layout._capacity = capacity;
                    layout.HideSlots();
                }
                return;
            }

            if (layout == null)
            {
                var groups = __instance.gemObjects234;
                if (groups == null || groups.Length != FourSlotGroup + 1 || groups[ThreeSlotGroup] == null
                    || groups[FourSlotGroup] == null || __instance.skillIcon == null)
                    return;

                var template = groups[ThreeSlotGroup].GetComponentInChildren<UI_InGame_Result_HeroGemItem>(true);
                var nativeFour = groups[FourSlotGroup].GetComponentsInChildren<UI_InGame_Result_HeroGemItem>(true);
                if (template == null || template.icon == null || template.parent != __instance
                    || nativeFour.Length != SlotRules.BonusLimit || Array.Exists(nativeFour, slot => slot.icon == null))
                    return;

                layout = __instance.gameObject.AddComponent<ChaosResultSlots>();
                layout._item = __instance;
                layout._template = template;
                layout._nativeFour = nativeFour;
            }

            layout._capacity = capacity;
            layout.Show(data, playerIndex, scoreMultiplier, bonusCount);
        }

        private void Show(DewGameResult data, int playerIndex, float scoreMultiplier, int bonusCount)
        {
            var groups = _item.gemObjects234;
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (i >= bonusCount)
                {
                    if (slot != null)
                        slot.gameObject.SetActive(false);
                    continue;
                }

                if (slot == null)
                {
                    // Keep the native parent lookup and tooltip behavior.
                    slot = Instantiate(_template, _template.transform.parent, false);
                    slot.index = SlotRules.BaseSlots + i;
                    _slots[i] = slot;
                }

                // ResultView enumerates the stat items before invoking the skill item.
                // Newly added gems must be bound during this refresh.
                slot.UpdateAndGetScore(data, playerIndex, scoreMultiplier);
                slot.gameObject.SetActive(true);
            }

            groups[FourSlotGroup].SetActive(false);
            groups[ThreeSlotGroup].SetActive(true);
            PositionSlots(bonusCount);
        }

        private void PositionSlots(int bonusCount)
        {
            var memory = _item.skillIcon.rectTransform;
            var lowerFour = new SlotPoint[SlotRules.BonusLimit];
            for (int i = 0; i < lowerFour.Length; i++)
            {
                var icon = _nativeFour[i].icon.rectTransform;
                var point = memory.InverseTransformPoint(icon.TransformPoint(icon.rect.center));
                lowerFour[i] = new SlotPoint(point.x, point.y);
            }
            Array.Sort(lowerFour, (left, right) => left.X.CompareTo(right.X));

            var center = new SlotPoint(memory.rect.center.x, memory.rect.center.y);
            var sample = _slots[0].icon.rectTransform;
            float diameter = memory.InverseTransformVector(sample.TransformVector(Vector3.right * sample.rect.width)).magnitude;
            float clearance = memory.rect.height * 0.5f + diameter * 0.5f + memory.rect.height * GapAboveMemory;
            float spacing = BonusSlotArc.MinimumHorizontalSpacingForRings(lowerFour, bonusCount,
                diameter + memory.rect.width * RingGap);
            var positions = BonusSlotArc.BuildUpper(lowerFour, center, bonusCount, clearance, spacing,
                useCircularClearance: bonusCount == SlotRules.BonusLimit);
            for (int i = 0; i < bonusCount; i++)
            {
                var slot = _slots[i];
                var image = slot.icon.rectTransform;
                var target = memory.TransformPoint(new Vector3(positions[i].X, positions[i].Y, 0f));
                slot.transform.position += target - image.TransformPoint(image.rect.center);
            }
        }

        private void HideSlots()
        {
            foreach (var slot in _slots)
                if (slot != null)
                    slot.gameObject.SetActive(false);
        }

        internal static void RestoreAll()
        {
            foreach (var layout in FindObjectsByType<ChaosResultSlots>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                layout.HideSlots();
                foreach (var slot in layout._slots)
                    if (slot != null)
                        Destroy(slot.gameObject);

                if (layout._item != null)
                {
                    var groups = layout._item.gemObjects234;
                    groups[ThreeSlotGroup].SetActive(layout._capacity == SlotRules.BaseSlots);
                    groups[FourSlotGroup].SetActive(layout._capacity == SlotRules.BaseSlots + 1);
                }
                Destroy(layout);
            }
        }
    }
}
