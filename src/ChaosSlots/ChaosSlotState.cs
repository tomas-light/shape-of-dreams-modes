using System;
using Mirror;
using UnityEngine;

namespace ChaosSlots
{
    internal static class ChaosSlotState
    {
        internal static bool IsLocalPlayer(DewPlayer player) =>
            player != null && player == DewPlayer.local && ChaosSlotNetwork.IsEnabledFor(player);

        internal static Se_Shrine_Chaos_StatBonus FindBonus(Hero hero) =>
            hero.Status.TryGetStatusEffect<Se_Shrine_Chaos_StatBonus>(out var status) ? status : null;

        internal static SlotLayout Read(Se_Shrine_Chaos_StatBonus status) =>
            status == null ? default : new SlotLayout(status.currentAddedGemSlotQ, status.currentAddedGemSlotW,
                status.currentAddedGemSlotE, status.currentAddedGemSlotR);

        internal static bool TryMemorySlot(HeroSkillLocation location, out MemorySlot slot)
        {
            switch (location)
            {
                case HeroSkillLocation.Q: slot = MemorySlot.Q; return true;
                case HeroSkillLocation.W: slot = MemorySlot.W; return true;
                case HeroSkillLocation.E: slot = MemorySlot.E; return true;
                case HeroSkillLocation.R: slot = MemorySlot.R; return true;
                default: slot = default; return false;
            }
        }

        internal static bool CanEdit(DewPlayer player)
        {
            var edit = ManagerBase<EditSkillManager>.softInstance;
            var control = ManagerBase<ControlManager>.softInstance;
            return IsLocalPlayer(player) && IsActiveRunPlayer(player)
                && edit != null && edit.mode == EditSkillManager.ModeType.Regular
                && !edit.isDragging && control != null && !control.isEditSkillDisabled;
        }

        private static bool IsActiveRunPlayer(DewPlayer player)
        {
            var game = NetworkedManagerBase<GameManager>.softInstance;
            var zone = NetworkedManagerBase<ZoneManager>.softInstance;
            return player != null && player.state == PlayerState.Playing
                && !player.hero.IsNullInactiveDeadOrKnockedOut()
                && game != null && !game.isGameConcluded && zone != null && !zone.isInAnyTransition;
        }

        internal static bool CanGrab(DewPlayer player, GemLocation origin)
        {
            if (!CanEdit(player) || !TryMemorySlot(origin.skill, out var source))
                return false;

            var hero = player.hero;
            if (hero.Ability.IsAbilityEditLocked((int)origin.skill))
                return false;

            var layout = Read(FindBonus(hero));
            int capacity = SlotRules.BaseSlots + layout[source];
            return layout.IsValid && origin.index >= SlotRules.BaseSlots && origin.index < capacity
                && hero.Skill.GetMaxGemCount(origin.skill) == capacity;
        }

        internal static bool CanMove(DewPlayer player, GemLocation origin, HeroSkillLocation target) =>
            CanEdit(player) && TryPrepareMove(player, origin, target, out _, out _);

        internal static uint GetGemNetId(DewPlayer player, GemLocation location)
        {
            var gem = player.hero.Skill.GetGem(location);
            return gem == null ? 0u : gem.netId;
        }

        internal static bool TryCreateRequest(DewPlayer player, GemLocation origin, HeroSkillLocation target,
            ulong sequence, out SlotMoveRequest request)
        {
            request = default;
            if (!CanEdit(player) || !TryPrepareMove(player, origin, target, out var status, out _)
                || !TryMemorySlot(origin.skill, out var source) || !TryMemorySlot(target, out var destination))
                return false;

            var layout = Read(status);
            request = new SlotMoveRequest(source, origin.index, destination, layout[source], layout[destination],
                GetGemNetId(player, origin), sequence);
            return true;
        }

        private static bool TryPrepareMove(DewPlayer player, GemLocation origin, HeroSkillLocation target,
            out Se_Shrine_Chaos_StatBonus status, out SlotMove plan)
        {
            status = null;
            plan = default;
            if (origin.skill == target || !ChaosSlotNetwork.IsEnabledFor(player) || !IsActiveRunPlayer(player)
                || !TryMemorySlot(origin.skill, out var source) || !TryMemorySlot(target, out var destination))
                return false;

            var hero = player.hero;
            if (hero.Ability.IsAbilityEditLocked((int)origin.skill) || hero.Ability.IsAbilityEditLocked((int)target))
                return false;

            status = FindBonus(hero);
            if (status == null || !SlotRules.TryPlanMove(Read(status), source, origin.index, destination, out plan))
                return false;

            var skills = hero.Skill;
            var append = new GemLocation(target, plan.DestinationIndex);
            return skills.GetMaxGemCount(origin.skill) == plan.SourceLastIndex + 1
                && skills.GetMaxGemCount(target) == append.index && skills.GetGem(append) == null;
        }

        internal static bool TryMove(DewPlayer player, GemLocation origin, HeroSkillLocation target)
        {
            if (!NetworkServer.active || !CanEdit(player)
                || !TryPrepareMove(player, origin, target, out var status, out var plan))
                return false;

            return ApplyMove(player, origin, target, status, plan);
        }

        internal static bool TryMoveFromRequest(DewPlayer player, SlotMoveRequest request, ulong lastSequence)
        {
            if (!NetworkServer.active || !TryHeroSlot(request.Source, out var source)
                || !TryHeroSlot(request.Destination, out var target))
                return false;

            var origin = new GemLocation(source, request.SourceIndex);
            if (!TryPrepareMove(player, origin, target, out var status, out _)
                || !SlotMoveRequestRules.TryPlan(Read(status), GetGemNetId(player, origin), lastSequence, request, out var plan))
                return false;

            return ApplyMove(player, origin, target, status, plan);
        }

        private static bool TryHeroSlot(MemorySlot slot, out HeroSkillLocation location)
        {
            switch (slot)
            {
                case MemorySlot.Q: location = HeroSkillLocation.Q; return true;
                case MemorySlot.W: location = HeroSkillLocation.W; return true;
                case MemorySlot.E: location = HeroSkillLocation.E; return true;
                case MemorySlot.R: location = HeroSkillLocation.R; return true;
                default: location = default; return false;
            }
        }

        private static bool ApplyMove(DewPlayer player, GemLocation origin, HeroSkillLocation target,
            Se_Shrine_Chaos_StatBonus status, SlotMove plan)
        {
            var hero = player.hero;
            var skills = hero.Skill;
            var before = Read(status);
            var append = new GemLocation(target, plan.DestinationIndex);
            var last = new GemLocation(origin.skill, plan.SourceLastIndex);
            var moved = skills.GetGem(origin);
            var compacted = last == origin ? null : skills.GetGem(last);
            try
            {
                skills.SetMaxGemCount(target, append.index + 1);
                Relocate(skills, hero, origin, append, moved);
                Relocate(skills, hero, last, origin, compacted);
                Write(status, plan.Layout);
                skills.SetMaxGemCount(origin.skill, last.index);
                return true;
            }
            catch (Exception exception)
            {
                // Native equip can throw after changing ownership. Restore the two original positions.
                try
                {
                    skills.SetMaxGemCount(origin.skill, last.index + 1);
                    Restore(skills, hero, compacted, last);
                    Restore(skills, hero, moved, origin);
                }
                catch (Exception rollbackException)
                {
                    Debug.LogException(rollbackException);
                }
                finally
                {
                    Write(status, before);
                    skills.SetMaxGemCount(target, append.index);
                }
                Debug.LogException(exception);
                return false;
            }
        }

        private static void Relocate(HeroSkill skills, Hero hero, GemLocation from, GemLocation to, Gem gem)
        {
            if (gem == null)
                return;
            skills.UnequipGem(from, hero.agentPosition);
            skills.EquipGem(to, gem);
        }

        private static void Restore(HeroSkill skills, Hero hero, Gem gem, GemLocation original)
        {
            if (gem == null)
                return;
            if (skills.TryGetGemLocation(gem, out var current))
            {
                if (current == original)
                    return;
                skills.UnequipGem(current, hero.agentPosition);
            }
            else
            {
                // EquipGem sets both fields before adding to the dictionary.
                gem.skill = null;
                gem.owner = null;
            }
            skills.EquipGem(original, gem);
        }

        private static void Write(Se_Shrine_Chaos_StatBonus status, SlotLayout layout)
        {
            status.NetworkcurrentAddedGemSlotQ = layout[MemorySlot.Q];
            status.NetworkcurrentAddedGemSlotW = layout[MemorySlot.W];
            status.NetworkcurrentAddedGemSlotE = layout[MemorySlot.E];
            status.NetworkcurrentAddedGemSlotR = layout[MemorySlot.R];
        }
    }
}
