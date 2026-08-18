using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

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
        public LearningTelemetrySummary learning = new LearningTelemetrySummary();
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
        public LearningTelemetrySummary learning = new LearningTelemetrySummary();
    }

    /// <summary>
    /// Read-only-style aggregate produced from SimulationHistory records.
    /// Species are inferred after the fact from a deterministic distance graph;
    /// no predator/herbivore classes or role labels are used.
    /// </summary>
    public sealed class NaturalHistoryCatalog
    {
        // Tuned as a first post-hoc species boundary for the current genome
        // ranges. Keep this as the replacement seam for later validation or a
        // population-aware clustering policy.
        public const float SpeciesDistanceThreshold = 0.34f;
        public const int MaxClusterRepresentativeComparisons = 64;

        // EvolutionSimulation currently asks History.NaturalHistory from more
        // than one UI property per frame. Keep one source/revision cache here
        // until the owning SimulationHistory can expose an explicit revision.
        // The source reference check prevents one world from contaminating
        // another; a changed record/genome reference invalidates the catalog.
        private static IReadOnlyList<IndividualHistoryRecord> cachedSource;
        private static ulong cachedSourceRevision;
        private static int cachedHistoryRevision;
        private static bool cachedWithHistoryRevision;
        private static NaturalHistoryCatalog cachedCatalog;

        public readonly List<LineageSummary> lineages = new List<LineageSummary>();
        public readonly List<SpeciesSummary> species = new List<SpeciesSummary>();

        public IReadOnlyList<LineageSummary> Lineages { get { return lineages; } }
        public IReadOnlyList<SpeciesSummary> Species { get { return species; } }

        public static NaturalHistoryCatalog Build(IReadOnlyList<IndividualHistoryRecord> records)
        {
            ulong sourceRevision = ComputeSourceRevision(records);
            if (ReferenceEquals(cachedSource, records)
                && cachedCatalog != null
                && cachedSourceRevision == sourceRevision)
            {
                return cachedCatalog;
            }

            NaturalHistoryCatalog catalog = BuildUncached(records);
            cachedSource = records;
            cachedSourceRevision = sourceRevision;
            cachedWithHistoryRevision = false;
            cachedCatalog = catalog;
            return catalog;
        }

        /// <summary>
        /// O(1) cache-hit path for SimulationHistory. The caller owns the
        /// revision and increments it only when an individual record changes.
        /// </summary>
        public static NaturalHistoryCatalog Build(IReadOnlyList<IndividualHistoryRecord> records, int historyRevision)
        {
            if (ReferenceEquals(cachedSource, records)
                && cachedCatalog != null
                && cachedWithHistoryRevision
                && cachedHistoryRevision == historyRevision)
            {
                return cachedCatalog;
            }

            NaturalHistoryCatalog catalog = BuildUncached(records);
            cachedSource = records;
            cachedHistoryRevision = historyRevision;
            cachedWithHistoryRevision = true;
            cachedCatalog = catalog;
            return catalog;
        }

        private static NaturalHistoryCatalog BuildUncached(IReadOnlyList<IndividualHistoryRecord> records)
        {
            var catalog = new NaturalHistoryCatalog();
            if (records == null || records.Count == 0)
            {
                return catalog;
            }

            // Sort before populating any dictionary. All tie breaks and
            // representative choices are therefore independent of dictionary
            // enumeration order.
            var entries = new List<RecordEntry>();
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i] != null)
                {
                    entries.Add(new RecordEntry(records[i]));
                }
            }
            entries.Sort(RecordEntry.Compare);
            AssignMissingRecordFallbackIds(entries);

            var byId = new Dictionary<string, IndividualHistoryRecord>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                IndividualHistoryRecord record = entries[i].record;
                if (!string.IsNullOrEmpty(record.genomeId) && !byId.ContainsKey(record.genomeId))
                {
                    byId.Add(record.genomeId, record);
                }
            }

            string[] speciesKeys = Cluster(entries);
            var lineageByRoot = new Dictionary<string, LineageAccumulator>(StringComparer.Ordinal);
            var lineagesInCreationOrder = new List<LineageAccumulator>();
            var speciesByKey = new Dictionary<string, SpeciesAccumulator>(StringComparer.Ordinal);
            var speciesInCreationOrder = new List<SpeciesAccumulator>();

            for (int i = 0; i < entries.Count; i++)
            {
                RecordEntry entry = entries[i];
                IndividualHistoryRecord record = entry.record;
                string rootId = FindRootId(record, byId);
                if (string.IsNullOrEmpty(rootId))
                {
                    rootId = entry.fallbackId;
                }

                LineageAccumulator lineage;
                if (!lineageByRoot.TryGetValue(rootId, out lineage))
                {
                    lineage = new LineageAccumulator(rootId);
                    lineageByRoot.Add(rootId, lineage);
                    lineagesInCreationOrder.Add(lineage);
                }
                lineage.Add(record, speciesKeys[i], entry.stableKey);

                string speciesKey = speciesKeys[i];
                SpeciesAccumulator speciesAccumulator;
                if (!speciesByKey.TryGetValue(speciesKey, out speciesAccumulator))
                {
                    speciesAccumulator = new SpeciesAccumulator(speciesKey);
                    speciesByKey.Add(speciesKey, speciesAccumulator);
                    speciesInCreationOrder.Add(speciesAccumulator);
                }
                speciesAccumulator.Add(record, entry.stableKey);
            }

            // Never expose dictionary order through the public lists.
            lineagesInCreationOrder.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            speciesInCreationOrder.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            for (int i = 0; i < lineagesInCreationOrder.Count; i++)
            {
                catalog.lineages.Add(lineagesInCreationOrder[i].ToSummary());
            }
            for (int i = 0; i < speciesInCreationOrder.Count; i++)
            {
                catalog.species.Add(speciesInCreationOrder[i].ToSummary());
            }

            return catalog;
        }

        private static ulong ComputeSourceRevision(IReadOnlyList<IndividualHistoryRecord> records)
        {
            ulong hash = 14695981039346656037UL;
            if (records == null)
            {
                return AddHashInt(hash, -1);
            }

            hash = AddHashInt(hash, records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                IndividualHistoryRecord record = records[i];
                if (record == null)
                {
                    hash = AddHashInt(hash, 0);
                    continue;
                }

                hash = AddHashInt(hash, RuntimeHelpers.GetHashCode(record));
                hash = AddHashInt(hash, record.genome == null
                    ? 0
                    : RuntimeHelpers.GetHashCode(record.genome));
                ulong genomeContentHash = GenomeDistance.ContentHash(record.genome);
                hash = AddHashInt(hash, unchecked((int)genomeContentHash));
                hash = AddHashInt(hash, unchecked((int)(genomeContentHash >> 32)));
                hash = AddHashInt(hash, record.generation);
                hash = AddHashInt(hash, record.wasAlive ? 1 : 0);
                hash = AddHashInt(hash, record.hasFitness ? 1 : 0);
                hash = AddHashInt(hash, record.fitness.GetHashCode());
                hash = AddHashInt(hash, record.bodyPartCount);
                hash = AddHashInt(hash, record.jointCount);
            }

            return hash;
        }

        private static string FindRootId(
            IndividualHistoryRecord start,
            Dictionary<string, IndividualHistoryRecord> byId)
        {
            if (start == null)
            {
                return string.Empty;
            }

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

        private static string[] Cluster(List<RecordEntry> entries)
        {
            int count = entries.Count;
            var assignments = new string[count];
            if (count == 0)
            {
                return assignments;
            }

            // Descriptors are precomputed and reused. Representatives are
            // immutable cluster seeds, so a member can never chain two
            // otherwise distant clusters together. Candidate buckets plus a
            // hard comparison cap keep this bounded at roughly
            // N * MaxClusterRepresentativeComparisons distance calls, rather
            // than N^2. This is the replacement seam for a spatial index or
            // batch clustering pass if the history cap grows substantially.
            var buckets = new Dictionary<string, List<ClusterGroup>>(StringComparer.Ordinal);
            var groups = new List<ClusterGroup>();
            for (int i = 0; i < count; i++)
            {
                if (!entries[i].descriptor.HasGenome)
                {
                    ClusterGroup missingGroup = CreateGroup(i, entries);
                    groups.Add(missingGroup);
                    continue;
                }

                string bucketKey = GenomeDistance.CandidateBucket(entries[i].descriptor);
                List<ClusterGroup> candidates;
                if (!buckets.TryGetValue(bucketKey, out candidates))
                {
                    candidates = new List<ClusterGroup>();
                    buckets.Add(bucketKey, candidates);
                }

                ClusterGroup bestGroup = null;
                float bestDistance = float.PositiveInfinity;
                int candidateCount = candidates.Count;
                int comparisonCount = Math.Min(
                    candidateCount,
                    MaxClusterRepresentativeComparisons);
                for (int candidateIndex = 0; candidateIndex < comparisonCount; candidateIndex++)
                {
                    // Evenly sample a crowded bucket. The candidate list is
                    // created from stable-sorted records, so this remains
                    // deterministic while limiting worst-case work.
                    int selectedIndex = candidateCount <= comparisonCount
                        ? candidateIndex
                        : (candidateIndex * candidateCount) / comparisonCount;
                    ClusterGroup candidate = candidates[selectedIndex];
                    float distance = GenomeDistance.Between(
                        entries[i].descriptor,
                        candidate.representativeDescriptor);
                    if (distance < bestDistance
                        || (distance == bestDistance
                            && string.CompareOrdinal(
                                candidate.canonicalKey,
                                bestGroup == null ? string.Empty : bestGroup.canonicalKey) < 0))
                    {
                        bestDistance = distance;
                        bestGroup = candidate;
                    }
                }

                if (bestGroup == null || bestDistance > SpeciesDistanceThreshold)
                {
                    bestGroup = CreateGroup(i, entries);
                    candidates.Add(bestGroup);
                    groups.Add(bestGroup);
                }
                else
                {
                    bestGroup.memberIndexes.Add(i);
                }
            }

            for (int i = 0; i < groups.Count; i++)
            {
                groups[i].memberIndexes.Sort((a, b) => string.CompareOrdinal(
                    entries[a].stableKey,
                    entries[b].stableKey));
                groups[i].canonicalKey = entries[groups[i].memberIndexes[0]].stableKey;
            }
            groups.Sort((a, b) => string.CompareOrdinal(a.canonicalKey, b.canonicalKey));

            var usedKeys = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < groups.Count; i++)
            {
                ClusterGroup group = groups[i];
                string key = CreateClusterKey(group, entries);
                int occurrence;
                if (usedKeys.TryGetValue(key, out occurrence))
                {
                    occurrence++;
                    usedKeys[key] = occurrence;
                    key += "-" + occurrence.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    usedKeys.Add(key, 1);
                }

                for (int j = 0; j < group.memberIndexes.Count; j++)
                {
                    assignments[group.memberIndexes[j]] = key;
                }
            }

            return assignments;
        }

        private static ClusterGroup CreateGroup(int entryIndex, List<RecordEntry> entries)
        {
            var group = new ClusterGroup
            {
                representativeDescriptor = entries[entryIndex].descriptor,
                canonicalKey = entries[entryIndex].stableKey
            };
            group.memberIndexes.Add(entryIndex);
            return group;
        }

        private static string CreateClusterKey(
            ClusterGroup group,
            List<RecordEntry> entries)
        {
            ulong hash = 14695981039346656037UL;
            hash = AddHashInt(hash, group.memberIndexes.Count);
            for (int i = 0; i < group.memberIndexes.Count; i++)
            {
                string token = entries[group.memberIndexes[i]].stableKey;
                for (int j = 0; j < token.Length; j++)
                {
                    unchecked
                    {
                        hash ^= token[j];
                        hash *= 1099511628211UL;
                    }
                }
                hash = AddHashInt(hash, 124); // deterministic member separator
            }

            return "spc-" + hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static ulong AddHashInt(ulong hash, int value)
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

        private sealed class RecordEntry
        {
            public readonly IndividualHistoryRecord record;
            public readonly GenomeDistance.Descriptor descriptor;
            public readonly string stableKey;
            public string fallbackId;

            public RecordEntry(IndividualHistoryRecord record)
            {
                this.record = record;
                descriptor = GenomeDistance.Describe(record.genome);
                stableKey = CreateStableRecordKey(record);
                fallbackId = "record-" + HashText(stableKey);
            }

            public static int Compare(RecordEntry first, RecordEntry second)
            {
                return string.CompareOrdinal(first.stableKey, second.stableKey);
            }
        }

        private static string CreateStableRecordKey(IndividualHistoryRecord record)
        {
            string id = string.IsNullOrEmpty(record.genomeId) ? "missing" : record.genomeId;
            string parent = record.parentId ?? string.Empty;
            string secondaryParent = record.secondaryParentId ?? string.Empty;
            return string.Concat(
                id,
                "|g=", record.generation.ToString(CultureInfo.InvariantCulture),
                "|p=", parent,
                "|sp=", secondaryParent,
                "|genome=", GenomeDistance.StableSignature(record.genome));
        }

        private static void AssignMissingRecordFallbackIds(List<RecordEntry> entries)
        {
            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                RecordEntry entry = entries[i];
                if (!string.IsNullOrEmpty(entry.record.genomeId))
                {
                    continue;
                }

                int occurrence;
                if (!occurrences.TryGetValue(entry.stableKey, out occurrence))
                {
                    occurrence = 0;
                }
                occurrence++;
                occurrences[entry.stableKey] = occurrence;
                entry.fallbackId = "record-"
                    + HashText(entry.stableKey)
                    + "-"
                    + occurrence.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string HashText(string value)
        {
            ulong hash = 14695981039346656037UL;
            if (value != null)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    unchecked
                    {
                        hash ^= value[i];
                        hash *= 1099511628211UL;
                    }
                }
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private sealed class ClusterGroup
        {
            public readonly List<int> memberIndexes = new List<int>();
            public GenomeDistance.Descriptor representativeDescriptor;
            public string canonicalKey = string.Empty;
        }

        private sealed class LineageAccumulator
        {
            private readonly string rootId;
            private string bestId = string.Empty;
            private string bestStableKey = string.Empty;
            private string bestSpeciesKey = string.Empty;
            private int bestParts;
            private int bestJoints;
            private float maxFitness = float.NegativeInfinity;
            private int earliest = int.MaxValue;
            private int latest = int.MinValue;
            private int members;
            private int living;
            private readonly LearningTelemetrySummary learning = new LearningTelemetrySummary();

            public string Id { get { return rootId; } }

            public LineageAccumulator(string rootId) { this.rootId = rootId; }

            public void Add(IndividualHistoryRecord record, string speciesKey, string stableKey)
            {
                members++;
                if (record.wasAlive) living++;
                if (record.learningMetricsAvailable)
                {
                    learning.Observe(record.lifetimeLearningEnabled, record.learningSignal, record.learningAdaptationMagnitude);
                }
                earliest = Math.Min(earliest, record.generation);
                latest = Math.Max(latest, record.generation);

                if (IsBetterRepresentative(record, stableKey, bestStableKey, maxFitness))
                {
                    bestId = record.genomeId ?? string.Empty;
                    bestStableKey = stableKey;
                    bestSpeciesKey = speciesKey ?? string.Empty;
                    bestParts = SafeCount(record.bodyPartCount);
                    bestJoints = SafeCount(record.jointCount);
                }

                if (record.hasFitness && IsFinite(record.fitness) && record.fitness > maxFitness)
                {
                    maxFitness = record.fitness;
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
                    maxFitness = IsFinite(maxFitness) ? maxFitness : 0f,
                    representativeBodyPartCount = bestParts,
                    representativeJointCount = bestJoints,
                    speciesKey = bestSpeciesKey,
                    learning = learning.Clone()
                };
            }
        }

        private sealed class SpeciesAccumulator
        {
            private readonly string key;
            private string representativeId = string.Empty;
            private string representativeStableKey = string.Empty;
            private float representativeFitness = float.NegativeInfinity;
            private int members;
            private int living;
            private int first = int.MaxValue;
            private int last = int.MinValue;
            private float parts;
            private float fitness;
            private int fitnessCount;
            private readonly LearningTelemetrySummary learning = new LearningTelemetrySummary();

            public string Key { get { return key; } }

            public SpeciesAccumulator(string key) { this.key = key; }

            public void Add(IndividualHistoryRecord record, string stableKey)
            {
                members++;
                if (record.wasAlive) living++;
                if (record.learningMetricsAvailable)
                {
                    learning.Observe(record.lifetimeLearningEnabled, record.learningSignal, record.learningAdaptationMagnitude);
                }
                first = Math.Min(first, record.generation);
                last = Math.Max(last, record.generation);
                parts += SafeCount(record.bodyPartCount);

                if (record.hasFitness && IsFinite(record.fitness))
                {
                    fitness += record.fitness;
                    fitnessCount++;
                }

                if (IsBetterRepresentative(record, stableKey, representativeStableKey, representativeFitness))
                {
                    representativeFitness = IsUsableFitness(record) ? record.fitness : float.NegativeInfinity;
                    representativeStableKey = stableKey;
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
                    averageFitness = fitnessCount == 0 ? 0f : fitness / fitnessCount,
                    learning = learning.Clone()
                };
            }
        }

        private static bool IsBetterRepresentative(
            IndividualHistoryRecord candidate,
            string candidateKey,
            string currentKey,
            float currentFitness)
        {
            bool candidateHasFitness = IsUsableFitness(candidate);
            bool currentHasFitness = IsFinite(currentFitness);
            if (candidateHasFitness != currentHasFitness)
            {
                return candidateHasFitness;
            }
            if (candidateHasFitness && candidate.fitness != currentFitness)
            {
                return candidate.fitness > currentFitness;
            }
            return string.IsNullOrEmpty(currentKey)
                || string.CompareOrdinal(candidateKey, currentKey) < 0;
        }

        private static bool IsUsableFitness(IndividualHistoryRecord record)
        {
            return record != null && record.hasFitness && IsFinite(record.fitness);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static int SafeCount(int value)
        {
            return value < 0 ? 0 : value;
        }
    }
}
