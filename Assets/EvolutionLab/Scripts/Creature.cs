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

        public bool CanReproduce
        {
            get
            {
                return alive
                    && lifeAgeSeconds >= maturityAgeSeconds
                    && reproductionCooldownRemaining <= 0f
                    && energy >= reproductionEnergyThreshold;
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
                    + energy * 0.1f
                    + CurrentDistance * 0.2f);
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
            Genome = genome;
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

            partGenes = genome == null || genome.bodyParts == null
                ? Array.Empty<BodyPartGene>()
                : genome.bodyParts.ToArray();
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

            brain = new Brain(genome == null ? null : genome.brain);
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
            alive = true;
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
            float speed = rootBody == null || !IsFinite(rootBody.linearVelocity)
                ? 0f
                : rootBody.linearVelocity.magnitude;
            float energySpent = (metabolismPerSecond + speed * movementEnergyCost) * deltaTime;
            float safeEnergyGained = Mathf.Max(0f, IsFinite(energyGained) ? energyGained : 0f);
            energy = Mathf.Clamp(energy + safeEnergyGained - energySpent, 0f, maxEnergy);
            totalEnergyAcquired += safeEnergyGained;

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
            return true;
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
                ConfigurableJoint joint = joints[i];
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
                alive);
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

            // Let the randomly assembled rigidbodies settle before a brain applies torque.
            if (brainClock < settlingDuration)
            {
                bestX = Mathf.Max(bestX, rootBody.position.x);
                brainClock += Time.fixedDeltaTime;
                return;
            }

            float[] observations = BuildObservations();
            float[] outputs = brain.Evaluate(observations);
            for (int i = 0; i < joints.Count && i < outputs.Length; i++)
            {
                ConfigurableJoint joint = joints[i];
                if (joint == null)
                {
                    continue;
                }

                int geneIndex = i + 1;
                if (!IsFinite(outputs[i]))
                {
                    outputs[i] = 0f;
                }

                // ConfigurableJoint target angular velocity is expressed in
                // radians per second in joint space. Its primary local X axis
                // is mapped to the genome's single prototype actuator.
                joint.targetAngularVelocity = Vector3.right
                    * (outputs[i] * jointTargetSpeedDegrees * Mathf.Deg2Rad);
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
            Vector3 localResourceDirection = Vector3.zero;
            float resourceProximity = 0f;
            if (resourcePositionProvider != null)
            {
                Vector3 resourcePosition = resourcePositionProvider(rootBody.position);
                Vector3 toResource = resourcePosition - rootBody.position;
                if (IsFinite(toResource) && toResource.sqrMagnitude > 0.0001f)
                {
                    float resourceDistance = toResource.magnitude;
                    localResourceDirection = rootBody.transform.InverseTransformDirection(
                        toResource / resourceDistance);
                    resourceProximity = SafeClamp(1f - resourceDistance / 12f, 0f, 1f);
                }
            }

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
                resourceProximity * 2f - 1f
            };
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
