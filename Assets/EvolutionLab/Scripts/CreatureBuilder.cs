using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLab
{
    /// <summary>
    /// Converts pure genome data into a visible, physical creature.
    /// </summary>
    public static class CreatureBuilder
    {
        public static Creature Build(CreatureGenome sourceGenome, Vector3 origin, Color color)
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
            var joints = new List<HingeJoint>(Mathf.Max(0, partCount - 1));
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
                HingeJoint joint = partObjects[i].AddComponent<HingeJoint>();
                joint.connectedBody = rigidbodies[parentIndex];
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = Vector3.left * (gene.length * 0.45f);
                Vector3 parentAnchorWorld = positions[i] + rotations[i] * joint.anchor;
                joint.connectedAnchor = partObjects[parentIndex].transform.InverseTransformPoint(parentAnchorWorld);
                joint.axis = Vector3.forward;
                joint.enableCollision = false;
                joint.breakForce = float.PositiveInfinity;
                joint.breakTorque = float.PositiveInfinity;

                JointLimits limits = joint.limits;
                limits.min = -gene.jointLimit;
                limits.max = gene.jointLimit;
                limits.bounciness = 0f;
                limits.contactDistance = 2f;
                joint.limits = limits;
                joint.useLimits = true;

                JointMotor motor = joint.motor;
                motor.targetVelocity = 0f;
                motor.force = gene.driveStrength * 25f;
                motor.freeSpin = false;
                joint.motor = motor;
                joint.useMotor = false;
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

            creature.Configure(genome, rigidbodies, joints, renderers, colliders, material, color);
            creature.BeginEvaluation();
            return creature;
        }
    }
}
