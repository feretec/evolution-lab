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
            CreatureGenome genome = sourceGenome == null ? new CreatureGenome() : sourceGenome;
            genome.Repair();

            GameObject container = new GameObject("Creature_" + genome.genomeId);
            container.transform.position = Vector3.zero;
            Creature creature = container.AddComponent<Creature>();

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader)
            {
                color = color,
                enableInstancing = true
            };

            int partCount = genome.bodyParts.Count;
            var positions = new Vector3[partCount];
            var rotations = new Quaternion[partCount];
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
                Vector3 parentAnchorLocal = direction * parentAnchorDistance;
                Quaternion childRotation = rotations[parentIndex] * Quaternion.Euler(gene.localEulerAngles);
                Vector3 parentAnchorWorld = positions[parentIndex] + rotations[parentIndex] * parentAnchorLocal;
                Vector3 childAnchorLocal = Vector3.left * (gene.length * 0.45f);

                positions[i] = parentAnchorWorld - childRotation * childAnchorLocal;
                rotations[i] = childRotation;
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
                    renderer.sharedMaterial = material;
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
                bodyPart.Configure(creature, i);

                partObjects[i] = partObject;
                rigidbodies.Add(rigidbody);
            }

            for (int i = 1; i < partCount; i++)
            {
                BodyPartGene gene = genome.bodyParts[i];
                int parentIndex = Mathf.Clamp(gene.parentIndex, 0, i - 1);
                ConfigurableJoint joint = partObjects[i].AddComponent<ConfigurableJoint>();
                joint.connectedBody = rigidbodies[parentIndex];
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = Vector3.left * (gene.length * 0.45f);
                Vector3 parentAnchorWorld = positions[i] + rotations[i] * joint.anchor;
                joint.connectedAnchor = partObjects[parentIndex].transform.InverseTransformPoint(parentAnchorWorld);
                joint.axis = Vector3.forward;
                joint.secondaryAxis = Vector3.up;
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
                joint.angularYMotion = ConfigurableJointMotion.Locked;
                joint.angularZMotion = ConfigurableJointMotion.Locked;

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
                    positionDamper = 0f,
                    maximumForce = 0f
                };
                joint.targetAngularVelocity = Vector3.zero;
                joint.projectionMode = JointProjectionMode.PositionAndRotation;
                joint.projectionDistance = 0.08f;
                joint.projectionAngle = 8f;
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
    }
}
