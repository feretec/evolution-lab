using System;
using System.Globalization;

namespace EvolutionLab
{
    /// <summary>
    /// Deterministic, normalized descriptors used by post-hoc natural-history
    /// analysis. This is deliberately separate from CreatureGenome so the
    /// runtime genome and the analysis representation can evolve independently.
    /// </summary>
    public static class GenomeDistance
    {
        // Per-part features include the legacy body plan plus the full
        // ConfigurableJoint frame and angular limits introduced by schema 5.
        private const int MorphologyPartFeatureCount = 20;
        private const int SensorFeatureCount = 10; // all SensorGene fields
        private const int MouthFeatureCount = 9;   // all MouthGene fields
        private const int LearningFeatureCount = 11; // enabled + ten inherited plasticity parameters
        private const float MaxLength = 1.8f;
        private const float MaxThickness = 0.65f;
        private const float MaxMass = 2.8f;
        private const float MaxJointLimit = 170f;
        private const float MaxDriveStrength = 2.5f;
        private const float MaxOffset = 2f;
        private const float BrainValueScale = 3f;

        public const int MorphologyFeatureCount =
            1 + CreatureGenome.MaxBodyParts * MorphologyPartFeatureCount
            + 1 + CreatureGenome.MaxSensors * SensorFeatureCount
            + MouthFeatureCount;

        public const int BrainFeatureCount =
            BrainGene.InputCount * BrainGene.HiddenCount
            + BrainGene.HiddenCount
            + BrainGene.HiddenCount * BrainGene.MaxOutputCount
            + BrainGene.MaxOutputCount
            + 1 // activeHiddenCount
            + LearningFeatureCount;

        private const int BrainCoreFeatureCount =
            BrainGene.InputCount * BrainGene.HiddenCount
            + BrainGene.HiddenCount
            + BrainGene.HiddenCount * BrainGene.MaxOutputCount
            + BrainGene.MaxOutputCount;

        public const int EcologyFeatureCount = 8;

        public sealed class Descriptor
        {
            internal readonly float[] morphology;
            internal readonly float[] brain;
            internal readonly float[] ecology;

            public readonly bool HasGenome;
            public readonly bool HasMorphology;
            public readonly bool HasBrain;
            public readonly bool HasEcology;

            internal Descriptor(
                float[] morphology,
                float[] brain,
                float[] ecology,
                bool hasGenome,
                bool hasMorphology,
                bool hasBrain,
                bool hasEcology)
            {
                this.morphology = morphology;
                this.brain = brain;
                this.ecology = ecology;
                HasGenome = hasGenome;
                HasMorphology = hasMorphology;
                HasBrain = hasBrain;
                HasEcology = hasEcology;
            }
        }

        public static Descriptor Describe(CreatureGenome genome)
        {
            var morphology = new float[MorphologyFeatureCount];
            var brain = new float[BrainFeatureCount];
            var ecology = new float[EcologyFeatureCount];

            if (genome == null)
            {
                return new Descriptor(morphology, brain, ecology, false, false, false, false);
            }

            bool hasMorphology = genome.bodyParts != null && genome.bodyParts.Count > 0;
            if (hasMorphology)
            {
                int partCount = Math.Min(genome.bodyParts.Count, CreatureGenome.MaxBodyParts);
                morphology[0] = NormalizeUnit(partCount, CreatureGenome.MaxBodyParts);
                for (int i = 0; i < partCount; i++)
                {
                    BodyPartGene part = genome.bodyParts[i];
                    int offset = 1 + i * MorphologyPartFeatureCount;
                    morphology[offset] = NormalizeUnit(
                        part.parentIndex < 0 ? 0f : part.parentIndex,
                        Math.Max(1, CreatureGenome.MaxBodyParts - 1));
                    morphology[offset + 1] = NormalizeSigned(part.localOffset.x, MaxOffset);
                    morphology[offset + 2] = NormalizeSigned(part.localOffset.y, MaxOffset);
                    morphology[offset + 3] = NormalizeSigned(part.localOffset.z, MaxOffset);
                    morphology[offset + 4] = NormalizeSigned(part.localEulerAngles.x, 180f);
                    morphology[offset + 5] = NormalizeSigned(part.localEulerAngles.y, 180f);
                    morphology[offset + 6] = NormalizeSigned(part.localEulerAngles.z, 180f);
                    morphology[offset + 7] = NormalizeUnit(part.length, MaxLength);
                    morphology[offset + 8] = NormalizeUnit(part.thickness, MaxThickness);
                    morphology[offset + 9] = NormalizeUnit(part.mass, MaxMass);
                    morphology[offset + 10] = NormalizeUnit(part.jointLimit, MaxJointLimit);
                    morphology[offset + 11] = NormalizeUnit(part.driveStrength, MaxDriveStrength);
                    morphology[offset + 12] = NormalizeSigned(part.jointAxis.x, 1f);
                    morphology[offset + 13] = NormalizeSigned(part.jointAxis.y, 1f);
                    morphology[offset + 14] = NormalizeSigned(part.jointAxis.z, 1f);
                    morphology[offset + 15] = NormalizeSigned(part.secondaryAxis.x, 1f);
                    morphology[offset + 16] = NormalizeSigned(part.secondaryAxis.y, 1f);
                    morphology[offset + 17] = NormalizeSigned(part.secondaryAxis.z, 1f);
                    morphology[offset + 18] = NormalizeUnit(part.angularYLimit, MaxJointLimit);
                    morphology[offset + 19] = NormalizeUnit(part.angularZLimit, MaxJointLimit);
                }
            }

            int sensorOffset = 1 + CreatureGenome.MaxBodyParts * MorphologyPartFeatureCount;
            int sensorCount = genome.sensors == null ? 0 : Math.Min(genome.sensors.Count, CreatureGenome.MaxSensors);
            morphology[sensorOffset] = NormalizeUnit(sensorCount, CreatureGenome.MaxSensors);
            for (int i = 0; i < sensorCount; i++)
            {
                SensorGene sensor = genome.sensors[i];
                int offset = sensorOffset + 1 + i * SensorFeatureCount;
                morphology[offset] = NormalizeUnit(sensor.bodyPartIndex, Math.Max(1, CreatureGenome.MaxBodyParts - 1));
                morphology[offset + 1] = NormalizeSigned(sensor.localPosition.x, 2f);
                morphology[offset + 2] = NormalizeSigned(sensor.localPosition.y, 1f);
                morphology[offset + 3] = NormalizeSigned(sensor.localPosition.z, 1f);
                morphology[offset + 4] = NormalizeSigned(sensor.localDirection.x, 1f);
                morphology[offset + 5] = NormalizeSigned(sensor.localDirection.y, 1f);
                morphology[offset + 6] = NormalizeSigned(sensor.localDirection.z, 1f);
                morphology[offset + 7] = NormalizeRange(sensor.rangeMultiplier, 0.25f, 2f);
                morphology[offset + 8] = NormalizeRange(sensor.fieldOfView, 10f, 360f);
                morphology[offset + 9] = NormalizeRange(sensor.sensitivity, 0.05f, 3f);
            }

            int mouthOffset = sensorOffset + 1 + CreatureGenome.MaxSensors * SensorFeatureCount;
            morphology[mouthOffset] = NormalizeUnit(genome.mouth.bodyPartIndex, Math.Max(1, CreatureGenome.MaxBodyParts - 1));
            morphology[mouthOffset + 1] = NormalizeSigned(genome.mouth.localPosition.x, 2f);
            morphology[mouthOffset + 2] = NormalizeSigned(genome.mouth.localPosition.y, 1f);
            morphology[mouthOffset + 3] = NormalizeSigned(genome.mouth.localPosition.z, 1f);
            morphology[mouthOffset + 4] = NormalizeSigned(genome.mouth.localDirection.x, 1f);
            morphology[mouthOffset + 5] = NormalizeSigned(genome.mouth.localDirection.y, 1f);
            morphology[mouthOffset + 6] = NormalizeSigned(genome.mouth.localDirection.z, 1f);
            morphology[mouthOffset + 7] = NormalizeRange(genome.mouth.reach, 0.25f, 4f);
            morphology[mouthOffset + 8] = NormalizeRange(genome.mouth.efficiency, 0.05f, 2f);

            bool hasBrain = genome.brain != null
                && (genome.brain.inputHiddenWeights != null
                    || genome.brain.hiddenBiases != null
                    || genome.brain.hiddenOutputWeights != null
                    || genome.brain.outputBiases != null);
            if (hasBrain)
            {
                // Zero weights map to the middle of the normalized interval;
                // this also makes absent segments in an old partial BrainGene
                // neutral instead of falsely equivalent to a strong negative.
                for (int i = 0; i < brain.Length; i++) brain[i] = 0.5f;
                int cursor = 0;
                cursor = CopyNormalized(
                    genome.brain.inputHiddenWeights,
                    brain,
                    cursor,
                    BrainGene.InputCount * BrainGene.HiddenCount,
                    BrainValueScale);
                cursor = CopyNormalized(
                    genome.brain.hiddenBiases,
                    brain,
                    cursor,
                    BrainGene.HiddenCount,
                    BrainValueScale);
                cursor = CopyNormalized(
                    genome.brain.hiddenOutputWeights,
                    brain,
                    cursor,
                    BrainGene.HiddenCount * BrainGene.MaxOutputCount,
                    BrainValueScale);
                CopyNormalized(
                    genome.brain.outputBiases,
                    brain,
                    cursor,
                    BrainGene.MaxOutputCount,
                    BrainValueScale);
                int metadata = BrainCoreFeatureCount;
                int activeHiddenCount = genome.brain.activeHiddenCount <= 0
                    ? BrainGene.HiddenCount
                    : genome.brain.activeHiddenCount;
                brain[metadata++] = NormalizeRange(activeHiddenCount, 2f, BrainGene.HiddenCount);
                LifetimeLearningGene learning = genome.brain.learning;
                if (learning == null)
                {
                    learning = new LifetimeLearningGene();
                }
                brain[metadata++] = learning.enabled ? 1f : 0f;
                brain[metadata++] = NormalizeRange(learning.learningRate, 0.001f, 0.08f);
                brain[metadata++] = NormalizeRange(learning.eligibilityDecay, 0.55f, 0.995f);
                brain[metadata++] = NormalizeRange(learning.memoryRetention, 0.25f, 0.995f);
                brain[metadata++] = NormalizeRange(learning.fastWeightLimit, 0.1f, 1.5f);
                brain[metadata++] = NormalizeRange(learning.energyDeltaScale, 0.25f, 12f);
                brain[metadata++] = NormalizeRange(learning.damageScale, 0.25f, 12f);
                brain[metadata++] = NormalizeRange(learning.controlCostScale, 0f, 2f);
                brain[metadata++] = NormalizeRange(learning.survivalBias, 0f, 0.08f);
                brain[metadata++] = NormalizeRange(learning.rewardBaselineRate, 0.001f, 0.25f);
                brain[metadata] = NormalizeRange(learning.plasticityDecay, 0f, 0.01f);
            }

            bool hasEcology = genome.ecology != null;
            if (hasEcology)
            {
                ecology[0] = NormalizeUnit(genome.ecology.foragingDrive, 1f);
                ecology[1] = NormalizeUnit(genome.ecology.predationDrive, 1f);
                ecology[2] = NormalizeUnit(genome.ecology.defenseDrive, 1f);
                ecology[3] = NormalizeUnit(genome.ecology.socialDrive, 1f);
                ecology[4] = NormalizeRange(genome.ecology.sensorRange, 2f, 20f);
                ecology[5] = NormalizeUnit(genome.ecology.bodyProtection, 1f);
                ecology[6] = NormalizeRange(genome.ecology.energyEfficiency, 0.25f, 2f);
                ecology[7] = NormalizeUnit(genome.ecology.reproductionDrive, 1f);
            }

            return new Descriptor(morphology, brain, ecology, true, hasMorphology, hasBrain, hasEcology);
        }

        /// <summary>
        /// Returns a value in [0, 1]. Category means are normalized before the
        /// weighted combination so the large brain vector cannot drown out
        /// morphology or ecology.
        /// </summary>
        public static float Between(Descriptor first, Descriptor second)
        {
            if (first == null || second == null || !first.HasGenome || !second.HasGenome)
            {
                return 1f;
            }

            float total = 0f;
            float weight = 0f;
            AddCategory(first.HasMorphology && second.HasMorphology, first.morphology, second.morphology, 0.45f, ref total, ref weight);
            AddCategory(first.HasBrain && second.HasBrain, first.brain, second.brain, 0.35f, ref total, ref weight);
            AddCategory(first.HasEcology && second.HasEcology, first.ecology, second.ecology, 0.20f, ref total, ref weight);
            if (weight <= 0f)
            {
                return 1f;
            }

            return Clamp01((float)Math.Sqrt(Math.Max(0f, total / weight)));
        }

        /// <summary>
        /// Coarse deterministic bucket used only to find candidate cluster
        /// representatives. It is intentionally much cheaper than comparing
        /// the full descriptor and is not itself a species decision.
        /// </summary>
        public static string CandidateBucket(Descriptor descriptor)
        {
            if (descriptor == null || !descriptor.HasGenome)
            {
                return "missing";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "m{0}-{1}-{2}-b{3}-{4}-e{5}-{6}",
                descriptor.HasMorphology ? 1 : 0,
                Bucket(descriptor.morphology, 0, true),
                Bucket(descriptor.morphology, 1, false),
                descriptor.HasBrain ? 1 : 0,
                // Keep the candidate key stable when only activeHiddenCount or
                // inherited learning rules change. Those are compared by the
                // full descriptor, but must not make the coarse candidate set
                // churn between adjacent schema-6 genomes.
                Bucket(descriptor.brain, 0, BrainCoreFeatureCount),
                descriptor.HasEcology ? 1 : 0,
                Bucket(descriptor.ecology, 0, false));
        }

        /// <summary>
        /// Creates a stable content signature for records without a usable ID,
        /// and for deterministic tie-breaking of duplicate IDs.
        /// </summary>
        public static string StableSignature(CreatureGenome genome)
        {
            if (genome == null)
            {
                return "missing";
            }

            Descriptor descriptor = Describe(genome);
            ulong hash = 14695981039346656037UL;
            hash = AddInt(hash, descriptor.HasMorphology ? 1 : 0);
            hash = AddInt(hash, descriptor.HasBrain ? 1 : 0);
            hash = AddInt(hash, descriptor.HasEcology ? 1 : 0);
            hash = AddArray(hash, descriptor.morphology);
            hash = AddArray(hash, descriptor.brain);
            hash = AddArray(hash, descriptor.ecology);
            return "g-" + hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Allocation-free content revision used by the natural-history cache.
        /// It covers every field currently represented by Describe(), including
        /// partial/old arrays and sanitized non-finite values.
        /// </summary>
        public static ulong ContentHash(CreatureGenome genome)
        {
            if (genome == null)
            {
                return 0UL;
            }

            ulong hash = 14695981039346656037UL;
            hash = AddInt(hash, genome.schemaVersion);
            hash = AddInt(hash, genome.generation);
            hash = AddFloat(hash, genome.mutationRate);
            hash = AddInt(hash, genome.bodyParts == null ? -1 : genome.bodyParts.Count);
            if (genome.bodyParts != null)
            {
                for (int i = 0; i < genome.bodyParts.Count; i++)
                {
                    BodyPartGene part = genome.bodyParts[i];
                    hash = AddInt(hash, part.parentIndex);
                    hash = AddFloat(hash, part.localOffset.x);
                    hash = AddFloat(hash, part.localOffset.y);
                    hash = AddFloat(hash, part.localOffset.z);
                    hash = AddFloat(hash, part.localEulerAngles.x);
                    hash = AddFloat(hash, part.localEulerAngles.y);
                    hash = AddFloat(hash, part.localEulerAngles.z);
                    hash = AddFloat(hash, part.length);
                    hash = AddFloat(hash, part.thickness);
                    hash = AddFloat(hash, part.mass);
                    hash = AddFloat(hash, part.jointLimit);
                    hash = AddFloat(hash, part.driveStrength);
                    hash = AddFloat(hash, NormalizeSigned(part.jointAxis.x, 1f));
                    hash = AddFloat(hash, NormalizeSigned(part.jointAxis.y, 1f));
                    hash = AddFloat(hash, NormalizeSigned(part.jointAxis.z, 1f));
                    hash = AddFloat(hash, NormalizeSigned(part.secondaryAxis.x, 1f));
                    hash = AddFloat(hash, NormalizeSigned(part.secondaryAxis.y, 1f));
                    hash = AddFloat(hash, NormalizeSigned(part.secondaryAxis.z, 1f));
                    hash = AddFloat(hash, NormalizeUnit(part.angularYLimit, MaxJointLimit));
                    hash = AddFloat(hash, NormalizeUnit(part.angularZLimit, MaxJointLimit));
                }
            }

            hash = AddInt(hash, genome.sensors == null ? -1 : genome.sensors.Count);
            if (genome.sensors != null)
            {
                int sensorCount = Math.Min(genome.sensors.Count, CreatureGenome.MaxSensors);
                for (int i = 0; i < sensorCount; i++)
                {
                    SensorGene sensor = genome.sensors[i];
                    hash = AddFloat(hash, NormalizeUnit(sensor.bodyPartIndex, Math.Max(1, CreatureGenome.MaxBodyParts - 1)));
                    hash = AddFloat(hash, NormalizeSigned(sensor.localPosition.x, 2f));
                    hash = AddFloat(hash, NormalizeSigned(sensor.localPosition.y, 1f));
                    hash = AddFloat(hash, NormalizeSigned(sensor.localPosition.z, 1f));
                    hash = AddFloat(hash, NormalizeSigned(sensor.localDirection.x, 1f));
                    hash = AddFloat(hash, NormalizeSigned(sensor.localDirection.y, 1f));
                    hash = AddFloat(hash, NormalizeSigned(sensor.localDirection.z, 1f));
                    hash = AddFloat(hash, NormalizeRange(sensor.rangeMultiplier, 0.25f, 2f));
                    hash = AddFloat(hash, NormalizeRange(sensor.fieldOfView, 10f, 360f));
                    hash = AddFloat(hash, NormalizeRange(sensor.sensitivity, 0.05f, 3f));
                }
            }

            hash = AddFloat(hash, NormalizeUnit(genome.mouth.bodyPartIndex, Math.Max(1, CreatureGenome.MaxBodyParts - 1)));
            hash = AddFloat(hash, NormalizeSigned(genome.mouth.localPosition.x, 2f));
            hash = AddFloat(hash, NormalizeSigned(genome.mouth.localPosition.y, 1f));
            hash = AddFloat(hash, NormalizeSigned(genome.mouth.localPosition.z, 1f));
            hash = AddFloat(hash, NormalizeSigned(genome.mouth.localDirection.x, 1f));
            hash = AddFloat(hash, NormalizeSigned(genome.mouth.localDirection.y, 1f));
            hash = AddFloat(hash, NormalizeSigned(genome.mouth.localDirection.z, 1f));
            hash = AddFloat(hash, NormalizeRange(genome.mouth.reach, 0.25f, 4f));
            hash = AddFloat(hash, NormalizeRange(genome.mouth.efficiency, 0.05f, 2f));

            if (genome.brain == null)
            {
                hash = AddInt(hash, -1);
            }
            else
            {
                hash = AddRawArray(hash, genome.brain.inputHiddenWeights);
                hash = AddRawArray(hash, genome.brain.hiddenBiases);
                hash = AddRawArray(hash, genome.brain.hiddenOutputWeights);
                hash = AddRawArray(hash, genome.brain.outputBiases);
                hash = AddInt(hash, genome.brain.activeHiddenCount);
                LifetimeLearningGene learning = genome.brain.learning;
                if (learning == null)
                {
                    hash = AddInt(hash, -1);
                }
                else
                {
                    hash = AddInt(hash, learning.enabled ? 1 : 0);
                    hash = AddFloat(hash, NormalizeRange(learning.learningRate, 0.001f, 0.08f));
                    hash = AddFloat(hash, NormalizeRange(learning.eligibilityDecay, 0.55f, 0.995f));
                    hash = AddFloat(hash, NormalizeRange(learning.memoryRetention, 0.25f, 0.995f));
                    hash = AddFloat(hash, NormalizeRange(learning.fastWeightLimit, 0.1f, 1.5f));
                    hash = AddFloat(hash, NormalizeRange(learning.energyDeltaScale, 0.25f, 12f));
                    hash = AddFloat(hash, NormalizeRange(learning.damageScale, 0.25f, 12f));
                    hash = AddFloat(hash, NormalizeRange(learning.controlCostScale, 0f, 2f));
                    hash = AddFloat(hash, NormalizeRange(learning.survivalBias, 0f, 0.08f));
                    hash = AddFloat(hash, NormalizeRange(learning.rewardBaselineRate, 0.001f, 0.25f));
                    hash = AddFloat(hash, NormalizeRange(learning.plasticityDecay, 0f, 0.01f));
                }
            }

            if (genome.ecology == null)
            {
                hash = AddInt(hash, -1);
            }
            else
            {
                hash = AddFloat(hash, genome.ecology.foragingDrive);
                hash = AddFloat(hash, genome.ecology.predationDrive);
                hash = AddFloat(hash, genome.ecology.defenseDrive);
                hash = AddFloat(hash, genome.ecology.socialDrive);
                hash = AddFloat(hash, genome.ecology.sensorRange);
                hash = AddFloat(hash, genome.ecology.bodyProtection);
                hash = AddFloat(hash, genome.ecology.energyEfficiency);
                hash = AddFloat(hash, genome.ecology.reproductionDrive);
            }

            return hash;
        }

        private static void AddCategory(
            bool available,
            float[] first,
            float[] second,
            float categoryWeight,
            ref float total,
            ref float weight)
        {
            if (!available || first == null || second == null || first.Length == 0)
            {
                return;
            }

            int length = Math.Min(first.Length, second.Length);
            if (length == 0)
            {
                return;
            }

            float sum = 0f;
            for (int i = 0; i < length; i++)
            {
                float difference = Safe(first[i]) - Safe(second[i]);
                sum += difference * difference;
            }

            total += categoryWeight * (sum / length);
            weight += categoryWeight;
        }

        private static int Bucket(float[] values, int firstIndex, bool singleValue)
        {
            return Bucket(values, firstIndex, singleValue ? 1 : -1);
        }

        private static int Bucket(float[] values, int firstIndex, int featureCount)
        {
            if (values == null || values.Length == 0)
            {
                return 0;
            }

            float sum = 0f;
            int count = 0;
            if (featureCount == 1 && firstIndex >= 0 && firstIndex < values.Length)
            {
                // The first morphology feature is body-part count. Keep it
                // separate from the remaining shape values.
                sum = Safe(values[firstIndex]);
                count = 1;
            }
            else
            {
                int start = firstIndex < 0 ? 0 : firstIndex;
                int end = featureCount < 0 ? values.Length : Math.Min(values.Length, start + featureCount);
                for (int i = start; i < end; i++)
                {
                    sum += Safe(values[i]);
                    count++;
                }
            }

            float mean = count == 0 ? 0f : sum / count;
            int bucket = (int)Math.Floor(Clamp01(mean) * 4f);
            return bucket >= 4 ? 3 : bucket;
        }

        private static int CopyNormalized(
            float[] source,
            float[] target,
            int cursor,
            int segmentLength,
            float scale)
        {
            if (target == null || segmentLength <= 0)
            {
                return cursor;
            }

            int available = Math.Min(segmentLength, target.Length - cursor);
            int length = source == null ? 0 : Math.Min(source.Length, available);
            for (int i = 0; i < length; i++)
            {
                target[cursor + i] = NormalizeSigned(Safe(source[i]), scale);
            }

            return cursor + available;
        }

        private static float NormalizeUnit(float value, float maximum)
        {
            if (maximum <= 0f)
            {
                return 0f;
            }

            return Clamp01(Safe(value) / maximum);
        }

        private static float NormalizeRange(float value, float minimum, float maximum)
        {
            if (maximum <= minimum)
            {
                return 0f;
            }

            return Clamp01((Safe(value) - minimum) / (maximum - minimum));
        }

        private static float NormalizeSigned(float value, float maximum)
        {
            if (maximum <= 0f)
            {
                return 0.5f;
            }

            return (ClampSigned(Safe(value) / maximum) + 1f) * 0.5f;
        }

        private static float Safe(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }

        private static float ClampSigned(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            return value < -1f ? -1f : value > 1f ? 1f : value;
        }

        private static ulong AddArray(ulong hash, float[] values)
        {
            if (values == null)
            {
                return AddInt(hash, -1);
            }

            hash = AddInt(hash, values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                hash = AddInt(hash, Quantize(Safe(values[i])));
            }

            return hash;
        }

        private static ulong AddRawArray(ulong hash, float[] values)
        {
            if (values == null)
            {
                return AddInt(hash, -1);
            }

            hash = AddInt(hash, values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                hash = AddFloat(hash, values[i]);
            }
            return hash;
        }

        private static ulong AddFloat(ulong hash, float value)
        {
            return AddInt(hash, Quantize(Safe(value)));
        }

        private static int Quantize(float value)
        {
            double scaled = Math.Round(Safe(value) * 100000.0, MidpointRounding.AwayFromZero);
            if (scaled > int.MaxValue) return int.MaxValue;
            if (scaled < int.MinValue) return int.MinValue;
            return (int)scaled;
        }

        private static ulong AddInt(ulong hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 1099511628211UL;
                hash ^= (uint)(value >> 16);
                hash *= 1099511628211UL;
                return hash;
            }
        }
    }
}
