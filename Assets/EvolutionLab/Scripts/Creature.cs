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
        private readonly List<HingeJoint> joints = new List<HingeJoint>();
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<Collider> colliders = new List<Collider>();

        private BodyPartGene[] partGenes = Array.Empty<BodyPartGene>();
        private Vector3[] safePositions = Array.Empty<Vector3>();
        private Quaternion[] safeRotations = Array.Empty<Quaternion>();
        private Brain brain;
        private Rigidbody rootBody;
        private Material bodyMaterial;
        private Color baseColor;
        private float startX;
        private float bestX;
        private float brainClock;
        private bool evaluationActive;
        private bool selected;

        public event Action<Creature> Clicked;

        public CreatureGenome Genome { get; private set; }

        public Brain Brain { get { return brain; } }

        public Rigidbody RootBody { get { return rootBody; } }

        public int BodyPartCount { get { return bodyParts.Count; } }

        public int JointCount { get { return joints.Count; } }

        public float AgeSeconds { get { return brainClock; } }

        public float CurrentDistance
        {
            get { return Mathf.Max(0f, bestX - startX); }
        }

        public float Fitness
        {
            get { return CurrentDistance; }
        }

        public IReadOnlyList<Collider> Colliders
        {
            get { return colliders; }
        }

        public void Configure(
            CreatureGenome genome,
            IList<Rigidbody> rigidbodies,
            IList<HingeJoint> hingeJoints,
            IList<Renderer> bodyRenderers,
            IList<Collider> bodyColliders,
            Material material,
            Color color)
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

            if (hingeJoints != null)
            {
                joints.AddRange(hingeJoints);
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

            brain = new Brain(genome == null ? null : genome.brain);
            rootBody = bodyParts.Count > 0 ? bodyParts[0] : null;
            bodyMaterial = material;
            baseColor = color;
            SetSelected(false);
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
            evaluationActive = true;
            DisableMotors();
        }

        public void StopEvaluation()
        {
            evaluationActive = false;
            DisableMotors();
        }

        private void DisableMotors()
        {
            for (int i = 0; i < joints.Count; i++)
            {
                if (joints[i] == null)
                {
                    continue;
                }

                JointMotor motor = joints[i].motor;
                motor.targetVelocity = 0f;
                joints[i].motor = motor;
                joints[i].useMotor = false;
            }
        }

        public CreatureEvaluationResult CaptureEvaluation()
        {
            float distance = CurrentDistance;
            return new CreatureEvaluationResult(Genome, distance, distance);
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
            if (brainClock < 0.5f)
            {
                bestX = Mathf.Max(bestX, rootBody.position.x);
                brainClock += Time.fixedDeltaTime;
                return;
            }

            float[] observations = BuildObservations();
            float[] outputs = brain.Evaluate(observations);
            for (int i = 0; i < joints.Count && i < outputs.Length; i++)
            {
                HingeJoint joint = joints[i];
                if (joint == null)
                {
                    continue;
                }

                int geneIndex = i + 1;
                float driveStrength = geneIndex < partGenes.Length ? partGenes[geneIndex].driveStrength : 1f;
                if (!IsFinite(outputs[i]))
                {
                    outputs[i] = 0f;
                }

                JointMotor motor = joint.motor;
                motor.targetVelocity = outputs[i] * 80f;
                motor.force = driveStrength * 25f;
                motor.freeSpin = false;
                joint.motor = motor;
                joint.useMotor = true;
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
                    averageAngle += SafeClamp(joints[i].angle / limit, -1f, 1f);
                    averageVelocity += SafeClamp(joints[i].velocity / 180f, -1f, 1f);
                }

                averageAngle /= joints.Count;
                averageVelocity /= joints.Count;
            }

            Vector3 velocity = IsFinite(rootBody.linearVelocity) ? rootBody.linearVelocity : Vector3.zero;
            Vector3 angularVelocity = IsFinite(rootBody.angularVelocity) ? rootBody.angularVelocity : Vector3.zero;
            float tilt = SafeClamp(Vector3.Dot(rootBody.transform.up, Vector3.up), -1f, 1f);
            float height = SafeClamp((rootBody.position.y - 0.45f) / 2.5f, -1f, 1f);

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
                1f
            };
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
