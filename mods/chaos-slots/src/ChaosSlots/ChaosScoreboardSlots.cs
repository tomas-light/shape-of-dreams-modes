using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using ScoreboardGem = UI_InGame_Scoreboard_PlayerItem_Skill_Gem;
using ScoreboardMemory = UI_InGame_Scoreboard_PlayerItem_Skill;

namespace ChaosSlots
{
    [HarmonyPatch(typeof(ScoreboardMemory), "UpdateInfo")]
    internal sealed class ChaosScoreboardSlots : MonoBehaviour
    {
        private static readonly MethodInfo UpdateGemInfo = AccessTools.Method(typeof(ScoreboardGem), "UpdateInfo");

        private readonly float[] _extraHeights = new float[SlotRules.BonusLimit];
        private readonly GameObject[] _bonusGroups = new GameObject[SlotRules.BonusLimit];
        private readonly List<UnityAction> _gemUpdates = new();
        private UI_InGame_ScoreboardView _view;
        private ScoreboardMemory _memory;
        private GameObject[] _originalGroups;
        private GameObject[] _expandedGroups;
        internal float ExtraHeight { get; private set; }

        private static void Prefix(ScoreboardMemory __instance)
        {
            if (!ChaosSlotState.TryMemorySlot(__instance.type, out _))
                return;

            var hero = __instance.hero;
            if (hero == null || !hero.isActive)
                return;

            int capacity = hero.Skill.GetMaxGemCount(__instance.type);
            bool usesExpandedFour = capacity == SlotRules.BaseSlots + 1
                && ChaosSlotNetwork.UsesExpandedScoreboardLayout(hero.owner)
                && ChaosSlotState.FindBonus(hero) != null;
            int bonusCount = ScoreboardSlotLayout.BonusCount(capacity, usesExpandedFour);
            var slots = __instance.GetComponent<ChaosScoreboardSlots>();
            if (slots == null && bonusCount > 0)
            {
                slots = __instance.gameObject.AddComponent<ChaosScoreboardSlots>();
                slots.Initialize(__instance);
            }

            if (slots != null && slots._memory != null)
                slots.SelectLayout(bonusCount);
        }

        private void SelectLayout(int bonusCount)
        {
            int index = ScoreboardSlotLayout.FourSlotGroup;
            var four = bonusCount == 1 ? _bonusGroups[0] : _originalGroups[index];
            if (_expandedGroups[index] != four)
            {
                _expandedGroups[index].SetActive(false);
                _expandedGroups[index] = four;
            }
            ExtraHeight = bonusCount == 0 ? 0f : _extraHeights[bonusCount - 1];
        }

        private void Initialize(ScoreboardMemory memory)
        {
            var groups = memory.gemObjects234;
            if (groups == null || groups.Length != ScoreboardSlotLayout.FourSlotGroup + 1
                || Array.Exists(groups, group => group == null))
            {
                WarnUnavailable(memory, "expected the native groups for two, three, and four slots");
                return;
            }
            if (memory.skillIcon == null || UpdateGemInfo == null)
            {
                WarnUnavailable(memory, memory.skillIcon == null ? "missing the memory icon" : "missing the native gem refresh method");
                return;
            }

            var template = groups[ScoreboardSlotLayout.ThreeSlotGroup];
            var baseSlots = template.GetComponentsInChildren<ScoreboardGem>(true);
            var sample = Array.Find(baseSlots, gem => gem.index == 1);
            var lowerFour = groups[ScoreboardSlotLayout.FourSlotGroup].GetComponentsInChildren<ScoreboardGem>(true);
            if (baseSlots.Length != SlotRules.BaseSlots || sample == null || !(sample.transform is RectTransform)
                || lowerFour.Length != SlotRules.BonusLimit || Array.Exists(lowerFour, gem => !(gem.transform is RectTransform)))
            {
                WarnUnavailable(memory, "expected a three-slot template with a middle slot and a four-slot reference arc");
                return;
            }

            var frame = ChaosScoreboardFrame.GetOrCreate(memory);
            if (frame == null)
            {
                WarnUnavailable(memory, "expected a memory frame on a player row with a preferred height");
                return;
            }

            _view = memory.GetComponentInParent<UI_InGame_ScoreboardView>(true);
            _originalGroups = groups;
            _expandedGroups = new GameObject[ScoreboardSlotLayout.GroupCount];
            Array.Copy(groups, _expandedGroups, groups.Length);

            var icon = memory.skillIcon.rectTransform;
            var arc = new SlotPoint[SlotRules.BonusLimit];
            for (int i = 0; i < arc.Length; i++)
            {
                var rect = (RectTransform)lowerFour[i].transform;
                var point = icon.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
                arc[i] = new SlotPoint(point.x, point.y);
            }
            Array.Sort(arc, (left, right) => left.X.CompareTo(right.X));

            // Reserve space for the charge badge even while it is hidden by the current memory.
            ScoreboardChargeBadge? chargeBadge = null;
            if (memory.multipleChargesObject != null && memory.multipleChargesObject.transform is RectTransform badge)
            {
                var left = icon.InverseTransformPoint(badge.TransformPoint(badge.rect.min));
                var right = icon.InverseTransformPoint(badge.TransformPoint(badge.rect.max));
                chargeBadge = new ScoreboardChargeBadge(left.x, right.x, right.y);
            }

            // Stage the groups inactive so native OnEnable reads the assigned slot indices.
            var staging = new GameObject("Chaos Slots scoreboard setup", typeof(RectTransform));
            staging.SetActive(false);
            try
            {
                for (int count = 1; count <= SlotRules.BonusLimit; count++)
                    CreateGroup(template, staging.transform, icon, arc, frame, count, chargeBadge);
            }
            catch (Exception error)
            {
                DestroyGroups();
                Debug.LogException(error, memory);
                return;
            }
            finally
            {
                Destroy(staging);
            }

            // The stock update selects a group by capacity and handles activation on every refresh.
            _memory = memory;
            memory.gemObjects234 = _expandedGroups;
            frame.Track(this);
        }

        private static void WarnUnavailable(ScoreboardMemory memory, string reason) =>
            Debug.LogWarning($"Chaos Slots scoreboard ({memory.type}): {reason}.", memory);

        private void CreateGroup(GameObject template, Transform staging, RectTransform memory,
            SlotPoint[] arc, ChaosScoreboardFrame frame, int bonusCount, ScoreboardChargeBadge? chargeBadge)
        {
            int capacity = SlotRules.BaseSlots + bonusCount;
            var group = Instantiate(template, staging, false);
            _bonusGroups[bonusCount - 1] = group;
            if (bonusCount > 1)
                _expandedGroups[ScoreboardSlotLayout.GroupIndex(capacity)] = group;
            group.name = $"Chaos Slots {capacity}";
            group.SetActive(false);
            group.transform.SetParent(template.transform.parent, false);

            var baseSlots = group.GetComponentsInChildren<ScoreboardGem>(true);
            foreach (var slot in baseSlots)
                TrackGemUpdate(slot);
            var sample = Array.Find(baseSlots, slot => slot.index == 1);
            var sampleRect = (RectTransform)sample.transform;
            float diameter = memory.InverseTransformVector(sampleRect.TransformVector(
                Vector3.right * sampleRect.rect.width)).magnitude;
            var center = new SlotPoint(memory.rect.center.x, memory.rect.center.y);
            var positions = ScoreboardSlotLayout.PositionBonusSlots(arc, center, bonusCount, memory.rect.height,
                diameter, chargeBadge);
            for (int i = 0; i < positions.Count; i++)
            {
                var gem = Instantiate(sample, sample.transform.parent, false);
                gem.index = ScoreboardSlotLayout.BonusSlotIndex(i);
                var rect = (RectTransform)gem.transform;
                var target = memory.TransformPoint(new Vector3(positions[i].X, positions[i].Y, 0f));
                rect.position += target - rect.TransformPoint(rect.rect.center);
                TrackGemUpdate(gem);
            }

            float extra = ScoreboardSlotLayout.ExtraHeight(positions, diameter, memory.rect.height, frame.StockTop(memory));
            _extraHeights[bonusCount - 1] = frame.ToFrameHeight(memory, extra);
        }

        private void TrackGemUpdate(ScoreboardGem gem) =>
            _gemUpdates.Add((UnityAction)UpdateGemInfo.CreateDelegate(typeof(UnityAction), gem));

        private void OnDestroy()
        {
            if (_view != null)
                foreach (var update in _gemUpdates)
                    _view.onShow.RemoveListener(update);
        }

        private void DestroyGroups()
        {
            foreach (var group in _bonusGroups)
            {
                if (group == null)
                    continue;
                group.SetActive(false);
                Destroy(group);
            }
        }

        internal static void RestoreAll()
        {
            foreach (var slots in FindObjectsByType<ChaosScoreboardSlots>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                slots.DestroyGroups();
                var memory = slots._memory;
                if (memory != null)
                {
                    memory.gemObjects234 = slots._originalGroups;
                    var hero = memory.hero;
                    if (hero != null && hero.isActive)
                    {
                        int index = ScoreboardSlotLayout.GroupIndex(hero.Skill.GetMaxGemCount(memory.type));
                        for (int i = 0; i < slots._originalGroups.Length; i++)
                            slots._originalGroups[i].SetActive(i == index);
                    }
                }
                Destroy(slots);
            }
        }
    }
}
