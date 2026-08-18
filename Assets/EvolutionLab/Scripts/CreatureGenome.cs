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
        // ConfigurableJoint's local frame is part of the inherited body plan.
        // Older genomes used forward/up and only the primary X angular axis.
        public Vector3 jointAxis;
        public Vector3 secondaryAxis;
        public float angularYLimit;
        public float angularZLimit;

        // Source-compatible aliases for the first draft of this schema. The
        // serialized fields above are the canonical names.
        public Vector3 primaryAxis
        {
            get { return jointAxis; }
            set { jointAxis = value; }
        }

        public float jointYLimit
        {
            get { return angularYLimit; }
            set { angularYLimit = value; }
        }

        public float jointZLimit
        {
            get { return angularZLimit; }
            set { angularZLimit = value; }
        }

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
                driveStrength = 1f,
                primaryAxis = Vector3.forward,
                secondaryAxis = Vector3.up,
                jointYLimit = 0f,
                jointZLimit = 0f
            };
        }
    }

    [Serializable]
    public struct SensorGene
    {
        public const int CurrentSchemaVersion = 1;

        public int bodyPartIndex;
        public Vector3 localPosition;
        public Vector3 localDirection;
        public float rangeMultiplier;
        public float fieldOfView;
        public float sensitivity;

        public static SensorGene CreateDefault()
        {
            return new SensorGene
            {
                bodyPartIndex = 0,
                localPosition = new Vector3(0.45f, 0f, 0f),
                localDirection = Vector3.right,
                rangeMultiplier = 1f,
                fieldOfView = 180f,
                sensitivity = 1f
            };
        }

        public static SensorGene CreateRandom(System.Random random, int bodyPartCount)
        {
            SensorGene sensor = new SensorGene
            {
                bodyPartIndex = bodyPartCount <= 0 ? 0 : random.Next(0, bodyPartCount),
                localPosition = new Vector3(
                    GenomeRandom.Signed(random, 0.45f),
                    GenomeRandom.Signed(random, 0.22f),
                    GenomeRandom.Signed(random, 0.22f)),
                localDirection = new Vector3(
                    GenomeRandom.Range(random, 0.25f, 1f),
                    GenomeRandom.Signed(random, 0.55f),
                    GenomeRandom.Signed(random, 0.55f)),
                rangeMultiplier = GenomeRandom.Range(random, 0.55f, 1.35f),
                fieldOfView = GenomeRandom.Range(random, 55f, 270f),
                sensitivity = GenomeRandom.Range(random, 0.65f, 1.35f)
            };
            sensor.Repair(bodyPartCount);
            return sensor;
        }

        public SensorGene Clone()
        {
            return this;
        }

        public static SensorGene Crossover(SensorGene first, SensorGene second, System.Random random)
        {
            SensorGene result = new SensorGene
            {
                bodyPartIndex = GenomeRandom.Chance(random, 0.5f) ? first.bodyPartIndex : second.bodyPartIndex,
                localPosition = Vector3.Lerp(first.localPosition, second.localPosition, 0.5f),
                localDirection = Vector3.Lerp(first.localDirection, second.localDirection, 0.5f),
                rangeMultiplier = Mathf.Lerp(first.rangeMultiplier, second.rangeMultiplier, 0.5f),
                fieldOfView = Mathf.Lerp(first.fieldOfView, second.fieldOfView, 0.5f),
                sensitivity = Mathf.Lerp(first.sensitivity, second.sensitivity, 0.5f)
            };
            return result;
        }

        public void Mutate(System.Random random, float mutationRate)
        {
            if (GenomeRandom.Chance(random, mutationRate))
            {
                localPosition += new Vector3(
                    GenomeRandom.Signed(random, 0.16f),
                    GenomeRandom.Signed(random, 0.12f),
                    GenomeRandom.Signed(random, 0.12f));
            }
            if (GenomeRandom.Chance(random, mutationRate))
            {
                localDirection = Quaternion.Euler(
                    GenomeRandom.Signed(random, 18f),
                    GenomeRandom.Signed(random, 24f),
                    GenomeRandom.Signed(random, 18f)) * localDirection;
            }
            if (GenomeRandom.Chance(random, mutationRate))
            {
                rangeMultiplier += GenomeRandom.Signed(random, 0.18f);
            }
            if (GenomeRandom.Chance(random, mutationRate))
            {
                fieldOfView += GenomeRandom.Signed(random, 24f);
            }
            if (GenomeRandom.Chance(random, mutationRate))
            {
                sensitivity += GenomeRandom.Signed(random, 0.16f);
            }
        }

        public void Repair(int bodyPartCount)
        {
            bodyPartIndex = bodyPartCount <= 0 ? 0 : Mathf.Clamp(bodyPartIndex, 0, bodyPartCount - 1);
            localPosition = SafeVector(localPosition, Vector3.zero);
            localPosition.x = Mathf.Clamp(localPosition.x, -2f, 2f);
            localPosition.y = Mathf.Clamp(localPosition.y, -1f, 1f);
            localPosition.z = Mathf.Clamp(localPosition.z, -1f, 1f);
            localDirection = SafeVector(localDirection, Vector3.right);
            if (localDirection.sqrMagnitude < 0.0001f)
            {
                localDirection = Vector3.right;
            }
            localDirection.Normalize();
            rangeMultiplier = Mathf.Clamp(Safe(rangeMultiplier, 1f), 0.25f, 2f);
            fieldOfView = Mathf.Clamp(Safe(fieldOfView, 180f), 10f, 360f);
            sensitivity = Mathf.Clamp(Safe(sensitivity, 1f), 0.05f, 3f);
        }

        private static Vector3 SafeVector(Vector3 value, Vector3 fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float Safe(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }
    }

    [Serializable]
    public struct MouthGene
    {
        public int bodyPartIndex;
        public Vector3 localPosition;
        public Vector3 localDirection;
        public float reach;
        public float efficiency;

        public static MouthGene CreateDefault()
        {
            return new MouthGene
            {
                bodyPartIndex = 0,
                localPosition = new Vector3(0.52f, 0f, 0f),
                localDirection = Vector3.right,
                reach = 1.4f,
                efficiency = 0.65f
            };
        }

        public static MouthGene CreateRandom(System.Random random, int bodyPartCount)
        {
            MouthGene organ = new MouthGene
            {
                bodyPartIndex = bodyPartCount <= 0 ? 0 : random.Next(0, bodyPartCount),
                localPosition = new Vector3(
                    GenomeRandom.Range(random, 0.25f, 0.7f),
                    GenomeRandom.Signed(random, 0.18f),
                    GenomeRandom.Signed(random, 0.18f)),
                localDirection = new Vector3(
                    GenomeRandom.Range(random, 0.35f, 1f),
                    GenomeRandom.Signed(random, 0.45f),
                    GenomeRandom.Signed(random, 0.45f)),
                reach = GenomeRandom.Range(random, 0.65f, 2.8f),
                efficiency = GenomeRandom.Range(random, 0.25f, 1.2f)
            };
            organ.Repair(bodyPartCount);
            return organ;
        }

        public MouthGene Clone()
        {
            return this;
        }

        public static MouthGene Crossover(
            MouthGene first,
            MouthGene second,
            System.Random random)
        {
            return new MouthGene
            {
                bodyPartIndex = GenomeRandom.Chance(random, 0.5f) ? first.bodyPartIndex : second.bodyPartIndex,
                localPosition = Vector3.Lerp(first.localPosition, second.localPosition, 0.5f),
                localDirection = Vector3.Lerp(first.localDirection, second.localDirection, 0.5f),
                reach = Mathf.Lerp(first.reach, second.reach, 0.5f),
                efficiency = Mathf.Lerp(first.efficiency, second.efficiency, 0.5f)
            };
        }

        public void Mutate(System.Random random, float mutationRate)
        {
            if (GenomeRandom.Chance(random, mutationRate))
            {
                localPosition += new Vector3(
                    GenomeRandom.Signed(random, 0.14f),
                    GenomeRandom.Signed(random, 0.1f),
                    GenomeRandom.Signed(random, 0.1f));
            }
            if (GenomeRandom.Chance(random, mutationRate))
            {
                localDirection = Quaternion.Euler(
                    GenomeRandom.Signed(random, 16f),
                    GenomeRandom.Signed(random, 22f),
                    GenomeRandom.Signed(random, 16f)) * localDirection;
            }
            if (GenomeRandom.Chance(random, mutationRate))
            {
                reach += GenomeRandom.Signed(random, 0.3f);
            }
            if (GenomeRandom.Chance(random, mutationRate))
            {
                efficiency += GenomeRandom.Signed(random, 0.14f);
            }
        }

        public void Repair(int bodyPartCount)
        {
            bodyPartIndex = bodyPartCount <= 0 ? 0 : Mathf.Clamp(bodyPartIndex, 0, bodyPartCount - 1);
            localPosition = IsFinite(localPosition) ? localPosition : Vector3.zero;
            localPosition.x = Mathf.Clamp(localPosition.x, -2f, 2f);
            localPosition.y = Mathf.Clamp(localPosition.y, -1f, 1f);
            localPosition.z = Mathf.Clamp(localPosition.z, -1f, 1f);
            localDirection = IsFinite(localDirection) ? localDirection : Vector3.right;
            if (localDirection.sqrMagnitude < 0.0001f)
            {
                localDirection = Vector3.right;
            }
            localDirection.Normalize();
            reach = Mathf.Clamp(Safe(reach, 1.4f), 0.25f, 4f);
            efficiency = Mathf.Clamp(Safe(efficiency, 0.65f), 0.05f, 2f);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float Safe(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }
    }

    /// <summary>
    /// Inherited parameters for lifetime neural plasticity.
    ///
    /// These values describe how an individual learns; they are not the
    /// individual's acquired memory. Runtime fast weights and traces live in
    /// Brain and are intentionally discarded when a creature dies or breeds
    /// (Baldwinian inheritance).
    /// </summary>
    [Serializable]
    public sealed class LifetimeLearningGene
    {
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        public bool enabled = true;
        public float learningRate = 0.018f;
        public float eligibilityDecay = 0.9f;
        public float memoryRetention = 0.82f;
        public float fastWeightLimit = 0.75f;
        public float energyDeltaScale = 5f;
        public float damageScale = 5f;
        public float controlCostScale = 0.35f;
        public float survivalBias = 0.012f;
        public float rewardBaselineRate = 0.035f;
        public float plasticityDecay = 0.0006f;

        public static LifetimeLearningGene CreateRandom(System.Random random)
        {
            var gene = new LifetimeLearningGene
            {
                learningRate = GenomeRandom.Range(random, 0.006f, 0.035f),
                eligibilityDecay = GenomeRandom.Range(random, 0.78f, 0.97f),
                memoryRetention = GenomeRandom.Range(random, 0.65f, 0.94f),
                fastWeightLimit = GenomeRandom.Range(random, 0.35f, 1.15f),
                energyDeltaScale = GenomeRandom.Range(random, 2.5f, 7.5f),
                damageScale = GenomeRandom.Range(random, 2.5f, 7.5f),
                controlCostScale = GenomeRandom.Range(random, 0.1f, 0.7f),
                survivalBias = GenomeRandom.Range(random, 0.002f, 0.025f),
                rewardBaselineRate = GenomeRandom.Range(random, 0.012f, 0.08f),
                plasticityDecay = GenomeRandom.Range(random, 0.00005f, 0.0025f)
            };
            gene.Repair();
            return gene;
        }

        public LifetimeLearningGene Clone()
        {
            Repair();
            return new LifetimeLearningGene
            {
                schemaVersion = schemaVersion,
                enabled = enabled,
                learningRate = learningRate,
                eligibilityDecay = eligibilityDecay,
                memoryRetention = memoryRetention,
                fastWeightLimit = fastWeightLimit,
                energyDeltaScale = energyDeltaScale,
                damageScale = damageScale,
                controlCostScale = controlCostScale,
                survivalBias = survivalBias,
                rewardBaselineRate = rewardBaselineRate,
                plasticityDecay = plasticityDecay
            };
        }

        public static LifetimeLearningGene Crossover(
            LifetimeLearningGene first,
            LifetimeLearningGene second,
            System.Random random)
        {
            LifetimeLearningGene a = first == null ? null : first.Clone();
            LifetimeLearningGene b = second == null ? null : second.Clone();
            LifetimeLearningGene result = a ?? b ?? new LifetimeLearningGene();
            if (a == null || b == null)
            {
                result.Repair();
                return result;
            }

            result.enabled = GenomeRandom.Chance(random, 0.5f) ? a.enabled : b.enabled;
            result.learningRate = Mathf.Lerp(a.learningRate, b.learningRate, 0.5f);
            result.eligibilityDecay = Mathf.Lerp(a.eligibilityDecay, b.eligibilityDecay, 0.5f);
            result.memoryRetention = Mathf.Lerp(a.memoryRetention, b.memoryRetention, 0.5f);
            result.fastWeightLimit = Mathf.Lerp(a.fastWeightLimit, b.fastWeightLimit, 0.5f);
            result.energyDeltaScale = Mathf.Lerp(a.energyDeltaScale, b.energyDeltaScale, 0.5f);
            result.damageScale = Mathf.Lerp(a.damageScale, b.damageScale, 0.5f);
            result.controlCostScale = Mathf.Lerp(a.controlCostScale, b.controlCostScale, 0.5f);
            result.survivalBias = Mathf.Lerp(a.survivalBias, b.survivalBias, 0.5f);
            result.rewardBaselineRate = Mathf.Lerp(a.rewardBaselineRate, b.rewardBaselineRate, 0.5f);
            result.plasticityDecay = Mathf.Lerp(a.plasticityDecay, b.plasticityDecay, 0.5f);
            result.Repair();
            return result;
        }

        public void Mutate(System.Random random, float mutationRate)
        {
            Repair();
            learningRate = MutateFloat(random, learningRate, mutationRate, 0.006f, 0.001f, 0.08f);
            eligibilityDecay = MutateFloat(random, eligibilityDecay, mutationRate, 0.045f, 0.55f, 0.995f);
            memoryRetention = MutateFloat(random, memoryRetention, mutationRate, 0.06f, 0.25f, 0.995f);
            fastWeightLimit = MutateFloat(random, fastWeightLimit, mutationRate, 0.12f, 0.1f, 1.5f);
            energyDeltaScale = MutateFloat(random, energyDeltaScale, mutationRate, 0.8f, 0.25f, 12f);
            damageScale = MutateFloat(random, damageScale, mutationRate, 0.8f, 0.25f, 12f);
            controlCostScale = MutateFloat(random, controlCostScale, mutationRate, 0.1f, 0f, 2f);
            survivalBias = MutateFloat(random, survivalBias, mutationRate, 0.004f, 0f, 0.08f);
            rewardBaselineRate = MutateFloat(random, rewardBaselineRate, mutationRate, 0.012f, 0.001f, 0.25f);
            plasticityDecay = MutateFloat(random, plasticityDecay, mutationRate, 0.0004f, 0f, 0.01f);
            if (GenomeRandom.Chance(random, mutationRate * 0.08f))
            {
                enabled = !enabled;
            }

            Repair();
        }

        public void Repair()
        {
            // A missing field in a pre-schema-4 archive is zero-filled by
            // JsonUtility. The nested schema marker lets us distinguish that
            // from a valid, intentionally disabled learning gene.
            if (schemaVersion <= 0)
            {
                schemaVersion = CurrentSchemaVersion;
                enabled = true;
                learningRate = 0.018f;
                eligibilityDecay = 0.9f;
                memoryRetention = 0.82f;
                fastWeightLimit = 0.75f;
                energyDeltaScale = 5f;
                damageScale = 5f;
                controlCostScale = 0.35f;
                survivalBias = 0.012f;
                rewardBaselineRate = 0.035f;
                plasticityDecay = 0.0006f;
            }

            if (schemaVersion < 2)
            {
                rewardBaselineRate = 0.035f;
                plasticityDecay = 0.0006f;
            }

            schemaVersion = CurrentSchemaVersion;
            learningRate = Mathf.Clamp(Safe(learningRate, 0.018f), 0.001f, 0.08f);
            eligibilityDecay = Mathf.Clamp(Safe(eligibilityDecay, 0.9f), 0.55f, 0.995f);
            memoryRetention = Mathf.Clamp(Safe(memoryRetention, 0.82f), 0.25f, 0.995f);
            fastWeightLimit = Mathf.Clamp(Safe(fastWeightLimit, 0.75f), 0.1f, 1.5f);
            energyDeltaScale = Mathf.Clamp(Safe(energyDeltaScale, 5f), 0.25f, 12f);
            damageScale = Mathf.Clamp(Safe(damageScale, 5f), 0.25f, 12f);
            controlCostScale = Mathf.Clamp(Safe(controlCostScale, 0.35f), 0f, 2f);
            survivalBias = Mathf.Clamp(Safe(survivalBias, 0.012f), 0f, 0.08f);
            rewardBaselineRate = Mathf.Clamp(Safe(rewardBaselineRate, 0.035f), 0.001f, 0.25f);
            plasticityDecay = Mathf.Clamp(Safe(plasticityDecay, 0.0006f), 0f, 0.01f);
        }

        private static float MutateFloat(
            System.Random random,
            float value,
            float mutationRate,
            float step,
            float min,
            float max)
        {
            if (GenomeRandom.Chance(random, mutationRate))
            {
                value += GenomeRandom.Signed(random, step);
            }

            return Mathf.Clamp(value, min, max);
        }

        private static float Safe(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }

    [Serializable]
    public sealed class BrainGene
    {
        // Final keeps the original locomotion observations at the front of the
        // vector and appends generic interaction/environment observations. This
        // lets older archives repair forward without changing their lineage IDs.
        public const int InputCount = 22;
        public const int HiddenCount = 8;
        public const int MaxOutputCount = 12;

        // Arrays retain a fixed maximum shape for fast evaluation and archive
        // compatibility, while this inherited value evolves the number of
        // neurons that actually participate in the controller.
        public int activeHiddenCount = HiddenCount;
        public float[] inputHiddenWeights;
        public float[] hiddenBiases;
        public float[] hiddenOutputWeights;
        public float[] outputBiases;
        // Inherited learning rules. Brain owns all acquired runtime state.
        public LifetimeLearningGene learning = new LifetimeLearningGene();

        public BrainGene()
        {
            EnsureShape();
        }

        public static BrainGene CreateRandom(System.Random random)
        {
            var gene = new BrainGene();
            gene.activeHiddenCount = random.Next(3, HiddenCount + 1);
            gene.learning = LifetimeLearningGene.CreateRandom(random);
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
                activeHiddenCount = activeHiddenCount,
                inputHiddenWeights = (float[])inputHiddenWeights.Clone(),
                hiddenBiases = (float[])hiddenBiases.Clone(),
                hiddenOutputWeights = (float[])hiddenOutputWeights.Clone(),
                outputBiases = (float[])outputBiases.Clone(),
                learning = learning == null
                    ? new LifetimeLearningGene()
                    : learning.Clone()
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
            if (GenomeRandom.Chance(random, mutationRate * 0.5f))
            {
                activeHiddenCount += random.Next(-1, 2);
            }
            learning = learning ?? new LifetimeLearningGene();
            learning.Mutate(random, mutationRate);
            activeHiddenCount = Mathf.Clamp(activeHiddenCount, 2, HiddenCount);
        }

        public void EnsureShape()
        {
            // Zero is the JsonUtility value for archives created before this
            // field existed; preserve their former eight-neuron behaviour.
            activeHiddenCount = activeHiddenCount <= 0
                ? HiddenCount
                : Mathf.Clamp(activeHiddenCount, 2, HiddenCount);
            inputHiddenWeights = Resize(inputHiddenWeights, InputCount * HiddenCount);
            hiddenBiases = Resize(hiddenBiases, HiddenCount);
            hiddenOutputWeights = Resize(hiddenOutputWeights, HiddenCount * MaxOutputCount);
            outputBiases = Resize(outputBiases, MaxOutputCount);
            learning = learning ?? new LifetimeLearningGene();
            learning.Repair();
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

    /// <summary>
    /// Continuous ecological traits. They are not species or roles: every
    /// individual can inherit and mutate any combination of these values, and
    /// the observed role is derived after the fact from behaviour and outcomes.
    /// </summary>
    [Serializable]
    public sealed class EcologyGene
    {
        public float foragingDrive;
        public float predationDrive;
        public float defenseDrive;
        public float socialDrive;
        public float sensorRange;
        public float bodyProtection;
        public float energyEfficiency;
        public float reproductionDrive;

        public static EcologyGene CreateRandom(System.Random random)
        {
            return new EcologyGene
            {
                foragingDrive = GenomeRandom.Range(random, 0.25f, 1f),
                predationDrive = GenomeRandom.Range(random, 0f, 1f),
                defenseDrive = GenomeRandom.Range(random, 0.15f, 1f),
                socialDrive = GenomeRandom.Range(random, 0f, 1f),
                sensorRange = GenomeRandom.Range(random, 3f, 14f),
                bodyProtection = GenomeRandom.Range(random, 0.05f, 1f),
                energyEfficiency = GenomeRandom.Range(random, 0.45f, 1.5f),
                reproductionDrive = GenomeRandom.Range(random, 0.25f, 1f)
            };
        }

        public EcologyGene Clone()
        {
            return new EcologyGene
            {
                foragingDrive = foragingDrive,
                predationDrive = predationDrive,
                defenseDrive = defenseDrive,
                socialDrive = socialDrive,
                sensorRange = sensorRange,
                bodyProtection = bodyProtection,
                energyEfficiency = energyEfficiency,
                reproductionDrive = reproductionDrive
            };
        }

        public void Mutate(System.Random random, float mutationRate)
        {
            foragingDrive = MutateFloat(random, foragingDrive, mutationRate, 0.16f, 0f, 1f);
            predationDrive = MutateFloat(random, predationDrive, mutationRate, 0.2f, 0f, 1f);
            defenseDrive = MutateFloat(random, defenseDrive, mutationRate, 0.16f, 0f, 1f);
            socialDrive = MutateFloat(random, socialDrive, mutationRate, 0.2f, 0f, 1f);
            sensorRange = MutateFloat(random, sensorRange, mutationRate, 1.2f, 2f, 20f);
            bodyProtection = MutateFloat(random, bodyProtection, mutationRate, 0.16f, 0f, 1f);
            energyEfficiency = MutateFloat(random, energyEfficiency, mutationRate, 0.16f, 0.25f, 2f);
            reproductionDrive = MutateFloat(random, reproductionDrive, mutationRate, 0.18f, 0f, 1f);
        }

        public void Repair()
        {
            // JsonUtility creates a zero-filled object when an older archive
            // has no ecology block. Restore sensible neutral defaults before
            // clamping so schema 1/2 genomes remain usable.
            if (sensorRange < 0.01f
                && energyEfficiency < 0.01f
                && foragingDrive < 0.01f
                && predationDrive < 0.01f
                && defenseDrive < 0.01f
                && socialDrive < 0.01f
                && bodyProtection < 0.01f
                && reproductionDrive < 0.01f)
            {
                foragingDrive = 0.6f;
                predationDrive = 0.25f;
                defenseDrive = 0.5f;
                socialDrive = 0.35f;
                sensorRange = 8f;
                bodyProtection = 0.4f;
                energyEfficiency = 1f;
                reproductionDrive = 0.6f;
            }

            foragingDrive = Mathf.Clamp01(Safe(foragingDrive, 0.6f));
            predationDrive = Mathf.Clamp01(Safe(predationDrive, 0.25f));
            defenseDrive = Mathf.Clamp01(Safe(defenseDrive, 0.5f));
            socialDrive = Mathf.Clamp01(Safe(socialDrive, 0.35f));
            sensorRange = Mathf.Clamp(Safe(sensorRange, 8f), 2f, 20f);
            bodyProtection = Mathf.Clamp01(Safe(bodyProtection, 0.4f));
            energyEfficiency = Mathf.Clamp(Safe(energyEfficiency, 1f), 0.25f, 2f);
            reproductionDrive = Mathf.Clamp01(Safe(reproductionDrive, 0.6f));
        }

        private static float MutateFloat(
            System.Random random,
            float value,
            float mutationRate,
            float step,
            float min,
            float max)
        {
            if (GenomeRandom.Chance(random, mutationRate))
            {
                value += GenomeRandom.Signed(random, step);
            }

            return Mathf.Clamp(value, min, max);
        }

        private static float Safe(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }

    [Serializable]
    public sealed class CreatureGenome
    {
        // Schema 5 introduced inherited sensor/organ topology and multi-axis
        // joints. Schema 6 adds an evolvable active hidden-neuron count while
        // retaining fixed-capacity arrays. Runtime fast weights remain outside
        // the inherited schema.
        public const int CurrentSchemaVersion = 6;
        public const int MinBodyParts = 2;
        public const int MaxBodyParts = 12;
        public const int MaxSensors = 3;

        public int schemaVersion = CurrentSchemaVersion;
        public string genomeId = string.Empty;
        public string parentId = string.Empty;
        public string secondaryParentId = string.Empty;
        public int generation;
        public float mutationRate = 0.16f;
        public List<BodyPartGene> bodyParts = new List<BodyPartGene>();
        public BrainGene brain = new BrainGene();
        public EcologyGene ecology = new EcologyGene();
        public List<SensorGene> sensors = new List<SensorGene>();
        public MouthGene mouth = MouthGene.CreateDefault();

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
                ecology = ecology == null ? new EcologyGene() : ecology.Clone(),
                bodyParts = new List<BodyPartGene>(),
                sensors = sensors == null ? new List<SensorGene>() : new List<SensorGene>(sensors),
                mouth = mouth.Clone()
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
                brain = BrainGene.CreateRandom(random),
                ecology = EcologyGene.CreateRandom(random),
                sensors = new List<SensorGene>(),
                mouth = MouthGene.CreateDefault()
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
                    driveStrength = GenomeRandom.Range(random, 0.45f, 1.6f),
                    primaryAxis = Vector3.forward,
                    secondaryAxis = Vector3.up,
                    jointYLimit = GenomeRandom.Chance(random, 0.42f)
                        ? GenomeRandom.Range(random, 18f, 100f)
                        : 0f,
                    jointZLimit = GenomeRandom.Chance(random, 0.32f)
                        ? GenomeRandom.Range(random, 18f, 100f)
                        : 0f
                });
            }

            int sensorCount = random.Next(1, 3);
            for (int i = 0; i < sensorCount; i++)
            {
                genome.sensors.Add(SensorGene.CreateRandom(random, genome.bodyParts.Count));
            }
            genome.mouth = MouthGene.CreateRandom(random, genome.bodyParts.Count);

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

            child.ecology = a.ecology == null
                ? EcologyGene.CreateRandom(random)
                : a.ecology.Clone();
            if (b != null && b.ecology != null)
            {
                child.ecology.foragingDrive = Mathf.Lerp(child.ecology.foragingDrive, b.ecology.foragingDrive, 0.5f);
                child.ecology.predationDrive = Mathf.Lerp(child.ecology.predationDrive, b.ecology.predationDrive, 0.5f);
                child.ecology.defenseDrive = Mathf.Lerp(child.ecology.defenseDrive, b.ecology.defenseDrive, 0.5f);
                child.ecology.socialDrive = Mathf.Lerp(child.ecology.socialDrive, b.ecology.socialDrive, 0.5f);
                child.ecology.sensorRange = Mathf.Lerp(child.ecology.sensorRange, b.ecology.sensorRange, 0.5f);
                child.ecology.bodyProtection = Mathf.Lerp(child.ecology.bodyProtection, b.ecology.bodyProtection, 0.5f);
                child.ecology.energyEfficiency = Mathf.Lerp(child.ecology.energyEfficiency, b.ecology.energyEfficiency, 0.5f);
                child.ecology.reproductionDrive = Mathf.Lerp(child.ecology.reproductionDrive, b.ecology.reproductionDrive, 0.5f);
            }

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
                    part.primaryAxis = Vector3.Lerp(part.primaryAxis, other.primaryAxis, 0.5f);
                    part.secondaryAxis = Vector3.Lerp(part.secondaryAxis, other.secondaryAxis, 0.5f);
                    part.jointYLimit = Mathf.Lerp(part.jointYLimit, other.jointYLimit, 0.5f);
                    part.jointZLimit = Mathf.Lerp(part.jointZLimit, other.jointZLimit, 0.5f);
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
                child.brain.learning = LifetimeLearningGene.Crossover(
                    child.brain.learning,
                    b.brain.learning,
                    random);
                child.brain.activeHiddenCount = GenomeRandom.Chance(random, 0.5f)
                    ? child.brain.activeHiddenCount
                    : b.brain.activeHiddenCount;
            }

            child.sensors = CrossoverSensors(child.sensors, b == null ? null : b.sensors, random);
            if (b != null)
            {
                child.mouth = MouthGene.Crossover(child.mouth, b.mouth, random);
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

                if (i > 0 && GenomeRandom.Chance(random, mutationRate))
                {
                    part.primaryAxis = Quaternion.Euler(
                        GenomeRandom.Signed(random, 18f),
                        GenomeRandom.Signed(random, 24f),
                        GenomeRandom.Signed(random, 18f)) * part.primaryAxis;
                }

                if (i > 0 && GenomeRandom.Chance(random, mutationRate))
                {
                    part.secondaryAxis = Quaternion.Euler(
                        GenomeRandom.Signed(random, 18f),
                        GenomeRandom.Signed(random, 24f),
                        GenomeRandom.Signed(random, 18f)) * part.secondaryAxis;
                }

                if (i > 0 && GenomeRandom.Chance(random, mutationRate))
                {
                    part.jointYLimit = Mathf.Clamp(part.jointYLimit + GenomeRandom.Signed(random, 16f), 0f, 170f);
                }

                if (i > 0 && GenomeRandom.Chance(random, mutationRate))
                {
                    part.jointZLimit = Mathf.Clamp(part.jointZLimit + GenomeRandom.Signed(random, 16f), 0f, 170f);
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
                    driveStrength = GenomeRandom.Range(random, 0.35f, 1.8f),
                    primaryAxis = Vector3.forward,
                    secondaryAxis = Vector3.up,
                    jointYLimit = GenomeRandom.Chance(random, 0.45f) ? GenomeRandom.Range(random, 15f, 95f) : 0f,
                    jointZLimit = GenomeRandom.Chance(random, 0.35f) ? GenomeRandom.Range(random, 15f, 95f) : 0f
                });
            }
            else if (bodyParts.Count > MinBodyParts && GenomeRandom.Chance(random, mutationRate * 0.25f))
            {
                bodyParts.RemoveAt(bodyParts.Count - 1);
            }

            brain = brain ?? new BrainGene();
            brain.Mutate(random, mutationRate);
            ecology = ecology ?? EcologyGene.CreateRandom(random);
            ecology.Mutate(random, mutationRate);

            for (int i = 0; i < sensors.Count; i++)
            {
                SensorGene sensor = sensors[i];
                sensor.Mutate(random, mutationRate);
                sensors[i] = sensor;
            }

            if (sensors.Count < MaxSensors && GenomeRandom.Chance(random, mutationRate * 0.3f))
            {
                sensors.Add(SensorGene.CreateRandom(random, bodyParts.Count));
            }
            else if (sensors.Count > 1 && GenomeRandom.Chance(random, mutationRate * 0.2f))
            {
                sensors.RemoveAt(sensors.Count - 1);
            }

            mouth.Mutate(random, mutationRate);
            Repair();
        }

        public void Repair()
        {
            int sourceSchema = schemaVersion;
            bool legacySingleAxis = sourceSchema < 5;
            schemaVersion = CurrentSchemaVersion;
            bodyParts = bodyParts ?? new List<BodyPartGene>();
            brain = brain ?? new BrainGene();
            brain.EnsureShape();
            ecology = ecology ?? new EcologyGene();
            ecology.Repair();
            sensors = sensors ?? new List<SensorGene>();
            // Mouth is a single generic interaction organ. It has no predator
            // or prey class; its continuous reach/efficiency shape outcomes.
            if (sourceSchema < 5)
            {
                // Schema 4 and older had an implicit root-centred interaction
                // profile. Preserve that capability instead of accepting the
                // zero-filled struct as an extremely short, inefficient mouth.
                mouth = MouthGene.CreateDefault();
            }

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
                    driveStrength = 1f,
                    primaryAxis = Vector3.forward,
                    secondaryAxis = Vector3.up,
                    jointYLimit = 0f,
                    jointZLimit = 0f
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
                part.primaryAxis = SafeVector(part.primaryAxis, Vector3.forward);
                if (part.primaryAxis.sqrMagnitude < 0.0001f)
                {
                    part.primaryAxis = Vector3.forward;
                }
                part.primaryAxis.Normalize();
                part.secondaryAxis = SafeVector(part.secondaryAxis, Vector3.up);
                part.secondaryAxis = Vector3.ProjectOnPlane(part.secondaryAxis, part.primaryAxis);
                if (part.secondaryAxis.sqrMagnitude < 0.0001f)
                {
                    part.secondaryAxis = Vector3.Cross(part.primaryAxis, Vector3.right);
                    if (part.secondaryAxis.sqrMagnitude < 0.0001f)
                    {
                        part.secondaryAxis = Vector3.Cross(part.primaryAxis, Vector3.up);
                    }
                }
                part.secondaryAxis.Normalize();
                part.jointYLimit = legacySingleAxis
                    ? 0f
                    : Mathf.Clamp(Safe(part.jointYLimit, 0f), 0f, 170f);
                part.jointZLimit = legacySingleAxis
                    ? 0f
                    : Mathf.Clamp(Safe(part.jointZLimit, 0f), 0f, 170f);
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

            while (sensors.Count > MaxSensors)
            {
                sensors.RemoveAt(sensors.Count - 1);
            }
            if (sensors.Count == 0)
            {
                sensors.Add(SensorGene.CreateDefault());
            }
            for (int i = 0; i < sensors.Count; i++)
            {
                SensorGene sensor = sensors[i];
                sensor.Repair(bodyParts.Count);
                sensors[i] = sensor;
            }

            mouth.Repair(bodyParts.Count);
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

        private static List<SensorGene> CrossoverSensors(
            List<SensorGene> first,
            List<SensorGene> second,
            System.Random random)
        {
            var result = first == null ? new List<SensorGene>() : new List<SensorGene>(first);
            if (second != null)
            {
                int shared = Mathf.Min(result.Count, second.Count);
                for (int i = 0; i < shared; i++)
                {
                    if (GenomeRandom.Chance(random, 0.5f))
                    {
                        result[i] = SensorGene.Crossover(result[i], second[i], random);
                    }
                }

                if (result.Count < MaxSensors && second.Count > shared && GenomeRandom.Chance(random, 0.5f))
                {
                    result.Add(second[shared]);
                }
            }
            return result;
        }

        private static Vector3 SafeVector(Vector3 value, Vector3 fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float Safe(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }
    }
}
