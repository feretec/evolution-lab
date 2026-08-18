using System;
using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLab
{
    internal static class GenomeRandom
    {
        public static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        public static float Signed(System.Random random, float magnitude)
        {
            return Range(random, -magnitude, magnitude);
        }

        public static bool Chance(System.Random random, float probability)
        {
            return random.NextDouble() < probability;
        }
    }

    [Serializable]
    public struct BodyPartGene
    {
        public int parentIndex;
        public Vector3 localOffset;
        public Vector3 localEulerAngles;
        public float length;
        public float thickness;
        public float mass;
        public float jointLimit;
        public float driveStrength;

        public static BodyPartGene CreateRoot(float length, float thickness, float mass)
        {
            return new BodyPartGene
            {
                parentIndex = -1,
                localOffset = Vector3.zero,
                localEulerAngles = Vector3.zero,
                length = length,
                thickness = thickness,
                mass = mass,
                jointLimit = 90f,
                driveStrength = 1f
            };
        }
    }

    [Serializable]
    public sealed class BrainGene
    {
        public const int InputCount = 10;
        public const int HiddenCount = 8;
        public const int MaxOutputCount = 8;

        public float[] inputHiddenWeights;
        public float[] hiddenBiases;
        public float[] hiddenOutputWeights;
        public float[] outputBiases;

        public BrainGene()
        {
            EnsureShape();
        }

        public static BrainGene CreateRandom(System.Random random)
        {
            var gene = new BrainGene();
            for (int i = 0; i < gene.inputHiddenWeights.Length; i++)
            {
                gene.inputHiddenWeights[i] = GenomeRandom.Signed(random, 0.9f);
            }

            for (int i = 0; i < gene.hiddenBiases.Length; i++)
            {
                gene.hiddenBiases[i] = GenomeRandom.Signed(random, 0.35f);
            }

            for (int i = 0; i < gene.hiddenOutputWeights.Length; i++)
            {
                gene.hiddenOutputWeights[i] = GenomeRandom.Signed(random, 0.9f);
            }

            for (int i = 0; i < gene.outputBiases.Length; i++)
            {
                gene.outputBiases[i] = GenomeRandom.Signed(random, 0.35f);
            }

            return gene;
        }

        public BrainGene Clone()
        {
            EnsureShape();
            var clone = new BrainGene
            {
                inputHiddenWeights = (float[])inputHiddenWeights.Clone(),
                hiddenBiases = (float[])hiddenBiases.Clone(),
                hiddenOutputWeights = (float[])hiddenOutputWeights.Clone(),
                outputBiases = (float[])outputBiases.Clone()
            };
            return clone;
        }

        public void Mutate(System.Random random, float mutationRate)
        {
            EnsureShape();
            MutateArray(random, inputHiddenWeights, mutationRate, 0.55f);
            MutateArray(random, hiddenBiases, mutationRate, 0.4f);
            MutateArray(random, hiddenOutputWeights, mutationRate, 0.55f);
            MutateArray(random, outputBiases, mutationRate, 0.4f);
        }

        public void EnsureShape()
        {
            inputHiddenWeights = Resize(inputHiddenWeights, InputCount * HiddenCount);
            hiddenBiases = Resize(hiddenBiases, HiddenCount);
            hiddenOutputWeights = Resize(hiddenOutputWeights, HiddenCount * MaxOutputCount);
            outputBiases = Resize(outputBiases, MaxOutputCount);
        }

        private static void MutateArray(System.Random random, float[] values, float mutationRate, float step)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!GenomeRandom.Chance(random, mutationRate))
                {
                    continue;
                }

                values[i] = Mathf.Clamp(values[i] + GenomeRandom.Signed(random, step), -3f, 3f);
            }
        }

        private static float[] Resize(float[] source, int length)
        {
            if (source != null && source.Length == length)
            {
                return source;
            }

            var result = new float[length];
            if (source != null)
            {
                Array.Copy(source, result, Mathf.Min(source.Length, result.Length));
            }

            return result;
        }
    }

    [Serializable]
    public sealed class CreatureGenome
    {
        public const int CurrentSchemaVersion = 1;
        public const int MinBodyParts = 2;
        public const int MaxBodyParts = 9;

        public int schemaVersion = CurrentSchemaVersion;
        public string genomeId = string.Empty;
        public string parentId = string.Empty;
        public string secondaryParentId = string.Empty;
        public int generation;
        public float mutationRate = 0.16f;
        public List<BodyPartGene> bodyParts = new List<BodyPartGene>();
        public BrainGene brain = new BrainGene();

        public int JointCount
        {
            get { return Mathf.Max(0, bodyParts == null ? 0 : bodyParts.Count - 1); }
        }

        public CreatureGenome Clone()
        {
            var clone = new CreatureGenome
            {
                schemaVersion = schemaVersion,
                genomeId = genomeId,
                parentId = parentId,
                secondaryParentId = secondaryParentId,
                generation = generation,
                mutationRate = mutationRate,
                brain = brain == null ? new BrainGene() : brain.Clone(),
                bodyParts = new List<BodyPartGene>()
            };

            if (bodyParts != null)
            {
                clone.bodyParts.AddRange(bodyParts);
            }

            clone.Repair();
            return clone;
        }

        public static CreatureGenome CreateFounder(System.Random random, int generation, string id)
        {
            var genome = new CreatureGenome
            {
                generation = generation,
                genomeId = id,
                parentId = string.Empty,
                secondaryParentId = string.Empty,
                mutationRate = GenomeRandom.Range(random, 0.10f, 0.22f),
                bodyParts = new List<BodyPartGene>(),
                brain = BrainGene.CreateRandom(random)
            };

            int partCount = random.Next(3, 7);
            var root = BodyPartGene.CreateRoot(
                GenomeRandom.Range(random, 0.85f, 1.45f),
                GenomeRandom.Range(random, 0.28f, 0.48f),
                GenomeRandom.Range(random, 0.9f, 2.0f));
            genome.bodyParts.Add(root);

            for (int i = 1; i < partCount; i++)
            {
                int parentIndex = random.Next(0, i);
                BodyPartGene parent = genome.bodyParts[parentIndex];
                float length = GenomeRandom.Range(random, 0.45f, 1.25f);
                float thickness = GenomeRandom.Range(random, 0.14f, 0.34f);
                float pitch = GenomeRandom.Range(random, -80f, 80f);
                float yaw = GenomeRandom.Range(random, -30f, 30f);
                Quaternion direction = Quaternion.Euler(0f, yaw, pitch);
                float gap = GenomeRandom.Range(random, parent.length * 0.42f, parent.length * 0.78f);

                genome.bodyParts.Add(new BodyPartGene
                {
                    parentIndex = parentIndex,
                    localOffset = direction * (Vector3.right * gap),
                    localEulerAngles = new Vector3(0f, yaw, pitch),
                    length = length,
                    thickness = thickness,
                    mass = GenomeRandom.Range(random, 0.25f, 1.25f),
                    jointLimit = GenomeRandom.Range(random, 35f, 145f),
                    driveStrength = GenomeRandom.Range(random, 0.45f, 1.6f)
                });
            }

            genome.Repair();
            return genome;
        }

        public static CreatureGenome Crossover(
            CreatureGenome first,
            CreatureGenome second,
            System.Random random,
            int generation,
            string id)
        {
            CreatureGenome a = first ?? second;
            CreatureGenome b = second ?? first;
            if (a == null)
            {
                return CreateFounder(random, generation, id);
            }

            var child = a.Clone();
            child.genomeId = id;
            child.parentId = a.genomeId;
            child.secondaryParentId = b == null ? string.Empty : b.genomeId;
            child.generation = generation;
            child.mutationRate = b == null
                ? a.mutationRate
                : Mathf.Lerp(a.mutationRate, b.mutationRate, 0.5f);

            if (b != null && b.bodyParts != null)
            {
                int sharedParts = Mathf.Min(child.bodyParts.Count, b.bodyParts.Count);
                for (int i = 0; i < sharedParts; i++)
                {
                    if (!GenomeRandom.Chance(random, 0.42f))
                    {
                        continue;
                    }

                    BodyPartGene part = child.bodyParts[i];
                    BodyPartGene other = b.bodyParts[i];
                    part.length = Mathf.Lerp(part.length, other.length, 0.5f);
                    part.thickness = Mathf.Lerp(part.thickness, other.thickness, 0.5f);
                    part.mass = Mathf.Lerp(part.mass, other.mass, 0.5f);
                    part.jointLimit = Mathf.LerpAngle(part.jointLimit, other.jointLimit, 0.5f);
                    part.driveStrength = Mathf.Lerp(part.driveStrength, other.driveStrength, 0.5f);
                    part.localOffset = Vector3.Lerp(part.localOffset, other.localOffset, 0.5f);
                    part.localEulerAngles = Vector3.Lerp(part.localEulerAngles, other.localEulerAngles, 0.5f);
                    child.bodyParts[i] = part;
                }
            }

            if (b != null && b.brain != null)
            {
                child.brain = child.brain ?? new BrainGene();
                child.brain.EnsureShape();
                b.brain.EnsureShape();
                MixArrays(random, child.brain.inputHiddenWeights, b.brain.inputHiddenWeights);
                MixArrays(random, child.brain.hiddenBiases, b.brain.hiddenBiases);
                MixArrays(random, child.brain.hiddenOutputWeights, b.brain.hiddenOutputWeights);
                MixArrays(random, child.brain.outputBiases, b.brain.outputBiases);
            }

            child.Repair();
            return child;
        }

        public void Mutate(System.Random random)
        {
            Repair();
            mutationRate = Mathf.Clamp(
                mutationRate * (1f + GenomeRandom.Signed(random, 0.18f)),
                0.04f,
                0.38f);

            for (int i = 0; i < bodyParts.Count; i++)
            {
                BodyPartGene part = bodyParts[i];
                if (GenomeRandom.Chance(random, mutationRate))
                {
                    part.length = Mathf.Clamp(part.length + GenomeRandom.Signed(random, 0.18f), 0.3f, 1.8f);
                }

                if (GenomeRandom.Chance(random, mutationRate))
                {
                    part.thickness = Mathf.Clamp(part.thickness + GenomeRandom.Signed(random, 0.06f), 0.08f, 0.65f);
                }

                if (GenomeRandom.Chance(random, mutationRate))
                {
                    part.mass = Mathf.Clamp(part.mass + GenomeRandom.Signed(random, 0.25f), 0.12f, 2.8f);
                }

                if (i > 0 && GenomeRandom.Chance(random, mutationRate))
                {
                    part.localOffset += new Vector3(
                        GenomeRandom.Signed(random, 0.22f),
                        GenomeRandom.Signed(random, 0.22f),
                        GenomeRandom.Signed(random, 0.12f));
                }

                if (i > 0 && GenomeRandom.Chance(random, mutationRate))
                {
                    part.localEulerAngles += new Vector3(
                        GenomeRandom.Signed(random, 12f),
                        GenomeRandom.Signed(random, 16f),
                        GenomeRandom.Signed(random, 24f));
                }

                if (i > 0 && GenomeRandom.Chance(random, mutationRate))
                {
                    part.jointLimit = Mathf.Clamp(part.jointLimit + GenomeRandom.Signed(random, 18f), 20f, 170f);
                }

                if (i > 0 && GenomeRandom.Chance(random, mutationRate))
                {
                    part.driveStrength = Mathf.Clamp(part.driveStrength + GenomeRandom.Signed(random, 0.25f), 0.2f, 2.5f);
                }

                bodyParts[i] = part;
            }

            // Prototype-only topology mutations. Removing the last entry preserves the ordered-tree invariant.
            if (bodyParts.Count < MaxBodyParts && GenomeRandom.Chance(random, mutationRate * 0.35f))
            {
                int parentIndex = random.Next(0, bodyParts.Count);
                BodyPartGene parent = bodyParts[parentIndex];
                float length = GenomeRandom.Range(random, 0.35f, 1.15f);
                float pitch = GenomeRandom.Range(random, -70f, 70f);
                float yaw = GenomeRandom.Range(random, -40f, 40f);
                Quaternion direction = Quaternion.Euler(0f, yaw, pitch);
                bodyParts.Add(new BodyPartGene
                {
                    parentIndex = parentIndex,
                    localOffset = direction * (Vector3.right * GenomeRandom.Range(random, parent.length * 0.35f, parent.length * 0.85f)),
                    localEulerAngles = new Vector3(0f, yaw, pitch),
                    length = length,
                    thickness = GenomeRandom.Range(random, 0.1f, 0.3f),
                    mass = GenomeRandom.Range(random, 0.2f, 1.1f),
                    jointLimit = GenomeRandom.Range(random, 30f, 150f),
                    driveStrength = GenomeRandom.Range(random, 0.35f, 1.8f)
                });
            }
            else if (bodyParts.Count > MinBodyParts && GenomeRandom.Chance(random, mutationRate * 0.25f))
            {
                bodyParts.RemoveAt(bodyParts.Count - 1);
            }

            brain = brain ?? new BrainGene();
            brain.Mutate(random, mutationRate);
            Repair();
        }

        public void Repair()
        {
            schemaVersion = CurrentSchemaVersion;
            bodyParts = bodyParts ?? new List<BodyPartGene>();
            brain = brain ?? new BrainGene();
            brain.EnsureShape();

            if (bodyParts.Count == 0)
            {
                bodyParts.Add(BodyPartGene.CreateRoot(1f, 0.35f, 1f));
                bodyParts.Add(new BodyPartGene
                {
                    parentIndex = 0,
                    localOffset = Vector3.right * 0.65f,
                    localEulerAngles = Vector3.zero,
                    length = 0.75f,
                    thickness = 0.2f,
                    mass = 0.5f,
                    jointLimit = 90f,
                    driveStrength = 1f
                });
            }

            while (bodyParts.Count > MaxBodyParts)
            {
                bodyParts.RemoveAt(bodyParts.Count - 1);
            }

            for (int i = 0; i < bodyParts.Count; i++)
            {
                BodyPartGene part = bodyParts[i];
                part.parentIndex = i == 0 ? -1 : Mathf.Clamp(part.parentIndex, 0, i - 1);
                part.length = Mathf.Clamp(part.length, 0.3f, 1.8f);
                part.thickness = Mathf.Clamp(part.thickness, 0.08f, 0.65f);
                part.mass = Mathf.Clamp(part.mass, 0.12f, 2.8f);
                part.jointLimit = Mathf.Clamp(part.jointLimit, 20f, 170f);
                part.driveStrength = Mathf.Clamp(part.driveStrength, 0.2f, 2.5f);
                if (i == 0)
                {
                    part.localOffset = Vector3.zero;
                    part.localEulerAngles = Vector3.zero;
                }
                else if (part.localOffset.sqrMagnitude < 0.01f)
                {
                    part.localOffset = Vector3.right * 0.5f;
                }

                bodyParts[i] = part;
            }
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        private static void MixArrays(System.Random random, float[] first, float[] second)
        {
            int length = Mathf.Min(first == null ? 0 : first.Length, second == null ? 0 : second.Length);
            for (int i = 0; i < length; i++)
            {
                if (GenomeRandom.Chance(random, 0.5f))
                {
                    first[i] = second[i];
                }
            }
        }
    }
}
