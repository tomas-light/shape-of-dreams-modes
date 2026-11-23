using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ChaosSlots.QaRoom
{
    internal static class FixturePositions
    {
        private const float MinDistanceFromSpawn = 1.2f;
        private const float MinDistanceBetweenFixtures = 1.3f;
        private const float MinDistanceFromShrine = 2.0f;
        private const int Directions = 16;

        internal static List<Vector3> Resolve(
            Vector3 spawn, Quaternion rotation, IReadOnlyList<FixturePlacement> plan)
        {
            var positions = new List<Vector3>(plan.Count);
            var path = new NavMeshPath();
            var heading = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
            foreach (var fixture in plan)
            {
                var preferred = spawn + heading * new Vector3(fixture.X, 0f, fixture.Z);
                if (!TryPosition(preferred, fixture.Kind, out var position)
                    && !TryFallback(preferred, fixture.Kind, out position))
                {
                    // Memories are last in the plan. Keep the required test layout if one cannot fit.
                    return fixture.Kind == FixtureKind.Memory ? positions : null;
                }
                positions.Add(position);
            }
            return positions;

            bool TryPosition(Vector3 proposed, FixtureKind kind, out Vector3 position)
            {
                position = default;
                if (!NavMesh.SamplePosition(Dew.GetPositionOnGround(proposed), out var hit, 1.1f, NavMesh.AllAreas)
                    || DistanceSquared(spawn, hit.position) < MinDistanceFromSpawn * MinDistanceFromSpawn)
                    return false;

                for (int i = 0; i < positions.Count; i++)
                {
                    float spacing = kind == FixtureKind.Shrine || plan[i].Kind == FixtureKind.Shrine
                        ? MinDistanceFromShrine : MinDistanceBetweenFixtures;
                    if (DistanceSquared(positions[i], hit.position) < spacing * spacing)
                        return false;
                }

                if (!NavMesh.CalculatePath(spawn, hit.position, NavMesh.AllAreas, path)
                    || path.status != NavMeshPathStatus.PathComplete)
                    return false;

                position = hit.position;
                return true;
            }

            bool TryFallback(Vector3 preferred, FixtureKind kind, out Vector3 position)
            {
                float radialDistance = Mathf.Sqrt(DistanceSquared(spawn, preferred));
                var direction = new Vector3(preferred.x - spawn.x, 0f, preferred.z - spawn.z).normalized;
                // Keep the planned distance first, then search nearby distances on the same walkable island.
                foreach (float radius in new[] { radialDistance, 2.5f, 4f, 5.5f, 7f, 8.5f, 10f })
                {
                    for (int i = 0; i < Directions; i++)
                    {
                        var rotated = Quaternion.Euler(0f, i * 360f / Directions, 0f) * direction;
                        if (TryPosition(spawn + rotated * radius, kind, out position))
                            return true;
                    }
                }
                position = default;
                return false;
            }
        }

        private static float DistanceSquared(Vector3 first, Vector3 second)
        {
            float x = first.x - second.x;
            float z = first.z - second.z;
            return x * x + z * z;
        }
    }
}
