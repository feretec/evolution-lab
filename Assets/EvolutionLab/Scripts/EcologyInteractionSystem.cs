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
        private readonly List<EcologyInteractionEvent> events = new List<EcologyInteractionEvent>();

        public IReadOnlyList<EcologyInteractionEvent> Events
        {
            get { return events; }
        }

        public int InteractionCount { get; private set; }

        public int PredationCount { get; private set; }

        public CreatureInteractionObservation Observe(
            Creature subject,
            IReadOnlyList<Creature> creatures,
            EnvironmentController environment)
        {
            CreatureInteractionObservation observation = CreatureInteractionObservation.Empty;
            if (subject == null || subject.RootBody == null || creatures == null)
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
            for (int i = 0; i < creatures.Count; i++)
            {
                Creature candidate = creatures[i];
                if (candidate == null || candidate == subject || !candidate.IsAlive || candidate.RootBody == null)
                {
                    continue;
                }

                Vector3 delta = candidate.RootBody.position - subject.RootBody.position;
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
                Vector3 direction = nearest.RootBody.position - subject.RootBody.position;
                direction.y = 0f;
                observation.nearestIndividualDirection = direction.sqrMagnitude < 0.0001f
                    ? Vector3.zero
                    : direction.normalized;
                observation.nearestIndividualDistance = Mathf.Sqrt(nearestDistance);
            }

            if (nearestThreat != null)
            {
                Vector3 direction = nearestThreat.RootBody.position - subject.RootBody.position;
                direction.y = 0f;
                observation.nearestThreatDirection = direction.sqrMagnitude < 0.0001f
                    ? Vector3.zero
                    : direction.normalized;
                observation.nearestThreatDistance = Mathf.Sqrt(nearestThreatDistance);
            }

            if (environment != null)
            {
                observation.obstacleProximity = environment.GetObstacleProximity(
                    subject.RootBody.position,
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

            for (int i = 0; i < creatures.Count; i++)
            {
                Creature first = creatures[i];
                if (first == null || !first.IsAlive || first.RootBody == null)
                {
                    continue;
                }

                for (int j = i + 1; j < creatures.Count; j++)
                {
                    Creature second = creatures[j];
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
            float attackPower = intent * (0.55f + attackerMass * 0.35f);
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
            bool killed = !target.IsAlive;
            if (killed)
            {
                PredationCount++;
                attacker.RegisterKill();
                attacker.AddEnergy(Mathf.Max(4f, target.MaxEnergy * 0.45f));
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
            float firstRange = first.Genome == null || first.Genome.ecology == null
                ? 8f
                : first.Genome.ecology.sensorRange;
            float secondRange = second.Genome == null || second.Genome.ecology == null
                ? 8f
                : second.Genome.ecology.sensorRange;
            // Long-range sensors observe; interaction itself still requires a
            // local encounter so the world remains spatially legible.
            return Mathf.Clamp(Mathf.Min(firstRange, secondRange) * 0.28f + 1.25f, 1.5f, 5f);
        }

        private static float AttackPotential(Creature attacker, Creature target, float distance)
        {
            if (attacker == null || target == null || !attacker.IsAlive || !target.IsAlive)
            {
                return 0f;
            }

            EcologyGene gene = attacker.Genome == null ? null : attacker.Genome.ecology;
            float predation = gene == null ? 0.2f : gene.predationDrive;
            float range = gene == null ? 8f : Mathf.Max(2f, gene.sensorRange);
            float proximity = Mathf.Clamp01(1f - distance / Mathf.Max(1f, range * 0.32f + 1.2f));
            float massAdvantage = Mathf.Clamp01(Mathf.Sqrt(attacker.BodyMass / Mathf.Max(0.1f, target.BodyMass)) * 0.5f);
            return predation * (0.2f + attacker.InteractionIntent * 0.8f) * (0.45f + massAdvantage) * proximity;
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
    }
}
