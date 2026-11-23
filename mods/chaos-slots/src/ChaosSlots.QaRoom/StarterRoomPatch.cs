using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace ChaosSlots.QaRoom
{
    [HarmonyPatch(typeof(Room), nameof(Room.StartRoom))]
    internal static class StarterRoomPatch
    {
        private const string ForestStartScenePrefix = "Room_Forest_Start_";
        private const int EssenceQuality = 10;
        private const int MemoryLevel = 1;

        private static readonly Type[] EssenceTypes =
        {
            typeof(Gem_C_Charcoal), typeof(Gem_C_Confidence), typeof(Gem_C_Efficiency), typeof(Gem_C_Quicksilver),
            typeof(Gem_C_Sharp), typeof(Gem_C_Sulfur), typeof(Gem_C_Talc), typeof(Gem_C_Wind)
        };

        private static Room _populatedRoom;

        private static void Postfix(Room __instance)
        {
            if (!IsFreshRunStart(__instance) || _populatedRoom == __instance)
                return;

            _populatedRoom = __instance;
            try
            {
                Populate(__instance);
            }
            catch (Exception exception)
            {
                // A missing test fixture should never interrupt the original room transition.
                Debug.LogError("[Chaos Slots Test Room] Fixture setup failed.");
                Debug.LogException(exception);
            }
        }

        private static bool IsFreshRunStart(Room room)
        {
            if (!NetworkServer.active || !(NetworkedManagerBase<GameManager>.instance is PlayGameManager) || room.isRevisit
                || !room.gameObject.scene.name.StartsWith(ForestStartScenePrefix, StringComparison.Ordinal))
                return false;

            var zone = NetworkedManagerBase<ZoneManager>.instance;
            var settings = zone == null ? null : zone.lastLoadNodeSettings;
            return settings != null && !settings.isLoadingFromSave && settings.from == -1
                && zone.currentZoneIndex == 0 && zone.currentRoom == room;
        }

        private static void Populate(Room room)
        {
            var memories = FindMemoryPrefabs();
            var plan = TestRoomPlan.GetPlacements(memories.Count);
            var positions = FixturePositions.Resolve(room.GetHeroSpawnPosition(), room.GetHeroSpawnRotation(), plan);
            if (positions == null)
                throw new InvalidOperationException("Could not fit the fixtures on reachable ground in the start room.");

            var shrine = RequirePrefab<Shrine_CorruptedChaos>(typeof(Shrine_CorruptedChaos));
            var essences = Array.ConvertAll(EssenceTypes, RequirePrefab<Gem>);
            const int requiredCount = TestRoomPlan.ShrineCount + TestRoomPlan.EssenceCount;
            var spawned = new List<Actor>(requiredCount);
            try
            {
                for (int i = 0; i < requiredCount; i++)
                {
                    var fixture = plan[i];
                    var position = positions[i];
                    spawned.Add(fixture.Kind == FixtureKind.Shrine
                        ? (Actor)CreateShrine(shrine, position, fixture.Index == TestRoomPlan.ShrineCount - 1)
                        : Dew.CreateGem(essences[fixture.Index], position, EssenceQuality));
                }
            }
            catch
            {
                foreach (var actor in spawned)
                    if (actor != null)
                        actor.Destroy();
                throw;
            }

            int spawnedMemories = 0;
            for (int i = requiredCount; i < positions.Count; i++)
            {
                try
                {
                    if (Dew.CreateSkillTrigger(memories[plan[i].Index], positions[i], MemoryLevel) != null)
                        spawnedMemories++;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
            Debug.Log($"[Chaos Slots Test Room] Spawned {TestRoomPlan.ShrineCount} Corrupted Chaos shrines, "
                + $"{TestRoomPlan.EssenceCount} essences, and {spawnedMemories} memories.");
            if (spawnedMemories < TestRoomPlan.MemoryCount)
                Debug.LogWarning("[Chaos Slots Test Room] Could not place all optional memory pickups.");
        }

        private static List<SkillTrigger> FindMemoryPrefabs()
        {
            var memories = new List<SkillTrigger>(TestRoomPlan.MemoryCount);
            try
            {
                var database = DewResources.database;
                var loot = NetworkedManagerBase<LootManager>.instance;
                var candidates = loot == null
                    ? Enumerable.Empty<string>()
                    : loot.poolSkills.Where(IsOrdinaryMemoryName).OrderBy(name => name, StringComparer.Ordinal);
                // A managed class may have no registered prefab in this build.
                var registered = database.typeNameToType.Keys.Where(IsOrdinaryMemoryName)
                    .OrderBy(name => name, StringComparer.Ordinal);
                var usedTypes = new HashSet<Type>();
                foreach (string name in candidates.Concat(registered))
                {
                    if (!database.typeNameToType.TryGetValue(name, out var type)
                        || !typeof(SkillTrigger).IsAssignableFrom(type) || !database.typeToGuid.ContainsKey(type)
                        || !Dew.IsSkillIncludedInGame(name) || Dew.IsExcludedFromPool(name) || !usedTypes.Add(type))
                        continue;
                    try
                    {
                        var skill = DewResources.GetByType<SkillTrigger>(type);
                        if (skill == null || skill.isCharacterSkill || skill.excludeFromPool
                            || (skill.rarity != Rarity.Common && skill.rarity != Rarity.Rare))
                            continue;
                        memories.Add(skill);
                        if (memories.Count == TestRoomPlan.MemoryCount)
                            break;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            return memories;
        }

        private static bool IsOrdinaryMemoryName(string name) =>
            name.StartsWith("St_C_", StringComparison.Ordinal) || name.StartsWith("St_R_", StringComparison.Ordinal);

        private static T RequirePrefab<T>(Type type) where T : Actor
        {
            var prefab = DewResources.GetByType<T>(type);
            if (prefab == null)
                throw new InvalidOperationException($"Missing test room prefab: {type.Name}.");
            return prefab;
        }

        private static Shrine_CorruptedChaos CreateShrine(
            Shrine_CorruptedChaos prefab, Vector3 position, bool withAlternateReward)
        {
            return Dew.CreateActor(prefab, position, null, beforePrepare: shrine =>
            {
                shrine.pool = new List<Shrine_CorruptedChaos.PoolItem>
                {
                    CreateReward(CorruptedChaosRewardType.AddedEssenceSlot)
                };
                if (withAlternateReward)
                    shrine.pool.Add(CreateReward(CorruptedChaosRewardType.DodgeCooldown));
                shrine.numOfChoice = shrine.pool.Count;
            });
        }

        private static Shrine_CorruptedChaos.PoolItem CreateReward(CorruptedChaosRewardType type) =>
            new() { type = type, weight = 1f };
    }
}
