using System;
using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLab
{
    [Serializable]
    public sealed class EcologyInteractionEvent
    {
        public Creature actor;
        public Creature target;
        public bool predation;
        public bool successful;
        public float value;
    }

    /// <summary>
    /// Resolves generic proximity interactions between live embodiments. There
    /// are no Predator/Prey subclasses: predation, avoidance, clustering, and
    /// resource-oriented behaviour are outcomes of continuous genes plus brain
    /// outputs. The system is deliberately separate from CreatureBuilder so a
    /// future ECS/Burst implementation can replace this runtime adapter.
    /// </summary>
    public sealed class EcologyInteractionSystem
    {
        private const float SpatialCellSize = 5f;
        private readonly List<EcologyInteractionEvent> events = new List<EcologyInteractionEvent>();
        private readonly Dictionary<long, List<Creature>> spatialCells = new Dictionary<long, List<Creature>>();
        private readonly Dictionary<Creature, int> creatureIndices = new Dictionary<Creature, int>();
        private readonly List<Creature> neighborBuffer = new List<Creature>(32);

        public IReadOnlyList<EcologyInteractionEvent> Events
        {
            get { return events; }
        }

        public int InteractionCount { get; private set; }

        public int PredationCount { get; private set; }

        /// <summary>
        /// Rebuilds a lightweight spatial index once per physics step. This
        /// keeps observation and encounter work local without coupling the
        /// simulation contract to a future Jobs/ECS implementation.
        /// </summary>
        public void RebuildSpatialIndex(IReadOnlyList<Creature> creatures)
        {
            spatialCells.Clear();
            creatureIndices.Clear();
            if (creatures == null)
            {
                return;
            }

            for (int i = 0; i < creatures.Count; i++)
            {
                Creature creature = creatures[i];
                if (creature == null || !creature.IsAlive || creature.RootBody == null)
                {
                    continue;
                }

                creatureIndices[creature] = i;
                long key = CellKey(creature.RootBody.position);
                if (!spatialCells.TryGetValue(key, out List<Creature> cell))
                {
                    cell = new List<Creature>(8);
                    spatialCells.Add(key, cell);
                }
                cell.Add(creature);
            }
        }

        public CreatureInteractionObservation Observe(
            Creature subject,
            IReadOnlyList<Creature> creatures,
            EnvironmentController environment)
        {
            return Observe(
                subject,
                subject == null || subject.RootBody == null ? Vector3.zero : subject.RootBody.position,
                creatures,
                environment);
        }

        public CreatureInteractionObservation Observe(
            Creature subject,
            Vector3 observationOrigin,
            IReadOnlyList<Creature> creatures,
            EnvironmentController environment)
        {
            CreatureInteractionObservation observation = CreatureInteractionObservation.Empty;
            if (subject == null || subject.RootBody == null || creatures == null || !IsFinite(observationOrigin))
            {
                return observation;
            }

            float range = subject.Genome == null || subject.Genome.ecology == null
                ? 8f
                : Mathf.Max(2f, subject.Genome.ecology.sensorRange);
            float nearestDistance = range * range;
            float nearestThreatDistance = range * range;
            Creature nearest = null;
            Creature nearestThreat = null;
            CollectNeighbors(observationOrigin, range, creatures);
            for (int i = 0; i < neighborBuffer.Count; i++)
            {
                Creature candidate = neighborBuffer[i];
                if (candidate == null || candidate == subject || !candidate.IsAlive || candidate.RootBody == null)
                {
                    continue;
                }

                Vector3 delta = candidate.RootBody.position - observationOrigin;
                delta.y = 0f;
                float distance = delta.sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = candidate;
                }

                float threat = AttackPotential(candidate, subject, delta.magnitude);
                if (threat > 0.2f && distance < nearestThreatDistance)
                {
                    nearestThreatDistance = distance;
                    nearestThreat = candidate;
                }
            }

            if (nearest != null)
            {
                Vector3 direction = nearest.RootBody.position - observationOrigin;
                direction.y = 0f;
                observation.nearestIndividualDirection = direction.sqrMagnitude < 0.0001f
                    ? Vector3.zero
                    : direction.normalized;
                observation.nearestIndividualDistance = Mathf.Sqrt(nearestDistance);
            }

            if (nearestThreat != null)
            {
                Vector3 direction = nearestThreat.RootBody.position - observationOrigin;
                direction.y = 0f;
                observation.nearestThreatDirection = direction.sqrMagnitude < 0.0001f
                    ? Vector3.zero
                    : direction.normalized;
                observation.nearestThreatDistance = Mathf.Sqrt(nearestThreatDistance);
            }

            if (environment != null)
            {
                observation.obstacleProximity = environment.GetObstacleProximity(
                    observationOrigin,
                    range);
            }

            return observation;
        }

        public void Tick(
            IReadOnlyList<Creature> creatures,
            float deltaTime)
        {
            events.Clear();
            InteractionCount = 0;
            PredationCount = 0;
            if (creatures == null || deltaTime <= 0f)
            {
                return;
            }

            if (creatureIndices.Count == 0)
            {
                RebuildSpatialIndex(creatures);
            }

            for (int i = 0; i < creatures.Count; i++)
            {
                Creature first = creatures[i];
                if (first == null || !first.IsAlive || first.RootBody == null)
                {
                    continue;
                }

                CollectNeighbors(first.RootBody.position, 5f, creatures);
                for (int j = 0; j < neighborBuffer.Count; j++)
                {
                    Creature second = neighborBuffer[j];
                    if (!creatureIndices.TryGetValue(second, out int secondIndex) || secondIndex <= i)
                    {
                        continue;
                    }
                    if (second == null || !second.IsAlive || second.RootBody == null)
                    {
                        continue;
                    }

                    Vector3 delta = second.RootBody.position - first.RootBody.position;
                    delta.y = 0f;
                    float distance = delta.magnitude;
                    float range = InteractionRange(first, second);
                    if (distance > range)
                    {
                        continue;
                    }

                    float firstIntent = AttackPotential(first, second, distance);
                    float secondIntent = AttackPotential(second, first, distance);
                    float firstSocial = SocialPotential(first, distance, range);
                    float secondSocial = SocialPotential(second, distance, range);
                    if (firstSocial > 0.25f || secondSocial > 0.25f)
                    {
                        InteractionCount++;
                        events.Add(new EcologyInteractionEvent
                        {
                            actor = firstSocial >= secondSocial ? first : second,
                            target = firstSocial >= secondSocial ? second : first,
                            predation = false,
                            successful = true,
                            value = Mathf.Max(firstSocial, secondSocial)
                        });
                    }

                    if (firstIntent <= 0.28f && secondIntent <= 0.28f)
                    {
                        continue;
                    }

                    Creature attacker = firstIntent >= secondIntent ? first : second;
                    Creature target = firstIntent >= secondIntent ? second : first;
                    float attackIntent = Mathf.Max(firstIntent, secondIntent);
                    ResolveAttack(attacker, target, attackIntent, distance, deltaTime);
                }
            }
        }

        private void CollectNeighbors(
            Vector3 position,
            float range,
            IReadOnlyList<Creature> fallbackCreatures)
        {
            neighborBuffer.Clear();
            if (spatialCells.Count == 0)
            {
                if (fallbackCreatures != null)
                {
                    for (int i = 0; i < fallbackCreatures.Count; i++)
                    {
                        neighborBuffer.Add(fallbackCreatures[i]);
                    }
                }
                return;
            }

            int centerX = Mathf.FloorToInt(position.x / SpatialCellSize);
            int centerZ = Mathf.FloorToInt(position.z / SpatialCellSize);
            int cellRadius = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0.1f, range) / SpatialCellSize));
            for (int z = centerZ - cellRadius; z <= centerZ + cellRadius; z++)
            {
                for (int x = centerX - cellRadius; x <= centerX + cellRadius; x++)
                {
                    if (!spatialCells.TryGetValue(CellKey(x, z), out List<Creature> cell))
                    {
                        continue;
                    }

                    for (int i = 0; i < cell.Count; i++)
                    {
                        neighborBuffer.Add(cell[i]);
                    }
                }
            }
        }

        private static long CellKey(Vector3 position)
        {
            return CellKey(
                Mathf.FloorToInt(position.x / SpatialCellSize),
                Mathf.FloorToInt(position.z / SpatialCellSize));
        }

        private static long CellKey(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }

        private void ResolveAttack(
            Creature attacker,
            Creature target,
            float intent,
            float distance,
            float deltaTime)
        {
            if (attacker == null || target == null || !attacker.IsAlive || !target.IsAlive)
            {
                return;
            }

            InteractionCount++;
            EcologyGene attackerGene = attacker.Genome == null ? null : attacker.Genome.ecology;
            EcologyGene targetGene = target.Genome == null ? null : target.Genome.ecology;
            float attackerMass = Mathf.Sqrt(attacker.BodyMass);
            float targetMass = Mathf.Sqrt(target.BodyMass);
            attacker.TryGetMouthProfile(
                out Vector3 mouthOrigin,
                out Vector3 mouthDirection,
                out float mouthReach,
                out float mouthEfficiency);
            float attackPower = intent * (0.55f + attackerMass * 0.35f) * mouthEfficiency;
            float defense = targetGene == null
                ? 0.4f
                : targetGene.defenseDrive * 0.75f + targetGene.bodyProtection * 0.65f;
            float escape = target.InteractionIntent * 0.35f + (targetGene == null ? 0.2f : targetGene.defenseDrive * 0.45f);
            float damageFactor = Mathf.Max(0f, attackPower - defense * 0.35f - escape * 0.2f);
            float damage = damageFactor * (7f + targetMass * 2f) * deltaTime;
            if (damage <= 0.02f)
            {
                events.Add(new EcologyInteractionEvent
                {
                    actor = attacker,
                    target = target,
                    predation = true,
                    successful = false,
                    value = escape
                });
                return;
            }

            float applied = target.ApplyDamage(damage, "Predated by " + attacker.Genome.genomeId);
            if (applied > 0.02f)
            {
                // Generic interaction energy transfer is shaped by the
                // inherited mouth efficiency, without assigning a predator
                // class to either individual.
                attacker.AddEnergy(applied * 0.08f * mouthEfficiency);
            }
            bool killed = !target.IsAlive;
            if (killed)
            {
                PredationCount++;
                attacker.RegisterKill();
                attacker.AddEnergy(Mathf.Max(4f, target.MaxEnergy * 0.45f * mouthEfficiency));
            }

            events.Add(new EcologyInteractionEvent
            {
                actor = attacker,
                target = target,
                predation = true,
                successful = applied > 0.02f,
                value = applied
            });
        }

        private static float InteractionRange(Creature first, Creature second)
        {
            Vector3 unusedOrigin;
            Vector3 unusedDirection;
            float unusedEfficiency;
            float firstReach;
            float secondReach;
            first.TryGetMouthProfile(out unusedOrigin, out unusedDirection, out firstReach, out unusedEfficiency);
            second.TryGetMouthProfile(out unusedOrigin, out unusedDirection, out secondReach, out unusedEfficiency);
            return Mathf.Clamp(Mathf.Max(firstReach, secondReach), 0.5f, 4f);
        }

        private static float AttackPotential(Creature attacker, Creature target, float distance)
        {
            if (attacker == null || target == null || !attacker.IsAlive || !target.IsAlive)
            {
                return 0f;
            }

            EcologyGene gene = attacker.Genome == null ? null : attacker.Genome.ecology;
            float predation = gene == null ? 0.2f : gene.predationDrive;
            attacker.TryGetMouthProfile(
                out Vector3 mouthOrigin,
                out Vector3 mouthDirection,
                out float reach,
                out float efficiency);
            Vector3 toTarget = target.RootBody.position - mouthOrigin;
            float mouthDistance = toTarget.magnitude;
            float facing = toTarget.sqrMagnitude < 0.0001f
                ? 1f
                : Mathf.Clamp01(0.35f + Vector3.Dot(mouthDirection, toTarget.normalized) * 0.65f);
            float proximity = Mathf.Clamp01(1f - Mathf.Max(distance, mouthDistance) / Mathf.Max(0.25f, reach));
            float massAdvantage = Mathf.Clamp01(Mathf.Sqrt(attacker.BodyMass / Mathf.Max(0.1f, target.BodyMass)) * 0.5f);
            return predation * efficiency * (0.2f + attacker.InteractionIntent * 0.8f)
                * (0.45f + massAdvantage) * facing * proximity;
        }

        private static float SocialPotential(Creature creature, float distance, float range)
        {
            EcologyGene gene = creature == null || creature.Genome == null
                ? null
                : creature.Genome.ecology;
            float social = gene == null ? 0.25f : gene.socialDrive;
            float proximity = Mathf.Clamp01(1f - distance / Mathf.Max(0.1f, range));
            return social * (0.35f + creature.SocialIntent) * proximity;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
