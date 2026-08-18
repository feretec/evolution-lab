using System;
using System.Collections.Generic;
using System.Globalization;

namespace EvolutionLab
{
    [Serializable]
    public sealed class LineageSummary
    {
        public string lineageId = string.Empty;
        public string founderId = string.Empty;
        public string representativeGenomeId = string.Empty;
        public int earliestGeneration;
        public int latestGeneration;
        public int memberCount;
        public int livingCount;
        public bool extinct;
        public float maxFitness;
        public int representativeBodyPartCount;
        public int representativeJointCount;
        public string speciesKey = string.Empty;
    }

    [Serializable]
    public sealed class SpeciesSummary
    {
        public string speciesKey = string.Empty;
        public string representativeGenomeId = string.Empty;
        public int memberCount;
        public int livingCount;
        public bool extinct;
        public int generationFirstSeen;
        public int generationLastSeen;
        public float averageBodyPartCount;
        public float averageFitness;
    }

    /// <summary>
    /// Read-only-style aggregate produced from SimulationHistory records.
    /// The lists are intentionally exposed as IReadOnlyList through the properties.
    /// </summary>
    public sealed class NaturalHistoryCatalog
    {
        public readonly List<LineageSummary> lineages = new List<LineageSummary>();
        public readonly List<SpeciesSummary> species = new List<SpeciesSummary>();

        public IReadOnlyList<LineageSummary> Lineages { get { return lineages; } }
        public IReadOnlyList<SpeciesSummary> Species { get { return species; } }

        public static NaturalHistoryCatalog Build(IReadOnlyList<IndividualHistoryRecord> records)
        {
            var catalog = new NaturalHistoryCatalog();
            if (records == null || records.Count == 0)
            {
                return catalog;
            }

            var byId = new Dictionary<string, IndividualHistoryRecord>(StringComparer.Ordinal);
            for (int i = 0; i < records.Count; i++)
            {
                IndividualHistoryRecord record = records[i];
                if (record != null && !string.IsNullOrEmpty(record.genomeId))
                {
                    byId[record.genomeId] = record;
                }
            }

            var lineageByRoot = new Dictionary<string, LineageAccumulator>(StringComparer.Ordinal);
            var speciesByKey = new Dictionary<string, SpeciesAccumulator>(StringComparer.Ordinal);
            for (int i = 0; i < records.Count; i++)
            {
                IndividualHistoryRecord record = records[i];
                if (record == null)
                {
                    continue;
                }

                string rootId = FindRootId(record, byId);
                if (string.IsNullOrEmpty(rootId))
                {
                    rootId = "record-" + i.ToString(CultureInfo.InvariantCulture);
                }

                LineageAccumulator lineage;
                if (!lineageByRoot.TryGetValue(rootId, out lineage))
                {
                    lineage = new LineageAccumulator(rootId);
                    lineageByRoot.Add(rootId, lineage);
                }
                lineage.Add(record);

                string key = CreateSpeciesKey(record);
                SpeciesAccumulator speciesAccumulator;
                if (!speciesByKey.TryGetValue(key, out speciesAccumulator))
                {
                    speciesAccumulator = new SpeciesAccumulator(key);
                    speciesByKey.Add(key, speciesAccumulator);
                }
                speciesAccumulator.Add(record);
            }

            foreach (LineageAccumulator lineage in lineageByRoot.Values)
            {
                catalog.lineages.Add(lineage.ToSummary());
            }
            foreach (SpeciesAccumulator speciesAccumulator in speciesByKey.Values)
            {
                catalog.species.Add(speciesAccumulator.ToSummary());
            }

            catalog.lineages.Sort((a, b) => string.CompareOrdinal(a.lineageId, b.lineageId));
            catalog.species.Sort((a, b) => string.CompareOrdinal(a.speciesKey, b.speciesKey));
            return catalog;
        }

        private static string FindRootId(
            IndividualHistoryRecord start,
            Dictionary<string, IndividualHistoryRecord> byId)
        {
            string currentId = start.genomeId ?? string.Empty;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrEmpty(currentId) && visited.Add(currentId))
            {
                IndividualHistoryRecord current;
                if (!byId.TryGetValue(currentId, out current) || current == null
                    || string.IsNullOrEmpty(current.parentId)
                    || !byId.ContainsKey(current.parentId))
                {
                    return currentId;
                }
                currentId = current.parentId;
            }

            return string.IsNullOrEmpty(currentId) ? (start.genomeId ?? string.Empty) : currentId;
        }

        private static string CreateSpeciesKey(IndividualHistoryRecord record)
        {
            int parts = Math.Max(0, record.bodyPartCount);
            int joints = Math.Max(0, record.jointCount);
            int lengthBand = 0;
            int thicknessBand = 0;
            int brainBand = 0;
            int ecologyBand = 0;
            if (record.genome != null)
            {
                if (record.genome.bodyParts != null)
                {
                    float totalLength = 0f;
                    float totalThickness = 0f;
                    for (int i = 0; i < record.genome.bodyParts.Count; i++)
                    {
                        totalLength += record.genome.bodyParts[i].length;
                        totalThickness += record.genome.bodyParts[i].thickness;
                    }
                    float divisor = Math.Max(1, record.genome.bodyParts.Count);
                    lengthBand = Quantize(totalLength / divisor, 0.25f);
                    thicknessBand = Quantize(totalThickness / divisor, 0.05f);
                }
                brainBand = Quantize(BrainMagnitude(record.genome), 0.5f);
                if (record.genome.ecology != null)
                {
                    ecologyBand = Quantize(
                        record.genome.ecology.predationDrive
                        + record.genome.ecology.defenseDrive
                        + record.genome.ecology.foragingDrive,
                        0.35f);
                }
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "m{0}-j{1}-l{2}-t{3}-b{4}-e{5}",
                parts, joints, lengthBand, thicknessBand, brainBand, ecologyBand);
        }

        private static int Quantize(float value, float width)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || width <= 0f)
            {
                return 0;
            }
            return (int)Math.Floor(value / width + 0.5f);
        }

        private static float BrainMagnitude(CreatureGenome genome)
        {
            if (genome == null || genome.brain == null)
            {
                return 0f;
            }
            float total = 0f;
            total += SumAbsolute(genome.brain.inputHiddenWeights);
            total += SumAbsolute(genome.brain.hiddenBiases);
            total += SumAbsolute(genome.brain.hiddenOutputWeights);
            total += SumAbsolute(genome.brain.outputBiases);
            return total;
        }

        private static float SumAbsolute(float[] values)
        {
            float total = 0f;
            if (values == null) return total;
            for (int i = 0; i < values.Length; i++)
            {
                if (!float.IsNaN(values[i]) && !float.IsInfinity(values[i])) total += Math.Abs(values[i]);
            }
            return total;
        }

        private sealed class LineageAccumulator
        {
            private readonly string rootId;
            private string bestId = string.Empty;
            private string bestSpeciesKey = string.Empty;
            private int bestParts;
            private int bestJoints;
            private float maxFitness = float.NegativeInfinity;
            private int earliest = int.MaxValue;
            private int latest = int.MinValue;
            private int members;
            private int living;

            public LineageAccumulator(string rootId) { this.rootId = rootId; }

            public void Add(IndividualHistoryRecord record)
            {
                members++;
                if (record.wasAlive) living++;
                earliest = Math.Min(earliest, record.generation);
                latest = Math.Max(latest, record.generation);
                if (string.IsNullOrEmpty(bestId))
                {
                    bestId = record.genomeId ?? string.Empty;
                    bestParts = Math.Max(0, record.bodyPartCount);
                    bestJoints = Math.Max(0, record.jointCount);
                    bestSpeciesKey = CreateSpeciesKey(record);
                }
                if (record.hasFitness && !float.IsNaN(record.fitness) && !float.IsInfinity(record.fitness)
                    && record.fitness > maxFitness)
                {
                    maxFitness = record.fitness;
                    bestId = record.genomeId ?? string.Empty;
                    bestParts = Math.Max(0, record.bodyPartCount);
                    bestJoints = Math.Max(0, record.jointCount);
                    bestSpeciesKey = CreateSpeciesKey(record);
                }
            }

            public LineageSummary ToSummary()
            {
                return new LineageSummary
                {
                    lineageId = rootId,
                    founderId = rootId,
                    representativeGenomeId = bestId,
                    earliestGeneration = earliest == int.MaxValue ? 0 : earliest,
                    latestGeneration = latest == int.MinValue ? 0 : latest,
                    memberCount = members,
                    livingCount = living,
                    extinct = members > 0 && living == 0,
                    maxFitness = maxFitness == float.NegativeInfinity ? 0f : maxFitness,
                    representativeBodyPartCount = bestParts,
                    representativeJointCount = bestJoints,
                    speciesKey = bestSpeciesKey
                };
            }
        }

        private sealed class SpeciesAccumulator
        {
            private readonly string key;
            private string representativeId = string.Empty;
            private float representativeFitness = float.NegativeInfinity;
            private int members;
            private int living;
            private int first = int.MaxValue;
            private int last = int.MinValue;
            private float parts;
            private float fitness;
            private int fitnessCount;

            public SpeciesAccumulator(string key) { this.key = key; }

            public void Add(IndividualHistoryRecord record)
            {
                members++;
                if (record.wasAlive) living++;
                first = Math.Min(first, record.generation);
                last = Math.Max(last, record.generation);
                parts += Math.Max(0, record.bodyPartCount);
                if (record.hasFitness && !float.IsNaN(record.fitness) && !float.IsInfinity(record.fitness))
                {
                    fitness += record.fitness;
                    fitnessCount++;
                    if (string.IsNullOrEmpty(representativeId) || record.fitness > representativeFitness)
                    {
                        representativeFitness = record.fitness;
                        representativeId = record.genomeId ?? string.Empty;
                    }
                }
                else if (string.IsNullOrEmpty(representativeId))
                {
                    representativeId = record.genomeId ?? string.Empty;
                }
            }

            public SpeciesSummary ToSummary()
            {
                return new SpeciesSummary
                {
                    speciesKey = key,
                    representativeGenomeId = representativeId,
                    memberCount = members,
                    livingCount = living,
                    extinct = members > 0 && living == 0,
                    generationFirstSeen = first == int.MaxValue ? 0 : first,
                    generationLastSeen = last == int.MinValue ? 0 : last,
                    averageBodyPartCount = members == 0 ? 0f : parts / members,
                    averageFitness = fitnessCount == 0 ? 0f : fitness / fitnessCount
                };
            }
        }
    }
}
