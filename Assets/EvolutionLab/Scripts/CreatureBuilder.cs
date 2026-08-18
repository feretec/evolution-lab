using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLab
{
    /// <summary>
    /// Converts pure genome data into a visible, physical creature.
    /// </summary>
    public static class CreatureBuilder
    {
        public static Creature Build(
            CreatureGenome sourceGenome,
            Vector3 origin,
            Color color,
            float jointDriveForce,
            float jointTargetSpeedDegrees,
            float jointDamping,
            float settlingDuration)
        {
            // A runtime embodiment owns a repaired snapshot. This prevents
            // physics/display code from mutating the genome held by the
            // population engine or a history archive.
            CreatureGenome genome = sourceGenome == null
                ? new CreatureGenome()
                : sourceGenome.Clone();
            genome.Repair();

            GameObject container = new GameObject("Creature_" + genome.genomeId);
            container.transform.position = Vector3.zero;
            Creature creature = container.AddComponent<Creature>();

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = null;
            if (shader != null)
            {
                material = new Material(shader)
                {
                    color = color,
                    enableInstancing = true
                };
            }

            int partCount = genome.bodyParts.Count;
            var positions = new Vector3[partCount];
            var rotations = new Quaternion[partCount];
            var connectionPoints = new Vector3[partCount];
            var partObjects = new GameObject[partCount];
            var rigidbodies = new List<Rigidbody>(partCount);
            var joints = new List<ConfigurableJoint>(Mathf.Max(0, partCount - 1));
            var renderers = new List<Renderer>(partCount);
            var colliders = new List<Collider>(partCount);

            positions[0] = origin;
            rotations[0] = Quaternion.Euler(genome.bodyParts[0].localEulerAngles);
            for (int i = 1; i < partCount; i++)
            {
                BodyPartGene gene = genome.bodyParts[i];
                int parentIndex = Mathf.Clamp(gene.parentIndex, 0, i - 1);
                BodyPartGene parentGene = genome.bodyParts[parentIndex];
                Vector3 direction = gene.localOffset.sqrMagnitude > 0.001f
                    ? gene.localOffset.normalized
                    : Vector3.right;
                float parentAnchorDistance = Mathf.Clamp(
                    gene.localOffset.magnitude,
                    parentGene.length * 0.18f,
                    parentGene.length * 0.47f);
                Vector3 parentHalfExtents = new Vector3(
                    parentGene.length * 0.5f,
                    parentGene.thickness * 0.5f,
                    parentGene.thickness * 0.5f);
                float maxVisibleAttachmentDistance = DistanceToBoxBoundary(direction, parentHalfExtents);
                float attachmentDistance = Mathf.Min(
                    parentAnchorDistance,
                    maxVisibleAttachmentDistance * 0.98f);
                Vector3 parentAnchorLocal = direction * Mathf.Max(0f, attachmentDistance);
                Quaternion childRotation = rotations[parentIndex] * Quaternion.Euler(gene.localEulerAngles);
                Vector3 parentAnchorWorld = positions[parentIndex] + rotations[parentIndex] * parentAnchorLocal;
                Vector3 childAnchorWorldOffset = childRotation * (Vector3.left * (gene.length * 0.45f));

                positions[i] = parentAnchorWorld - childAnchorWorldOffset;
                rotations[i] = childRotation;
                connectionPoints[i] = parentAnchorWorld;
            }

            for (int i = 0; i < partCount; i++)
            {
                BodyPartGene gene = genome.bodyParts[i];
                GameObject partObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                partObject.name = "BodyPart_" + i.ToString("00");
                partObject.transform.SetParent(container.transform, true);
                partObject.transform.position = positions[i];
                partObject.transform.rotation = rotations[i];
                partObject.transform.localScale = new Vector3(gene.length, gene.thickness, gene.thickness);

                Renderer renderer = partObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    if (material != null)
                    {
                        renderer.sharedMaterial = material;
                    }
                    renderers.Add(renderer);
                }

                Collider collider = partObject.GetComponent<Collider>();
                if (collider != null)
                {
                    colliders.Add(collider);
                }

                CreatureBodyPart bodyPart = partObject.AddComponent<CreatureBodyPart>();
                Rigidbody rigidbody = partObject.AddComponent<Rigidbody>();
                rigidbody.mass = gene.mass;
                rigidbody.linearDamping = 0.35f;
                rigidbody.angularDamping = 1.25f;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rigidbody.maxAngularVelocity = 30f;
                // Long articulated chains need a little more solver budget, while
                // limiting depenetration prevents a bad spawn from exploding the
                // whole individual before its brain can be evaluated.
                rigidbody.solverIterations = 12;
                rigidbody.solverVelocityIterations = 8;
                rigidbody.maxDepenetrationVelocity = 5f;
                bodyPart.Configure(creature, i);

                partObjects[i] = partObject;
                rigidbodies.Add(rigidbody);
            }

            // Sensors are visual observation markers only. They follow their
            // host body parts and never participate in physics.
            for (int sensorIndex = 0; sensorIndex < genome.sensors.Count; sensorIndex++)
            {
                SensorGene sensor = genome.sensors[sensorIndex];
                if (sensor.bodyPartIndex < 0 || sensor.bodyPartIndex >= partObjects.Length)
                {
                    continue;
                }

                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "SensorMarker_" + sensorIndex.ToString("00");
                marker.transform.SetParent(partObjects[sensor.bodyPartIndex].transform, false);
                marker.transform.localPosition = sensor.localPosition;
                marker.transform.localScale = Vector3.one * 0.1f;
                Collider markerCollider = marker.GetComponent<Collider>();
                if (markerCollider != null)
                {
                    markerCollider.enabled = false;
                }
                Renderer markerRenderer = marker.GetComponent<Renderer>();
                if (markerRenderer != null)
                {
                    if (material != null)
                    {
                        markerRenderer.sharedMaterial = material;
                    }
                    renderers.Add(markerRenderer);
                }
            }

            // The mouth is an observation marker only. It follows its host
            // body part, has no collider, and therefore cannot change physics.
            MouthGene mouth = genome.mouth;
            if (mouth.bodyPartIndex >= 0 && mouth.bodyPartIndex < partObjects.Length)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "MouthMarker";
                marker.transform.SetParent(partObjects[mouth.bodyPartIndex].transform, false);
                marker.transform.localPosition = mouth.localPosition;
                marker.transform.localRotation = Quaternion.FromToRotation(
                    Vector3.forward,
                    mouth.localDirection.sqrMagnitude < 0.0001f ? Vector3.right : mouth.localDirection);
                float markerSize = Mathf.Clamp(mouth.reach * 0.12f, 0.04f, 0.22f);
                marker.transform.localScale = Vector3.one * markerSize;
                Collider markerCollider = marker.GetComponent<Collider>();
                if (markerCollider != null)
                {
                    markerCollider.enabled = false;
                }
                Renderer markerRenderer = marker.GetComponent<Renderer>();
                if (markerRenderer != null)
                {
                    if (material != null)
                    {
                        markerRenderer.sharedMaterial = material;
                    }
                    renderers.Add(markerRenderer);
                }
            }

            // Ensure all scaled transforms are reflected in the physics scene
            // before ConfigurableJoints cache their initial anchor state.
            Physics.SyncTransforms();

            for (int i = 1; i < partCount; i++)
            {
                BodyPartGene gene = genome.bodyParts[i];
                int parentIndex = Mathf.Clamp(gene.parentIndex, 0, i - 1);
                ConfigurableJoint joint = partObjects[i].AddComponent<ConfigurableJoint>();
                joint.connectedBody = rigidbodies[parentIndex];
                joint.autoConfigureConnectedAnchor = false;
                Vector3 connectionPoint = connectionPoints[i];
                // Anchor values are local to scaled body transforms. Resolve both
                // sides from one world-space point so the physics seam and the
                // visible seam stay identical even when length/thickness mutate.
                joint.anchor = partObjects[i].transform.InverseTransformPoint(connectionPoint);
                joint.connectedAnchor = partObjects[parentIndex].transform.InverseTransformPoint(connectionPoint);
                joint.axis = SafeAxis(gene.primaryAxis, Vector3.forward);
                joint.secondaryAxis = SafeSecondaryAxis(gene.secondaryAxis, joint.axis);
                joint.configuredInWorldSpace = false;
                joint.enableCollision = false;
                joint.breakForce = float.PositiveInfinity;
                joint.breakTorque = float.PositiveInfinity;

                // ConfigurableJoint uses its primary (local X) axis for angular X.
                // The primary axis is oriented along local forward so the current
                // prototype still bends in the ground plane while leaving a clean
                // seam for future multi-axis joint genes.
                // ConfigurableJoint defaults to free linear motion; explicitly lock
                // all three linear axes so a joint cannot pull the body apart.
                joint.xMotion = ConfigurableJointMotion.Locked;
                joint.yMotion = ConfigurableJointMotion.Locked;
                joint.zMotion = ConfigurableJointMotion.Locked;
                joint.angularXMotion = ConfigurableJointMotion.Limited;
                joint.angularYMotion = gene.jointYLimit > 0.5f
                    ? ConfigurableJointMotion.Limited
                    : ConfigurableJointMotion.Locked;
                joint.angularZMotion = gene.jointZLimit > 0.5f
                    ? ConfigurableJointMotion.Limited
                    : ConfigurableJointMotion.Locked;

                SoftJointLimit lowLimit = joint.lowAngularXLimit;
                lowLimit.limit = -gene.jointLimit;
                lowLimit.bounciness = 0f;
                lowLimit.contactDistance = 2f;
                joint.lowAngularXLimit = lowLimit;

                SoftJointLimit highLimit = joint.highAngularXLimit;
                highLimit.limit = gene.jointLimit;
                highLimit.bounciness = 0f;
                highLimit.contactDistance = 2f;
                joint.highAngularXLimit = highLimit;

                SoftJointLimit yLimit = joint.angularYLimit;
                yLimit.limit = Mathf.Clamp(gene.jointYLimit, 0f, 170f);
                yLimit.bounciness = 0f;
                yLimit.contactDistance = 2f;
                joint.angularYLimit = yLimit;

                SoftJointLimit zLimit = joint.angularZLimit;
                zLimit.limit = Mathf.Clamp(gene.jointZLimit, 0f, 170f);
                zLimit.bounciness = 0f;
                zLimit.contactDistance = 2f;
                joint.angularZLimit = zLimit;

                joint.rotationDriveMode = RotationDriveMode.XYAndZ;
                joint.angularXDrive = new JointDrive
                {
                    positionSpring = 0f,
                    positionDamper = jointDamping,
                    maximumForce = gene.driveStrength * jointDriveForce
                };
                joint.angularYZDrive = new JointDrive
                {
                    positionSpring = 0f,
                    positionDamper = jointDamping,
                    maximumForce = gene.driveStrength * jointDriveForce
                };
                joint.targetAngularVelocity = Vector3.zero;
                joint.projectionMode = JointProjectionMode.PositionAndRotation;
                // Keep a small correction envelope. A near-zero projection
                // distance can inject large impulses into a mutated chain.
                joint.projectionDistance = 0.05f;
                joint.projectionAngle = 6f;
                joints.Add(joint);
            }

            // Random founder topologies can overlap at spawn. Keep self-collision out of
            // the locomotion experiment so the result reflects the controller/body pair.
            for (int i = 0; i < colliders.Count; i++)
            {
                for (int j = i + 1; j < colliders.Count; j++)
                {
                    if (colliders[i] != null && colliders[j] != null)
                    {
                        Physics.IgnoreCollision(colliders[i], colliders[j], true);
                    }
                }
            }

            creature.Configure(
                genome,
                rigidbodies,
                joints,
                renderers,
                colliders,
                material,
                color,
                jointDriveForce,
                jointTargetSpeedDegrees,
                jointDamping,
                settlingDuration);
            creature.BeginEvaluation();
            return creature;
        }

        private static float DistanceToBoxBoundary(Vector3 direction, Vector3 halfExtents)
        {
            float distance = float.PositiveInfinity;
            if (Mathf.Abs(direction.x) > 0.0001f)
            {
                distance = Mathf.Min(distance, halfExtents.x / Mathf.Abs(direction.x));
            }

            if (Mathf.Abs(direction.y) > 0.0001f)
            {
                distance = Mathf.Min(distance, halfExtents.y / Mathf.Abs(direction.y));
            }

            if (Mathf.Abs(direction.z) > 0.0001f)
            {
                distance = Mathf.Min(distance, halfExtents.z / Mathf.Abs(direction.z));
            }

            return float.IsInfinity(distance) ? 0f : distance;
        }

        private static Vector3 SafeAxis(Vector3 axis, Vector3 fallback)
        {
            if (!IsFinite(axis) || axis.sqrMagnitude < 0.0001f)
            {
                return fallback;
            }

            return axis.normalized;
        }

        private static Vector3 SafeSecondaryAxis(Vector3 secondary, Vector3 primary)
        {
            Vector3 result = Vector3.ProjectOnPlane(secondary, primary);
            if (!IsFinite(result) || result.sqrMagnitude < 0.0001f)
            {
                result = Vector3.Cross(primary, Vector3.up);
                if (result.sqrMagnitude < 0.0001f)
                {
                    result = Vector3.Cross(primary, Vector3.right);
                }
            }

            return result.normalized;
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
