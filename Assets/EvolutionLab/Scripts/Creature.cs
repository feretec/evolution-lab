using System;
using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLab
{
    /// <summary>
    /// Live physical embodiment of a CreatureGenome.
    /// </summary>
    public sealed class Creature : MonoBehaviour
    {
        private readonly List<Rigidbody> bodyParts = new List<Rigidbody>();
        private readonly List<ConfigurableJoint> joints = new List<ConfigurableJoint>();
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<Collider> colliders = new List<Collider>();

        private BodyPartGene[] partGenes = Array.Empty<BodyPartGene>();
        private Vector3[] safePositions = Array.Empty<Vector3>();
        private Quaternion[] safeRotations = Array.Empty<Quaternion>();
        private Quaternion[] jointRestRelativeRotations = Array.Empty<Quaternion>();
        private Brain brain;
        private Rigidbody rootBody;
        private Material bodyMaterial;
        private Color baseColor;
        private float jointDriveForce = 100f;
        private float jointTargetSpeedDegrees = 220f;
        private float jointDamping = 8f;
        private float settlingDuration = 0.35f;
        private float startX;
        private float bestX;
        private float brainClock;
        private float lifeAgeSeconds;
        private float energy;
        private float maxEnergy = 100f;
        private float metabolismPerSecond = 0.35f;
        private float movementEnergyCost = 0.02f;
        private float maxAgeSeconds = 90f;
        private float maturityAgeSeconds = 5f;
        private float reproductionEnergyThreshold = 78f;
        private float reproductionCost = 42f;
        private float reproductionCooldownSeconds = 8f;
        private float reproductionCooldownRemaining;
        private float totalEnergyAcquired;
        private int offspringCount;
        private bool evaluationActive;
        private bool alive;
        private bool selected;
        private string deathReason = string.Empty;
        private Func<Vector3, Vector3> resourcePositionProvider;
        private Func<Vector3, CreatureInteractionObservation> interactionObservationProvider;
        private float interactionIntent;
        private float reproductionIntent;
        private float socialIntent;
        private float foragingIntent;
        private float damageTaken;
        private int killCount;
        private const int MotorChannelCount = 8;

        public event Action<Creature> Clicked;

        public CreatureGenome Genome { get; private set; }

        public Brain Brain { get { return brain; } }

        public Rigidbody RootBody { get { return rootBody; } }

        public int BodyPartCount { get { return bodyParts.Count; } }

        public int JointCount { get { return joints.Count; } }

        public float AgeSeconds { get { return lifeAgeSeconds; } }

        public float Energy { get { return energy; } }

        public float MaxEnergy { get { return maxEnergy; } }

        public float EnergyRatio
        {
            get { return maxEnergy <= 0f ? 0f : Mathf.Clamp01(energy / maxEnergy); }
        }

        public bool IsAlive { get { return alive; } }

        public string DeathReason { get { return deathReason; } }

        public int OffspringCount { get { return offspringCount; } }

        public float TotalEnergyAcquired { get { return totalEnergyAcquired; } }

        public int KillCount { get { return killCount; } }

        public float DamageTaken { get { return damageTaken; } }

        public float InteractionIntent { get { return interactionIntent; } }

        public float ReproductionIntent { get { return reproductionIntent; } }

        public float SocialIntent { get { return socialIntent; } }

        public float ForagingIntent { get { return foragingIntent; } }

        /// <summary>
        /// Read-only lifetime-learning metrics for observation UI/history.
        /// Fast weights themselves remain private to Brain and are not part of
        /// the inherited Genome.
        /// </summary>
        public bool LifetimeLearningEnabled
        {
            get { return brain != null && brain.LearningEnabled; }
        }

        public float LearningSignal
        {
            get { return brain == null ? 0f : brain.LastHomeostaticSignal; }
        }

        public float LearningAdaptationMagnitude
        {
            get { return brain == null ? 0f : brain.AdaptationMagnitude; }
        }

        public bool TryGetMouthProfile(
            out Vector3 origin,
            out Vector3 direction,
            out float reach,
            out float efficiency)
        {
            origin = rootBody == null ? transform.position : rootBody.position;
            direction = rootBody == null ? transform.forward : rootBody.transform.right;
            reach = 1.4f;
            efficiency = 0.65f;
            if (Genome == null || bodyParts.Count == 0)
            {
                return false;
            }

            MouthGene mouth = Genome.mouth;
            int index = Mathf.Clamp(mouth.bodyPartIndex, 0, bodyParts.Count - 1);
            Rigidbody bodyPart = bodyParts[index];
            if (bodyPart == null)
            {
                return false;
            }

            Vector3 localDirection = mouth.localDirection.sqrMagnitude < 0.0001f
                ? Vector3.right
                : mouth.localDirection.normalized;
            origin = bodyPart.transform.TransformPoint(mouth.localPosition);
            direction = bodyPart.transform.TransformDirection(localDirection).normalized;
            reach = Mathf.Clamp(mouth.reach, 0.25f, 4f);
            efficiency = Mathf.Clamp(mouth.efficiency, 0.05f, 2f);
            return IsFinite(origin) && IsFinite(direction);
        }

        public float BodyMass
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < bodyParts.Count; i++)
                {
                    if (bodyParts[i] != null && IsFinite(bodyParts[i].mass))
                    {
                        total += Mathf.Max(0f, bodyParts[i].mass);
                    }
                }

                return Mathf.Max(0.1f, total);
            }
        }

        /// <summary>
        /// A post-hoc observation label for the catalogue/UI. It is derived
        /// from continuous traits and current controller output, not used as
        /// a gameplay class or a hard-coded species role.
        /// </summary>
        public string EcologicalTendency
        {
            get
            {
                EcologyGene ecology = Genome == null ? null : Genome.ecology;
                if (ecology == null)
                {
                    return "undetermined";
                }

                float predatory = ecology.predationDrive * (0.35f + interactionIntent);
                float defensive = ecology.defenseDrive * (1.1f - Mathf.Clamp01(interactionIntent));
                float social = ecology.socialDrive * (0.35f + socialIntent);
                float foraging = ecology.foragingDrive * (0.35f + foragingIntent);
                if (predatory >= defensive && predatory >= social && predatory >= foraging)
                {
                    return "interacting / pursuing";
                }

                if (defensive >= social && defensive >= foraging)
                {
                    return "defensive / evasive";
                }

                if (social >= foraging)
                {
                    return "social / clustering";
                }

                return "resource-oriented";
            }
        }

        public bool CanReproduce
        {
            get
            {
                EcologyGene ecology = Genome == null ? null : Genome.ecology;
                float drive = ecology == null ? 0.6f : ecology.reproductionDrive;
                return alive
                    && lifeAgeSeconds >= maturityAgeSeconds
                    && reproductionCooldownRemaining <= 0f
                    && energy >= reproductionEnergyThreshold * Mathf.Lerp(1.15f, 0.82f, drive);
            }
        }

        public float CurrentDistance
        {
            get { return Mathf.Max(0f, bestX - startX); }
        }

        public float Fitness
        {
            get { return CurrentDistance; }
        }

        public float SurvivalFitness
        {
            get
            {
                return Mathf.Max(
                    0f,
                    lifeAgeSeconds
                    + offspringCount * 30f
                    + killCount * 36f
                    + energy * 0.1f
                    + CurrentDistance * 0.2f
                    + totalEnergyAcquired * 0.03f);
            }
        }

        public IReadOnlyList<Collider> Colliders
        {
            get { return colliders; }
        }

        public void Configure(
            CreatureGenome genome,
            IList<Rigidbody> rigidbodies,
            IList<ConfigurableJoint> configurableJoints,
            IList<Renderer> bodyRenderers,
            IList<Collider> bodyColliders,
            Material material,
            Color color,
            float initialDriveForce,
            float initialTargetSpeedDegrees,
            float initialDamping,
            float initialSettlingDuration)
        {
            Genome = genome == null ? new CreatureGenome() : genome.Clone();
            bodyParts.Clear();
            joints.Clear();
            renderers.Clear();
            colliders.Clear();

            if (rigidbodies != null)
            {
                bodyParts.AddRange(rigidbodies);
            }

            if (configurableJoints != null)
            {
                joints.AddRange(configurableJoints);
            }

            if (bodyRenderers != null)
            {
                renderers.AddRange(bodyRenderers);
            }

            if (bodyColliders != null)
            {
                colliders.AddRange(bodyColliders);
            }

            partGenes = Genome == null || Genome.bodyParts == null
                ? Array.Empty<BodyPartGene>()
                : Genome.bodyParts.ToArray();
            safePositions = new Vector3[bodyParts.Count];
            safeRotations = new Quaternion[bodyParts.Count];
            for (int i = 0; i < bodyParts.Count; i++)
            {
                if (bodyParts[i] != null)
                {
                    safePositions[i] = bodyParts[i].position;
                    safeRotations[i] = bodyParts[i].rotation;
                }
            }

            jointRestRelativeRotations = new Quaternion[joints.Count];
            for (int i = 0; i < joints.Count; i++)
            {
                ConfigurableJoint joint = joints[i];
                jointRestRelativeRotations[i] = joint == null || joint.connectedBody == null
                    ? Quaternion.identity
                    : Quaternion.Inverse(joint.connectedBody.rotation) * joint.transform.rotation;
            }

            brain = new Brain(Genome == null ? null : Genome.brain);
            rootBody = bodyParts.Count > 0 ? bodyParts[0] : null;
            bodyMaterial = material;
            baseColor = color;
            SetPhysicsTuning(
                initialDriveForce,
                initialTargetSpeedDegrees,
                initialDamping,
                initialSettlingDuration);
            SetSelected(false);
            alive = false;
            deathReason = string.Empty;
            interactionIntent = 0f;
            reproductionIntent = 0f;
            socialIntent = 0f;
            foragingIntent = 0f;
            damageTaken = 0f;
            killCount = 0;
            brain.ResetRuntimeState();
        }

        public void ConfigureLife(
            float initialEnergy,
            float initialMaxEnergy,
            float initialMetabolismPerSecond,
            float initialMovementEnergyCost,
            float initialMaxAgeSeconds,
            float initialMaturityAgeSeconds,
            float initialReproductionEnergyThreshold,
            float initialReproductionCost,
            float initialReproductionCooldownSeconds)
        {
            maxEnergy = Mathf.Max(1f, initialMaxEnergy);
            energy = Mathf.Clamp(initialEnergy, 0f, maxEnergy);
            metabolismPerSecond = Mathf.Max(0f, initialMetabolismPerSecond);
            movementEnergyCost = Mathf.Max(0f, initialMovementEnergyCost);
            maxAgeSeconds = Mathf.Max(1f, initialMaxAgeSeconds);
            maturityAgeSeconds = Mathf.Clamp(initialMaturityAgeSeconds, 0f, maxAgeSeconds);
            reproductionEnergyThreshold = Mathf.Clamp(
                initialReproductionEnergyThreshold,
                0f,
                maxEnergy);
            reproductionCost = Mathf.Clamp(initialReproductionCost, 0f, maxEnergy);
            reproductionCooldownSeconds = Mathf.Max(0f, initialReproductionCooldownSeconds);
            reproductionCooldownRemaining = 0f;
            lifeAgeSeconds = 0f;
            totalEnergyAcquired = 0f;
            offspringCount = 0;
            deathReason = string.Empty;
            damageTaken = 0f;
            killCount = 0;
            interactionIntent = 0f;
            reproductionIntent = 0f;
            socialIntent = 0f;
            foragingIntent = 0f;
            alive = true;
            // ConfigureLife is called for a new embodiment. Acquired fast
            // weights and memory must never leak across births.
            if (brain != null)
            {
                brain.ResetRuntimeState();
            }
        }

        public void RestoreLifeState(
            float restoredEnergy,
            float restoredAge,
            int restoredOffspringCount,
            int restoredKillCount,
            float restoredDamageTaken)
        {
            energy = Mathf.Clamp(restoredEnergy, 0f, maxEnergy);
            lifeAgeSeconds = Mathf.Clamp(restoredAge, 0f, maxAgeSeconds);
            offspringCount = Mathf.Max(0, restoredOffspringCount);
            killCount = Mathf.Max(0, restoredKillCount);
            damageTaken = Mathf.Max(0f, restoredDamageTaken);
            reproductionCooldownRemaining = 0f;
            deathReason = string.Empty;
            alive = true;
            evaluationActive = true;
        }

        /// <summary>
        /// Captures the complete embodiment state without copying Genome.
        /// The Brain runtime adapter is intentionally kept here because the
        /// archive contract is owned by the live creature, while Brain's
        /// inherited definition remains immutable and serializable as Genome.
        /// </summary>
        public void CaptureWorldState(WorldCreatureSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            snapshot.genomeId = Genome == null ? string.Empty : Genome.genomeId;
            snapshot.hasFullRuntimeState = true;
            snapshot.position = rootBody == null ? transform.position : rootBody.position;
            snapshot.rotation = rootBody == null ? transform.rotation : rootBody.rotation;
            snapshot.energy = energy;
            snapshot.age = lifeAgeSeconds;
            snapshot.offspringCount = offspringCount;
            snapshot.killCount = killCount;
            snapshot.damageTaken = damageTaken;
            snapshot.totalEnergyAcquired = totalEnergyAcquired;
            snapshot.reproductionCooldownRemaining = reproductionCooldownRemaining;
            snapshot.startX = startX;
            snapshot.bestX = bestX;
            snapshot.brainClock = brainClock;
            snapshot.alive = alive;
            snapshot.evaluationActive = evaluationActive;
            snapshot.deathReason = deathReason ?? string.Empty;
            snapshot.interactionIntent = interactionIntent;
            snapshot.reproductionIntent = reproductionIntent;
            snapshot.socialIntent = socialIntent;
            snapshot.foragingIntent = foragingIntent;
            if (snapshot.bodyParts == null)
            {
                snapshot.bodyParts = new List<WorldRigidbodySnapshot>();
            }
            snapshot.bodyParts.Clear();
            for (int i = 0; i < bodyParts.Count; i++)
            {
                Rigidbody bodyPart = bodyParts[i];
                if (bodyPart == null)
                {
                    continue;
                }

                snapshot.bodyParts.Add(new WorldRigidbodySnapshot
                {
                    index = i,
                    position = bodyPart.position,
                    rotation = bodyPart.rotation,
                    linearVelocity = bodyPart.linearVelocity,
                    angularVelocity = bodyPart.angularVelocity
                });
            }

            if (snapshot.brain == null)
            {
                snapshot.brain = new BrainRuntimeSnapshot();
            }
            brain.CaptureRuntimeState(snapshot.brain);
        }

        /// <summary>
        /// Restores a full live embodiment. Archives from schema 1/2 have no
        /// full-state marker and intentionally fall back to the legacy pose
        /// and life restoration path.
        /// </summary>
        public void RestoreWorldState(WorldCreatureSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (!snapshot.hasFullRuntimeState || snapshot.bodyParts == null || snapshot.bodyParts.Count == 0)
            {
                RestorePose(snapshot.position, snapshot.rotation);
                RestoreLifeState(snapshot.energy, snapshot.age, snapshot.offspringCount, snapshot.killCount, snapshot.damageTaken);
                return;
            }

            // Establish a safe fallback pose for any body part omitted from a
            // damaged/older schema-3 archive before applying indexed states.
            if (IsFinite(snapshot.position) && IsFinite(snapshot.rotation))
            {
                RestorePose(snapshot.position, snapshot.rotation);
            }

            for (int i = 0; i < snapshot.bodyParts.Count; i++)
            {
                WorldRigidbodySnapshot state = snapshot.bodyParts[i];
                if (state == null || state.index < 0 || state.index >= bodyParts.Count)
                {
                    continue;
                }

                Rigidbody bodyPart = bodyParts[state.index];
                if (bodyPart == null || !IsFinite(state.position) || !IsFinite(state.rotation))
                {
                    continue;
                }

                bodyPart.position = state.position;
                bodyPart.rotation = state.rotation;
                bodyPart.linearVelocity = IsFinite(state.linearVelocity) ? state.linearVelocity : Vector3.zero;
                bodyPart.angularVelocity = IsFinite(state.angularVelocity) ? state.angularVelocity : Vector3.zero;
            }

            energy = Mathf.Clamp(snapshot.energy, 0f, maxEnergy);
            lifeAgeSeconds = Mathf.Clamp(snapshot.age, 0f, maxAgeSeconds);
            offspringCount = Mathf.Max(0, snapshot.offspringCount);
            killCount = Mathf.Max(0, snapshot.killCount);
            damageTaken = Mathf.Max(0f, snapshot.damageTaken);
            totalEnergyAcquired = Mathf.Max(0f, snapshot.totalEnergyAcquired);
            reproductionCooldownRemaining = Mathf.Max(0f, snapshot.reproductionCooldownRemaining);
            startX = IsFinite(snapshot.startX) ? snapshot.startX : (rootBody == null ? 0f : rootBody.position.x);
            bestX = IsFinite(snapshot.bestX) ? snapshot.bestX : startX;
            brainClock = Mathf.Max(0f, snapshot.brainClock);
            alive = snapshot.alive;
            evaluationActive = snapshot.evaluationActive && alive;
            deathReason = snapshot.deathReason ?? string.Empty;
            interactionIntent = Mathf.Clamp01(snapshot.interactionIntent);
            reproductionIntent = Mathf.Clamp01(snapshot.reproductionIntent);
            socialIntent = Mathf.Clamp01(snapshot.socialIntent);
            foragingIntent = Mathf.Clamp01(snapshot.foragingIntent);
            brain.RestoreRuntimeState(snapshot.brain);
            if (!evaluationActive)
            {
                DisableMotors();
            }

            Physics.SyncTransforms();
        }

        public void RestorePose(Vector3 position, Quaternion rotation)
        {
            if (rootBody == null || !IsFinite(position))
            {
                return;
            }

            Quaternion deltaRotation = rotation * Quaternion.Inverse(rootBody.rotation);
            Vector3 oldRootPosition = rootBody.position;
            for (int i = 0; i < bodyParts.Count; i++)
            {
                Rigidbody bodyPart = bodyParts[i];
                if (bodyPart == null)
                {
                    continue;
                }

                bodyPart.position = position + deltaRotation * (bodyPart.position - oldRootPosition);
                bodyPart.rotation = deltaRotation * bodyPart.rotation;
                bodyPart.linearVelocity = Vector3.zero;
                bodyPart.angularVelocity = Vector3.zero;
            }

            Physics.SyncTransforms();
        }

        public void SetLifeTuning(
            float initialMetabolismPerSecond,
            float initialMaxAgeSeconds,
            float initialMaturityAgeSeconds,
            float initialReproductionEnergyThreshold,
            float initialReproductionCost,
            float initialReproductionCooldownSeconds)
        {
            metabolismPerSecond = Mathf.Max(0f, initialMetabolismPerSecond);
            maxAgeSeconds = Mathf.Max(1f, initialMaxAgeSeconds);
            maturityAgeSeconds = Mathf.Clamp(initialMaturityAgeSeconds, 0f, maxAgeSeconds);
            reproductionEnergyThreshold = Mathf.Clamp(
                initialReproductionEnergyThreshold,
                0f,
                maxEnergy);
            reproductionCost = Mathf.Clamp(initialReproductionCost, 0f, maxEnergy);
            reproductionCooldownSeconds = Mathf.Max(0f, initialReproductionCooldownSeconds);
        }

        public void SetResourceSensor(Func<Vector3, Vector3> provider)
        {
            resourcePositionProvider = provider;
        }

        public void SetInteractionSensor(Func<Vector3, CreatureInteractionObservation> provider)
        {
            interactionObservationProvider = provider;
        }

        public void TickLife(float deltaTime, float energyGained)
        {
            if (!alive || deltaTime <= 0f)
            {
                return;
            }

            lifeAgeSeconds += deltaTime;
            reproductionCooldownRemaining = Mathf.Max(
                0f,
                reproductionCooldownRemaining - deltaTime);
            float energyBefore = energy;
            float speed = rootBody == null || !IsFinite(rootBody.linearVelocity)
                ? 0f
                : rootBody.linearVelocity.magnitude;
            float efficiency = Genome == null || Genome.ecology == null
                ? 1f
                : Mathf.Max(0.25f, Genome.ecology.energyEfficiency);
            float energySpent = (metabolismPerSecond + speed * movementEnergyCost) * deltaTime / efficiency;
            float safeEnergyGained = Mathf.Max(0f, IsFinite(energyGained) ? energyGained : 0f);
            energy = Mathf.Clamp(energy + safeEnergyGained - energySpent, 0f, maxEnergy);
            totalEnergyAcquired += safeEnergyGained;

            float normalizedEnergyDelta = maxEnergy <= 0f
                ? 0f
                : (energy - energyBefore) / maxEnergy;
            if (brain != null)
            {
                brain.AccumulateHomeostaticFeedback(
                    normalizedEnergyDelta,
                    0f,
                    0f,
                    energy > 0.001f && lifeAgeSeconds < maxAgeSeconds);
            }

            if (energy <= 0.001f)
            {
                Die("Starvation");
            }
            else if (lifeAgeSeconds >= maxAgeSeconds)
            {
                Die("Old age");
            }
        }

        public bool TrySpendReproductionCost()
        {
            if (!CanReproduce || energy < reproductionCost)
            {
                return false;
            }

            energy = Mathf.Max(0f, energy - reproductionCost);
            reproductionCooldownRemaining = reproductionCooldownSeconds;
            offspringCount++;
            if (brain != null && maxEnergy > 0f)
            {
                brain.AccumulateHomeostaticFeedback(
                    -reproductionCost / maxEnergy,
                    0f,
                    0f,
                    true);
            }
            return true;
        }

        public float ApplyDamage(float amount, string reason)
        {
            if (!alive || amount <= 0f || !IsFinite(amount))
            {
                return 0f;
            }

            float applied = Mathf.Min(energy, amount);
            energy = Mathf.Max(0f, energy - applied);
            damageTaken += applied;
            if (brain != null && maxEnergy > 0f)
            {
                brain.AccumulateHomeostaticFeedback(
                    0f,
                    applied / maxEnergy,
                    0f,
                    energy > 0.001f);
            }
            if (energy <= 0.001f)
            {
                Die(string.IsNullOrEmpty(reason) ? "Interaction" : reason);
            }

            return applied;
        }

        public void AddEnergy(float amount)
        {
            if (!alive || amount <= 0f || !IsFinite(amount))
            {
                return;
            }

            energy = Mathf.Clamp(energy + amount, 0f, maxEnergy);
            if (brain != null && maxEnergy > 0f)
            {
                brain.AccumulateHomeostaticFeedback(
                    amount / maxEnergy,
                    0f,
                    0f,
                    true);
            }
        }

        public void RegisterKill()
        {
            if (alive)
            {
                killCount++;
            }
        }

        public void Die(string reason)
        {
            if (!alive)
            {
                return;
            }

            alive = false;
            deathReason = string.IsNullOrEmpty(reason) ? "Unknown" : reason;
            StopEvaluation();
        }

        public void SetPhysicsTuning(
            float driveForce,
            float targetSpeedDegrees,
            float damping,
            float evaluationSettlingDuration)
        {
            jointDriveForce = Mathf.Max(0f, driveForce);
            jointTargetSpeedDegrees = Mathf.Max(0f, targetSpeedDegrees);
            jointDamping = Mathf.Max(0f, damping);
            settlingDuration = Mathf.Clamp(evaluationSettlingDuration, 0f, 3f);

            for (int i = 0; i < joints.Count; i++)
            {
                ConfigurableJoint joint = i < joints.Count ? joints[i] : null;
                if (joint == null)
                {
                    continue;
                }

                int geneIndex = i + 1;
                float geneDriveStrength = geneIndex < partGenes.Length
                    ? partGenes[geneIndex].driveStrength
                    : 1f;
                JointDrive drive = joint.angularXDrive;
                drive.positionDamper = jointDamping;
                drive.maximumForce = geneDriveStrength * jointDriveForce;
                joint.angularXDrive = drive;
                JointDrive secondaryDrive = joint.angularYZDrive;
                secondaryDrive.positionDamper = jointDamping;
                secondaryDrive.maximumForce = geneDriveStrength * jointDriveForce;
                joint.angularYZDrive = secondaryDrive;
            }
        }

        public void BeginEvaluation()
        {
            if (rootBody == null)
            {
                return;
            }

            startX = rootBody.position.x;
            bestX = startX;
            brainClock = 0f;
            lifeAgeSeconds = 0f;
            alive = true;
            deathReason = string.Empty;
            evaluationActive = true;
            // BeginEvaluation marks a new lifetime for this embodiment.
            brain.ResetRuntimeState();
            DisableMotors();
        }

        public void StopEvaluation()
        {
            evaluationActive = false;
            DisableMotors();
        }

        /// <summary>
        /// Turns this embodiment into a non-simulated historical display.
        /// The genome and renderers remain available, but the preview cannot
        /// affect the live population or accumulate physics state.
        /// </summary>
        public void SetObservationPreview()
        {
            StopEvaluation();
            for (int i = 0; i < bodyParts.Count; i++)
            {
                Rigidbody bodyPart = bodyParts[i];
                if (bodyPart == null)
                {
                    continue;
                }

                bodyPart.isKinematic = true;
                bodyPart.useGravity = false;
                bodyPart.detectCollisions = false;
                bodyPart.linearVelocity = Vector3.zero;
                bodyPart.angularVelocity = Vector3.zero;
            }

            for (int i = 0; i < colliders.Count; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        private void DisableMotors()
        {
            for (int i = 0; i < joints.Count; i++)
            {
                if (joints[i] == null)
                {
                    continue;
                }

                joints[i].targetAngularVelocity = Vector3.zero;
            }
        }

        public CreatureEvaluationResult CaptureEvaluation()
        {
            float distance = CurrentDistance;
            return new CreatureEvaluationResult(
                Genome,
                SurvivalFitness,
                distance,
                energy,
                lifeAgeSeconds,
                offspringCount,
                deathReason,
                alive)
                .WithCombatStats(killCount, damageTaken);
        }

        public void SetSelected(bool value)
        {
            selected = value;
            if (bodyMaterial != null)
            {
                bodyMaterial.color = selected
                    ? Color.Lerp(baseColor, Color.white, 0.45f)
                    : baseColor;
            }
        }

        public void NotifyClicked()
        {
            if (Clicked != null)
            {
                Clicked(this);
            }
        }

        private void FixedUpdate()
        {
            if (!evaluationActive || rootBody == null || brain == null)
            {
                return;
            }

            if (!RepairInvalidPhysicsState())
            {
                return;
            }

            // Feedback generated by the previous simulation step modifies only
            // this creature's runtime fast weights. The inherited Genome is
            // never changed.
            brain.ApplyPendingLearning(Time.fixedDeltaTime);

            // Let the randomly assembled rigidbodies settle before a brain applies torque.
            if (brainClock < settlingDuration)
            {
                bestX = Mathf.Max(bestX, rootBody.position.x);
                brainClock += Time.fixedDeltaTime;
                return;
            }

            float[] observations = BuildObservations();
            float[] outputs = brain.Evaluate(observations);
            interactionIntent = outputs.Length > 8
                ? Mathf.Clamp01((outputs[8] + 1f) * 0.5f)
                : 0f;
            reproductionIntent = outputs.Length > 9
                ? Mathf.Clamp01((outputs[9] + 1f) * 0.5f)
                : 0f;
            socialIntent = outputs.Length > 10
                ? Mathf.Clamp01((outputs[10] + 1f) * 0.5f)
                : 0f;
            foragingIntent = outputs.Length > 11
                ? Mathf.Clamp01((outputs[11] + 1f) * 0.5f)
                : 0f;
            int motorChannel = 0;
            float controlCost = 0f;
            int controlledOutputs = 0;
            for (int i = 0; i < joints.Count; i++)
            {
                ConfigurableJoint joint = joints[i];
                if (joint == null)
                {
                    continue;
                }

                Vector3 targetVelocity = Vector3.zero;
                if (joint.angularXMotion != ConfigurableJointMotion.Locked && motorChannel < MotorChannelCount)
                {
                    float value = SafeClamp(outputs[motorChannel], -1f, 1f);
                    targetVelocity.x = value;
                    controlCost += Mathf.Abs(value);
                    controlledOutputs++;
                    motorChannel++;
                }

                if (joint.angularYMotion != ConfigurableJointMotion.Locked && motorChannel < MotorChannelCount)
                {
                    float value = SafeClamp(outputs[motorChannel], -1f, 1f);
                    targetVelocity.y = value;
                    controlCost += Mathf.Abs(value);
                    controlledOutputs++;
                    motorChannel++;
                }

                if (joint.angularZMotion != ConfigurableJointMotion.Locked && motorChannel < MotorChannelCount)
                {
                    float value = SafeClamp(outputs[motorChannel], -1f, 1f);
                    targetVelocity.z = value;
                    controlCost += Mathf.Abs(value);
                    controlledOutputs++;
                    motorChannel++;
                }

                joint.targetAngularVelocity = targetVelocity
                    * (jointTargetSpeedDegrees * Mathf.Deg2Rad);
            }

            if (controlledOutputs > 0)
            {
                brain.AccumulateControlCost(controlCost / controlledOutputs);
            }

            bestX = Mathf.Max(bestX, rootBody.position.x);
            brainClock += Time.fixedDeltaTime;
        }

        private bool RepairInvalidPhysicsState()
        {
            bool valid = true;
            for (int i = 0; i < bodyParts.Count; i++)
            {
                Rigidbody bodyPart = bodyParts[i];
                if (bodyPart == null || !IsFinite(bodyPart.position) || !IsFinite(bodyPart.linearVelocity)
                    || !IsFinite(bodyPart.angularVelocity) || bodyPart.position.sqrMagnitude > 1000000f)
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
            {
                return true;
            }

            // Physics can become unstable while experimenting with random topologies.
            // Reset only the invalid embodiment; the genome remains eligible for evaluation.
            for (int i = 0; i < bodyParts.Count; i++)
            {
                Rigidbody bodyPart = bodyParts[i];
                if (bodyPart == null || i >= safePositions.Length)
                {
                    continue;
                }

                bodyPart.position = safePositions[i];
                bodyPart.rotation = safeRotations[i];
                bodyPart.transform.position = safePositions[i];
                bodyPart.transform.rotation = safeRotations[i];
                bodyPart.linearVelocity = Vector3.zero;
                bodyPart.angularVelocity = Vector3.zero;
                bodyPart.Sleep();
            }

            Physics.SyncTransforms();

            return false;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static float SafeClamp(float value, float min, float max)
        {
            return IsFinite(value) ? Mathf.Clamp(value, min, max) : 0f;
        }

        private float[] BuildObservations()
        {
            float averageAngle = 0f;
            float averageVelocity = 0f;
            if (joints.Count > 0)
            {
                for (int i = 0; i < joints.Count; i++)
                {
                    if (joints[i] == null)
                    {
                        continue;
                    }

                    float limit = i + 1 < partGenes.Length ? Mathf.Max(20f, partGenes[i + 1].jointLimit) : 90f;
                    averageAngle += SafeClamp(EstimateJointAngle(i) / limit, -1f, 1f);
                    averageVelocity += SafeClamp(EstimateJointAngularVelocity(i) / 180f, -1f, 1f);
                }

                averageAngle /= joints.Count;
                averageVelocity /= joints.Count;
            }

            Vector3 velocity = IsFinite(rootBody.linearVelocity) ? rootBody.linearVelocity : Vector3.zero;
            Vector3 angularVelocity = IsFinite(rootBody.angularVelocity) ? rootBody.angularVelocity : Vector3.zero;
            float tilt = SafeClamp(Vector3.Dot(rootBody.transform.up, Vector3.up), -1f, 1f);
            float height = SafeClamp((rootBody.position.y - 0.45f) / 2.5f, -1f, 1f);
            Vector3 resourceWorldDirection = Vector3.zero;
            float resourceProximity = 0f;
            Vector3 individualWorldDirection = Vector3.zero;
            float individualProximity = 0f;
            Vector3 threatWorldDirection = Vector3.zero;
            float threatProximity = 0f;
            float obstacleProximity = 0f;
            int sensorCount = Genome == null || Genome.sensors == null ? 0 : Genome.sensors.Count;
            for (int sensorIndex = 0; sensorIndex < sensorCount && sensorIndex < CreatureGenome.MaxSensors; sensorIndex++)
            {
                SensorGene sensor = Genome.sensors[sensorIndex];
                int bodyIndex = Mathf.Clamp(sensor.bodyPartIndex, 0, bodyParts.Count - 1);
                Rigidbody sensorBody = bodyParts.Count == 0 ? null : bodyParts[bodyIndex];
                if (sensorBody == null)
                {
                    continue;
                }

                Vector3 sensorOrigin = sensorBody.transform.TransformPoint(sensor.localPosition);
                Vector3 sensorDirection = sensorBody.transform.TransformDirection(
                    sensor.localDirection.sqrMagnitude < 0.0001f ? Vector3.right : sensor.localDirection).normalized;
                float sensorRange = Mathf.Max(0.25f, (Genome.ecology == null ? 8f : Genome.ecology.sensorRange)
                    * Mathf.Clamp(sensor.rangeMultiplier, 0.25f, 2f));
                float sensorSensitivity = Mathf.Clamp(sensor.sensitivity, 0.05f, 3f);

                if (resourcePositionProvider != null)
                {
                    Vector3 resourcePosition = resourcePositionProvider(sensorOrigin);
                    Vector3 toResource = resourcePosition - sensorOrigin;
                    float distance = toResource.magnitude;
                    if (IsFinite(toResource) && distance > 0.0001f && distance <= sensorRange
                        && InFieldOfView(toResource / distance, sensorDirection, sensor.fieldOfView))
                    {
                        float value = Proximity(distance, sensorRange) * sensorSensitivity;
                        if (value > resourceProximity)
                        {
                            resourceProximity = value;
                            resourceWorldDirection = toResource / distance;
                        }
                    }
                }

                CreatureInteractionObservation interaction = interactionObservationProvider == null
                    ? CreatureInteractionObservation.Empty
                    : interactionObservationProvider(sensorOrigin);
                if (IsFinite(interaction.nearestIndividualDirection)
                    && InFieldOfView(interaction.nearestIndividualDirection, sensorDirection, sensor.fieldOfView))
                {
                    float value = Proximity(interaction.nearestIndividualDistance, sensorRange) * sensorSensitivity;
                    if (value > individualProximity)
                    {
                        individualProximity = value;
                        individualWorldDirection = interaction.nearestIndividualDirection.normalized;
                    }
                }
                if (IsFinite(interaction.nearestThreatDirection)
                    && InFieldOfView(interaction.nearestThreatDirection, sensorDirection, sensor.fieldOfView))
                {
                    float value = Proximity(interaction.nearestThreatDistance, sensorRange) * sensorSensitivity;
                    if (value > threatProximity)
                    {
                        threatProximity = value;
                        threatWorldDirection = interaction.nearestThreatDirection.normalized;
                    }
                }

                float obstacleValue = SafeClamp(interaction.obstacleProximity, 0f, 1f) * sensorSensitivity;
                if (InFieldOfView(rootBody.transform.forward, sensorDirection, sensor.fieldOfView))
                {
                    obstacleProximity = Mathf.Max(obstacleProximity, obstacleValue);
                }
            }

            Vector3 localResourceDirection = ToLocalDirection(resourceWorldDirection);
            Vector3 localIndividualDirection = ToLocalDirection(individualWorldDirection);
            Vector3 localThreatDirection = ToLocalDirection(threatWorldDirection);
            EcologyGene ecology = Genome == null ? null : Genome.ecology;

            return new[]
            {
                SafeClamp(velocity.x / 4f, -1f, 1f),
                SafeClamp(velocity.y / 4f, -1f, 1f),
                SafeClamp(angularVelocity.z / 8f, -1f, 1f),
                SafeClamp(tilt, -1f, 1f),
                averageAngle,
                averageVelocity,
                Mathf.Sin(brainClock * 2.15f),
                Mathf.Cos(brainClock * 2.15f),
                height,
                1f,
                EnergyRatio * 2f - 1f,
                SafeClamp(localResourceDirection.x, -1f, 1f),
                SafeClamp(localResourceDirection.z, -1f, 1f),
                resourceProximity * 2f - 1f,
                SafeClamp(localIndividualDirection.x, -1f, 1f),
                SafeClamp(localIndividualDirection.z, -1f, 1f),
                individualProximity * 2f - 1f,
                SafeClamp(localThreatDirection.x, -1f, 1f),
                SafeClamp(localThreatDirection.z, -1f, 1f),
                threatProximity * 2f - 1f,
                SafeClamp(Mathf.Clamp01(obstacleProximity) * 2f - 1f, -1f, 1f),
                ecology == null ? -1f : ecology.predationDrive * 2f - 1f
            };
        }

        private static bool InFieldOfView(Vector3 worldDirection, Vector3 sensorDirection, float fieldOfView)
        {
            if (!IsFinite(worldDirection) || worldDirection.sqrMagnitude < 0.0001f
                || !IsFinite(sensorDirection) || sensorDirection.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            float halfFov = Mathf.Clamp(fieldOfView, 10f, 360f) * 0.5f;
            return halfFov >= 179.9f
                || Vector3.Angle(worldDirection.normalized, sensorDirection.normalized) <= halfFov;
        }

        private Vector3 ToLocalDirection(Vector3 worldDirection)
        {
            if (!IsFinite(worldDirection) || worldDirection.sqrMagnitude < 0.0001f || rootBody == null)
            {
                return Vector3.zero;
            }

            return rootBody.transform.InverseTransformDirection(worldDirection.normalized);
        }

        private static float Proximity(float distance, float range)
        {
            if (!IsFinite(distance) || distance <= 0f)
            {
                return distance <= 0f ? 1f : 0f;
            }

            return Mathf.Clamp01(1f - distance / Mathf.Max(0.1f, range));
        }

        private float EstimateJointAngle(int index)
        {
            if (index < 0 || index >= joints.Count || index >= jointRestRelativeRotations.Length)
            {
                return 0f;
            }

            ConfigurableJoint joint = joints[index];
            if (joint == null || joint.connectedBody == null)
            {
                return 0f;
            }

            Quaternion currentRelative = Quaternion.Inverse(joint.connectedBody.rotation) * joint.transform.rotation;
            Quaternion delta = Quaternion.Inverse(jointRestRelativeRotations[index]) * currentRelative;
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (!IsFinite(angle) || axis.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            if (angle > 180f)
            {
                angle -= 360f;
            }

            Vector3 restAxis = jointRestRelativeRotations[index] * joint.axis.normalized;
            if (Vector3.Dot(axis, restAxis) < 0f)
            {
                angle = -angle;
            }

            return Mathf.Clamp(angle, -180f, 180f);
        }

        private float EstimateJointAngularVelocity(int index)
        {
            if (index < 0 || index >= joints.Count)
            {
                return 0f;
            }

            ConfigurableJoint joint = joints[index];
            Rigidbody child = joint == null ? null : joint.GetComponent<Rigidbody>();
            if (joint == null || child == null || joint.connectedBody == null)
            {
                return 0f;
            }

            Vector3 axisWorld = joint.transform.TransformDirection(joint.axis).normalized;
            Vector3 relativeAngularVelocity = child.angularVelocity - joint.connectedBody.angularVelocity;
            return Vector3.Dot(relativeAngularVelocity, axisWorld) * Mathf.Rad2Deg;
        }

        private void OnDestroy()
        {
            if (bodyMaterial != null)
            {
                Destroy(bodyMaterial);
                bodyMaterial = null;
            }
        }

    }
}
