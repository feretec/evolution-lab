using System;
using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLab
{
    [Serializable]
    public sealed class CreatureEvaluationResult
    {
        public CreatureGenome genome;
        public float fitness;
        public float distance;
        public float energy;
        public float age;
        public int offspringCount;
        public int killCount;
        public float damageTaken;
        public string deathReason = string.Empty;
        public bool alive;
        public bool learningMetricsAvailable;
        public bool lifetimeLearningEnabled;
        public float learningSignal;
        public float learningAdaptationMagnitude;

        public CreatureEvaluationResult(CreatureGenome genome, float fitness, float distance)
        {
            this.genome = genome;
            this.fitness = fitness;
            this.distance = distance;
            alive = true;
        }

        public CreatureEvaluationResult(
            CreatureGenome genome,
            float fitness,
            float distance,
            float energy,
            float age,
            int offspringCount,
            string deathReason,
            bool alive)
        {
            this.genome = genome;
            this.fitness = fitness;
            this.distance = distance;
            this.energy = energy;
            this.age = age;
            this.offspringCount = offspringCount;
            this.deathReason = deathReason ?? string.Empty;
            this.alive = alive;
        }

        public CreatureEvaluationResult WithCombatStats(int kills, float damage)
        {
            killCount = Mathf.Max(0, kills);
            damageTaken = Mathf.Max(0f, damage);
            return this;
        }

        public CreatureEvaluationResult WithLearningMetrics(bool enabled, float signal, float adaptation)
        {
            if (!float.IsNaN(signal) && !float.IsInfinity(signal)
                && !float.IsNaN(adaptation) && !float.IsInfinity(adaptation))
            {
                learningMetricsAvailable = true;
                lifetimeLearningEnabled = enabled;
                learningSignal = signal;
                learningAdaptationMagnitude = Mathf.Max(0f, adaptation);
            }
            return this;
        }

        public CreatureEvaluationResult WithLearningMetrics(Creature creature)
        {
            return creature == null
                ? this
                : WithLearningMetrics(
                    creature.LifetimeLearningEnabled,
                    creature.LearningSignal,
                    creature.LearningAdaptationMagnitude);
        }
    }

    [Serializable]
    public sealed class GenerationReport
    {
        public int generation;
        public int population;
        public float bestFitness;
        public float averageFitness;
        public string bestGenomeId = string.Empty;
        public int births;
        public int deaths;
        public int predations;
        public int interactions;
        public float averageEnergy;
        public float averageAge;
        public LearningTelemetrySummary learning = new LearningTelemetrySummary();

        public GenerationReport Clone()
        {
            return new GenerationReport
            {
                generation = generation,
                population = population,
                bestFitness = bestFitness,
                averageFitness = averageFitness,
                bestGenomeId = bestGenomeId,
                births = births,
                deaths = deaths,
                predations = predations,
                interactions = interactions,
                averageEnergy = averageEnergy,
                averageAge = averageAge,
                learning = learning == null ? new LearningTelemetrySummary() : learning.Clone()
            };
        }
    }

    [Serializable]
    public sealed class GenerationRecord
    {
        public int generation;
        public int population;
        public float bestFitness;
        public float averageFitness;
        public string bestGenomeId = string.Empty;
        public int births;
        public int deaths;
        public int predations;
        public int interactions;
        public float averageEnergy;
        public float averageAge;
        public LearningTelemetrySummary learning = new LearningTelemetrySummary();

        public GenerationRecord Clone()
        {
            return new GenerationRecord
            {
                generation = generation,
                population = population,
                bestFitness = bestFitness,
                averageFitness = averageFitness,
                bestGenomeId = bestGenomeId,
                births = births,
                deaths = deaths,
                predations = predations,
                interactions = interactions,
                averageEnergy = averageEnergy,
                averageAge = averageAge,
                learning = learning == null ? new LearningTelemetrySummary() : learning.Clone()
            };
        }
    }

    [Serializable]
    public sealed class IndividualHistoryRecord
    {
        public string genomeId = string.Empty;
        public string parentId = string.Empty;
        public string secondaryParentId = string.Empty;
        public int generation;
        public float fitness;
        public float distance;
        public int bodyPartCount;
        public int jointCount;
        public bool hasFitness;
        public float energy;
        public float age;
        public int offspringCount;
        public int killCount;
        public float damageTaken;
        public int deathGeneration;
        public string deathReason = string.Empty;
        public bool wasAlive;
        public bool learningMetricsAvailable;
        public bool lifetimeLearningEnabled;
        public float learningSignal;
        public float learningAdaptationMagnitude;
        public CreatureGenome genome;

        public static IndividualHistoryRecord FromGenome(
            CreatureGenome source,
            float recordedFitness,
            float recordedDistance,
            bool includeFitness,
            float recordedEnergy = 0f,
            float recordedAge = 0f,
            int recordedOffspringCount = 0,
            string recordedDeathReason = "",
            bool recordedAlive = true,
            int recordedKillCount = 0,
            float recordedDamageTaken = 0f,
            int recordedDeathGeneration = 0,
            bool recordedLearningMetricsAvailable = false,
            bool recordedLifetimeLearningEnabled = false,
            float recordedLearningSignal = 0f,
            float recordedLearningAdaptationMagnitude = 0f)
        {
            if (source == null)
            {
                return null;
            }

            CreatureGenome snapshot = source.Clone();
            snapshot.Repair();
            return new IndividualHistoryRecord
            {
                genomeId = snapshot.genomeId,
                parentId = snapshot.parentId,
                secondaryParentId = snapshot.secondaryParentId,
                generation = snapshot.generation,
                fitness = recordedFitness,
                distance = recordedDistance,
                bodyPartCount = snapshot.bodyParts == null ? 0 : snapshot.bodyParts.Count,
                jointCount = snapshot.JointCount,
                hasFitness = includeFitness,
                energy = recordedEnergy,
                age = recordedAge,
                offspringCount = recordedOffspringCount,
                killCount = recordedKillCount,
                damageTaken = recordedDamageTaken,
                deathGeneration = recordedDeathGeneration,
                deathReason = recordedDeathReason ?? string.Empty,
                wasAlive = recordedAlive,
                learningMetricsAvailable = recordedLearningMetricsAvailable,
                lifetimeLearningEnabled = recordedLifetimeLearningEnabled,
                learningSignal = recordedLearningSignal,
                learningAdaptationMagnitude = Mathf.Max(0f, recordedLearningAdaptationMagnitude),
                genome = snapshot
            };
        }
    }

    [Serializable]
    public sealed class SimulationHistoryArchive
    {
        public List<GenerationRecord> generations = new List<GenerationRecord>();
        public List<IndividualHistoryRecord> individuals = new List<IndividualHistoryRecord>();
        public List<EvolutionEventRecord> events = new List<EvolutionEventRecord>();
        public int currentCycle;
    }

    [Serializable]
    public sealed class SimulationHistory
    {
        private const int MaxGenerationRecords = 2000;
        private const int MaxIndividualRecords = 8192;

        [SerializeField]
        private List<GenerationRecord> generations = new List<GenerationRecord>();

        [SerializeField]
        private List<IndividualHistoryRecord> individuals = new List<IndividualHistoryRecord>();

        [SerializeField]
        private List<EvolutionEventRecord> events = new List<EvolutionEventRecord>();

        [SerializeField]
        private int currentCycle;

        // Runtime-only revision used by NaturalHistoryCatalog cache invalidation.
        // It is intentionally not serialized; loading an archive starts a new
        // in-memory revision and therefore cannot reuse a previous catalog.
        [NonSerialized]
        private int revision;

        private Dictionary<string, IndividualHistoryRecord> individualById;

        public IReadOnlyList<GenerationRecord> Generations
        {
            get { return generations; }
        }

        public IReadOnlyList<IndividualHistoryRecord> Individuals
        {
            get { return individuals; }
        }

        public IReadOnlyList<EvolutionEventRecord> Events
        {
            get { return events; }
        }

        public int CurrentCycle
        {
            get { return currentCycle; }
        }

        public int Revision
        {
            get { return revision; }
        }

        public NaturalHistoryCatalog NaturalHistory
        {
            get { return NaturalHistoryCatalog.Build(individuals, revision); }
        }

        public string ToJson()
        {
            EnsureIndex();
            var archive = new SimulationHistoryArchive();
            for (int i = 0; i < generations.Count; i++)
            {
                if (generations[i] != null)
                {
                    archive.generations.Add(generations[i].Clone());
                }
            }

            for (int i = 0; i < individuals.Count; i++)
            {
                IndividualHistoryRecord record = CloneRecord(individuals[i]);
                if (record != null)
                {
                    archive.individuals.Add(record);
                }
            }

            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] != null)
                {
                    archive.events.Add(events[i].Clone());
                }
            }

            archive.currentCycle = currentCycle;

            return JsonUtility.ToJson(archive, true);
        }

        public bool TryLoadJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            SimulationHistoryArchive archive;
            try
            {
                archive = JsonUtility.FromJson<SimulationHistoryArchive>(json);
            }
            catch (Exception)
            {
                return false;
            }

            if (archive == null || archive.generations == null || archive.individuals == null)
            {
                return false;
            }

            generations = new List<GenerationRecord>();
            for (int i = 0; i < archive.generations.Count; i++)
            {
                GenerationRecord record = archive.generations[i];
                if (record != null)
                {
                    generations.Add(record.Clone());
                }
            }

            individuals = new List<IndividualHistoryRecord>();
            for (int i = 0; i < archive.individuals.Count; i++)
            {
                IndividualHistoryRecord record = CloneRecord(archive.individuals[i]);
                if (record != null && record.genome != null && !string.IsNullOrEmpty(record.genomeId))
                {
                    individuals.Add(record);
                }
            }

            events = new List<EvolutionEventRecord>();
            if (archive.events != null)
            {
                for (int i = 0; i < archive.events.Count; i++)
                {
                    if (archive.events[i] != null)
                    {
                        events.Add(archive.events[i].Clone());
                    }
                }
            }

            currentCycle = Mathf.Max(0, archive.currentCycle);
            revision = 0;

            while (generations.Count > MaxGenerationRecords)
            {
                generations.RemoveAt(0);
            }

            while (individuals.Count > MaxIndividualRecords)
            {
                individuals.RemoveAt(0);
            }

            individualById = null;
            EnsureIndex();
            return true;
        }

        public void Record(GenerationReport report)
        {
            if (report == null)
            {
                return;
            }

            generations.Add(new GenerationRecord
            {
                generation = report.generation,
                population = report.population,
                bestFitness = report.bestFitness,
                averageFitness = report.averageFitness,
                bestGenomeId = report.bestGenomeId,
                births = report.births,
                deaths = report.deaths,
                predations = report.predations,
                interactions = report.interactions,
                averageEnergy = report.averageEnergy,
                averageAge = report.averageAge,
                learning = report.learning == null ? new LearningTelemetrySummary() : report.learning.Clone()
            });

            currentCycle++;

            // History is intentionally bounded so a long observation run does not grow forever.
            if (generations.Count > MaxGenerationRecords)
            {
                generations.RemoveAt(0);
            }
        }

        public void RecordEvent(
            EvolutionEventType type,
            int generation,
            string subjectId,
            string relatedId,
            string message,
            float value = 0f)
        {
            if (events == null)
            {
                events = new List<EvolutionEventRecord>();
            }

            events.Add(new EvolutionEventRecord
            {
                cycle = currentCycle,
                generation = generation,
                type = type,
                subjectId = subjectId ?? string.Empty,
                relatedId = relatedId ?? string.Empty,
                message = message ?? string.Empty,
                value = value
            });

            while (events.Count > 4096)
            {
                events.RemoveAt(0);
            }
        }

        public void RegisterPopulation(IReadOnlyList<CreatureGenome> population)
        {
            if (population == null)
            {
                return;
            }

            for (int i = 0; i < population.Count; i++)
            {
                RegisterGenome(population[i], 0f, 0f, false);
            }
        }

        public void RecordIndividuals(IReadOnlyList<CreatureEvaluationResult> results)
        {
            if (results == null)
            {
                return;
            }

            for (int i = 0; i < results.Count; i++)
            {
                CreatureEvaluationResult result = results[i];
                if (result == null || result.genome == null)
                {
                    continue;
                }

                RegisterGenome(
                    result.genome,
                    result.fitness,
                    result.distance,
                    true,
                    result.energy,
                    result.age,
                    result.offspringCount,
                    result.deathReason,
                    result.alive,
                    result.killCount,
                    result.damageTaken,
                    result.alive ? 0 : result.genome.generation,
                    result.learningMetricsAvailable,
                    result.lifetimeLearningEnabled,
                    result.learningSignal,
                    result.learningAdaptationMagnitude);
            }
        }

        public void RecordIndividual(CreatureEvaluationResult result)
        {
            if (result == null || result.genome == null)
            {
                return;
            }

            RecordIndividuals(new[] { result });
        }

        public bool TryGetIndividual(string genomeId, out IndividualHistoryRecord record)
        {
            EnsureIndex();
            if (string.IsNullOrEmpty(genomeId))
            {
                record = null;
                return false;
            }

            return individualById.TryGetValue(genomeId, out record);
        }

        public List<IndividualHistoryRecord> GetAncestry(CreatureGenome currentGenome, int maxDepth)
        {
            var ancestry = new List<IndividualHistoryRecord>();
            if (currentGenome == null || maxDepth <= 0)
            {
                return ancestry;
            }

            var visited = new HashSet<string>();
            CreatureGenome cursor = currentGenome;
            for (int depth = 0; cursor != null && depth < maxDepth; depth++)
            {
                string cursorId = cursor.genomeId ?? string.Empty;
                if (!string.IsNullOrEmpty(cursorId) && !visited.Add(cursorId))
                {
                    break;
                }

                IndividualHistoryRecord record;
                if (!TryGetIndividual(cursorId, out record))
                {
                    record = IndividualHistoryRecord.FromGenome(cursor, 0f, 0f, false);
                }

                if (record == null)
                {
                    break;
                }

                ancestry.Add(record);
                if (string.IsNullOrEmpty(record.parentId)
                    || !TryGetIndividual(record.parentId, out IndividualHistoryRecord parentRecord)
                    || parentRecord == null
                    || parentRecord.genome == null)
                {
                    break;
                }

                cursor = parentRecord.genome;
            }

            return ancestry;
        }

        public List<IndividualHistoryRecord> GetDescendants(string genomeId, int maxRecords)
        {
            var descendants = new List<IndividualHistoryRecord>();
            if (string.IsNullOrEmpty(genomeId) || maxRecords <= 0)
            {
                return descendants;
            }

            var frontier = new Queue<string>();
            var visited = new HashSet<string>();
            frontier.Enqueue(genomeId);
            visited.Add(genomeId);
            while (frontier.Count > 0 && descendants.Count < maxRecords)
            {
                string ancestorId = frontier.Dequeue();
                for (int i = 0; i < individuals.Count && descendants.Count < maxRecords; i++)
                {
                    IndividualHistoryRecord record = individuals[i];
                    if (record == null || string.IsNullOrEmpty(record.genomeId)
                        || visited.Contains(record.genomeId))
                    {
                        continue;
                    }

                    if (record.parentId != ancestorId && record.secondaryParentId != ancestorId)
                    {
                        continue;
                    }

                    visited.Add(record.genomeId);
                    descendants.Add(record);
                    frontier.Enqueue(record.genomeId);
                }
            }

            return descendants;
        }

        private void RegisterGenome(
            CreatureGenome source,
            float recordedFitness,
            float recordedDistance,
            bool includeFitness,
            float recordedEnergy = 0f,
            float recordedAge = 0f,
            int recordedOffspringCount = 0,
            string recordedDeathReason = "",
            bool recordedAlive = true,
            int recordedKillCount = 0,
            float recordedDamageTaken = 0f,
            int recordedDeathGeneration = 0,
            bool recordedLearningMetricsAvailable = false,
            bool recordedLifetimeLearningEnabled = false,
            float recordedLearningSignal = 0f,
            float recordedLearningAdaptationMagnitude = 0f)
        {
            if (source == null || string.IsNullOrEmpty(source.genomeId))
            {
                return;
            }

            EnsureIndex();
            if (individualById.TryGetValue(source.genomeId, out IndividualHistoryRecord existing))
            {
                bool changed = existing.parentId != (source.parentId ?? string.Empty)
                    || existing.secondaryParentId != (source.secondaryParentId ?? string.Empty)
                    || existing.generation != source.generation
                    || existing.bodyPartCount != (source.bodyParts == null ? 0 : source.bodyParts.Count)
                    || existing.jointCount != source.JointCount
                    || GenomeDistance.ContentHash(existing.genome) != GenomeDistance.ContentHash(source)
                    || (includeFitness && (!existing.hasFitness
                        || existing.fitness != recordedFitness
                        || existing.distance != recordedDistance
                        || existing.energy != recordedEnergy
                        || existing.age != recordedAge
                        || existing.offspringCount != recordedOffspringCount
                        || existing.killCount != recordedKillCount
                        || existing.damageTaken != recordedDamageTaken
                        || existing.deathReason != (recordedDeathReason ?? string.Empty)
                        || existing.wasAlive != recordedAlive
                        || existing.learningMetricsAvailable != recordedLearningMetricsAvailable
                        || existing.lifetimeLearningEnabled != recordedLifetimeLearningEnabled
                        || existing.learningSignal != recordedLearningSignal
                         || existing.learningAdaptationMagnitude != Mathf.Max(0f, recordedLearningAdaptationMagnitude)))
                     || (!includeFitness && (!existing.wasAlive || !string.IsNullOrEmpty(existing.deathReason)));

                existing.parentId = source.parentId;
                existing.secondaryParentId = source.secondaryParentId;
                existing.generation = source.generation;
                existing.bodyPartCount = source.bodyParts == null ? 0 : source.bodyParts.Count;
                existing.jointCount = source.JointCount;
                existing.genome = source.Clone();
                if (includeFitness)
                {
                    existing.fitness = recordedFitness;
                    existing.distance = recordedDistance;
                    existing.hasFitness = true;
                    existing.energy = recordedEnergy;
                    existing.age = recordedAge;
                    existing.offspringCount = recordedOffspringCount;
                    existing.killCount = recordedKillCount;
                    existing.damageTaken = recordedDamageTaken;
                    existing.learningMetricsAvailable = recordedLearningMetricsAvailable;
                    existing.lifetimeLearningEnabled = recordedLifetimeLearningEnabled;
                    existing.learningSignal = recordedLearningSignal;
                    existing.learningAdaptationMagnitude = Mathf.Max(0f, recordedLearningAdaptationMagnitude);
                    if (!recordedAlive)
                    {
                        existing.deathGeneration = recordedDeathGeneration <= 0
                            ? source.generation
                            : recordedDeathGeneration;
                    }
                    existing.deathReason = recordedDeathReason ?? string.Empty;
                    existing.wasAlive = recordedAlive;
                }
                else
                {
                    existing.wasAlive = true;
                    existing.deathReason = string.Empty;
                }

                if (changed) revision++;

                return;
            }

            IndividualHistoryRecord record = IndividualHistoryRecord.FromGenome(
                source,
                recordedFitness,
                recordedDistance,
                includeFitness,
                recordedEnergy,
                recordedAge,
                recordedOffspringCount,
                recordedDeathReason,
                recordedAlive,
                recordedKillCount,
                recordedDamageTaken,
                recordedDeathGeneration,
                recordedLearningMetricsAvailable,
                recordedLifetimeLearningEnabled,
                recordedLearningSignal,
                recordedLearningAdaptationMagnitude);
            if (record == null)
            {
                return;
            }

            individuals.Add(record);
            individualById.Add(record.genomeId, record);
            revision++;
            while (individuals.Count > MaxIndividualRecords)
            {
                IndividualHistoryRecord oldest = individuals[0];
                individuals.RemoveAt(0);
                if (oldest != null && !string.IsNullOrEmpty(oldest.genomeId))
                {
                    individualById.Remove(oldest.genomeId);
                }
            }
        }

        private void EnsureIndex()
        {
            if (individualById == null)
            {
                individualById = new Dictionary<string, IndividualHistoryRecord>();
            }

            if (individualById.Count == individuals.Count)
            {
                return;
            }

            individualById.Clear();
            for (int i = 0; i < individuals.Count; i++)
            {
                IndividualHistoryRecord record = individuals[i];
                if (record != null && !string.IsNullOrEmpty(record.genomeId))
                {
                    individualById[record.genomeId] = record;
                }
            }
        }

        private static IndividualHistoryRecord CloneRecord(IndividualHistoryRecord source)
        {
            if (source == null || source.genome == null)
            {
                return null;
            }

            return new IndividualHistoryRecord
            {
                genomeId = source.genomeId,
                parentId = source.parentId,
                secondaryParentId = source.secondaryParentId,
                generation = source.generation,
                fitness = source.fitness,
                distance = source.distance,
                bodyPartCount = source.bodyPartCount,
                jointCount = source.jointCount,
                hasFitness = source.hasFitness,
                energy = source.energy,
                age = source.age,
                offspringCount = source.offspringCount,
                killCount = source.killCount,
                damageTaken = source.damageTaken,
                deathGeneration = source.deathGeneration,
                deathReason = source.deathReason,
                wasAlive = source.wasAlive,
                learningMetricsAvailable = source.learningMetricsAvailable,
                lifetimeLearningEnabled = source.lifetimeLearningEnabled,
                learningSignal = source.learningSignal,
                learningAdaptationMagnitude = source.learningAdaptationMagnitude,
                genome = source.genome.Clone()
            };
        }
    }

    /// <summary>
    /// Genome-only selection and breeding. It never creates or destroys Unity objects.
    /// </summary>
    public sealed class EvolutionEngine
    {
        private readonly DeterministicRandom random;
        private readonly int populationSize;
        private int genomeSerial;

        public EvolutionEngine(int populationSize, int seed)
        {
            this.populationSize = Mathf.Max(2, populationSize);
            random = new DeterministicRandom(seed);
            CurrentPopulation = new List<CreatureGenome>(this.populationSize);
            History = new SimulationHistory();
            LastReport = new GenerationReport();
        }

        public int Generation { get; private set; } = 1;

        public List<CreatureGenome> CurrentPopulation { get; private set; }

        public GenerationReport LastReport { get; private set; }

        public SimulationHistory History { get; private set; }

        public uint RandomState
        {
            get { return random.State; }
        }

        public void RestoreRandomState(uint state, int fallbackSeed)
        {
            if (state == 0u)
            {
                random.Reset(fallbackSeed);
                return;
            }

            random.State = state;
        }

        public void Initialize()
        {
            Generation = 1;
            CurrentPopulation.Clear();
            for (int i = 0; i < populationSize; i++)
            {
                CurrentPopulation.Add(CreatureGenome.CreateFounder(random, Generation, CreateGenomeId(Generation, i)));
            }

            History.RegisterPopulation(CurrentPopulation);
        }

        public List<CreatureGenome> BreedNextGeneration(IReadOnlyList<CreatureEvaluationResult> results)
        {
            var ranked = new List<CreatureEvaluationResult>();
            if (results != null)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    if (results[i] != null && results[i].genome != null)
                    {
                        ranked.Add(results[i]);
                    }
                }
            }

            ranked.Sort((left, right) => right.fitness.CompareTo(left.fitness));
            LastReport = BuildReport(ranked);
            History.Record(LastReport);
            History.RecordIndividuals(ranked);

            int nextGeneration = Generation + 1;
            var nextPopulation = new List<CreatureGenome>(populationSize);
            if (ranked.Count == 0)
            {
                for (int i = 0; i < populationSize; i++)
                {
                    nextPopulation.Add(CreatureGenome.CreateFounder(
                        random,
                        nextGeneration,
                        CreateGenomeId(nextGeneration, i)));
                }

                Generation = nextGeneration;
                CurrentPopulation = nextPopulation;
                History.RegisterPopulation(CurrentPopulation);
                return nextPopulation;
            }

            int eliteCount = Mathf.Min(2, Mathf.Min(populationSize, ranked.Count));
            for (int i = 0; i < eliteCount; i++)
            {
                CreatureGenome elite = ranked[i].genome.Clone();
                elite.parentId = ranked[i].genome.genomeId;
                elite.secondaryParentId = string.Empty;
                elite.generation = nextGeneration;
                elite.genomeId = CreateGenomeId(nextGeneration, nextPopulation.Count);
                nextPopulation.Add(elite);
            }

            while (nextPopulation.Count < populationSize)
            {
                CreatureGenome first = PickParent(ranked);
                CreatureGenome second = PickParent(ranked);
                CreatureGenome child = CreatureGenome.Crossover(
                    first,
                    second,
                    random,
                    nextGeneration,
                    CreateGenomeId(nextGeneration, nextPopulation.Count));
                child.Mutate(random);
                child.Repair();
                nextPopulation.Add(child);
            }

            Generation = nextGeneration;
            CurrentPopulation = nextPopulation;
            History.RegisterPopulation(CurrentPopulation);
            return nextPopulation;
        }

        /// <summary>
        /// Creates one naturally born child without replacing the whole population.
        /// Prototype 1.5 keeps using BreedNextGeneration; Prototype 2 uses this
        /// smaller birth operation so population size can emerge from life events.
        /// </summary>
        public CreatureGenome CreateOffspring(CreatureGenome first, CreatureGenome second)
        {
            CreatureGenome a = first ?? second;
            CreatureGenome b = second ?? first;
            int firstGeneration = a == null ? 1 : a.generation;
            int secondGeneration = b == null ? firstGeneration : b.generation;
            int childGeneration = Mathf.Max(firstGeneration, secondGeneration) + 1;
            CreatureGenome child = CreatureGenome.Crossover(
                a,
                b,
                random,
                childGeneration,
                CreateGenomeId(childGeneration, CurrentPopulation.Count));
            child.Mutate(random);
            child.Repair();
            CurrentPopulation.Add(child);
            History.RegisterPopulation(new[] { child });
            return child;
        }

        public void RemovePopulationGenome(CreatureGenome genome)
        {
            if (genome == null || CurrentPopulation == null)
            {
                return;
            }

            for (int i = CurrentPopulation.Count - 1; i >= 0; i--)
            {
                if (CurrentPopulation[i] != null && CurrentPopulation[i].genomeId == genome.genomeId)
                {
                    CurrentPopulation.RemoveAt(i);
                    break;
                }
            }
        }

        public void RestorePopulation(IReadOnlyList<CreatureGenome> genomes, int generation)
        {
            CurrentPopulation.Clear();
            Generation = Mathf.Max(1, generation);
            if (genomes == null)
            {
                return;
            }

            for (int i = 0; i < genomes.Count; i++)
            {
                if (genomes[i] == null)
                {
                    continue;
                }

                CreatureGenome restored = genomes[i].Clone();
                restored.Repair();
                CurrentPopulation.Add(restored);
            }

            History.RegisterPopulation(CurrentPopulation);
        }

        /// <summary>
        /// Captures the live Creature metrics at the history boundary. This is
        /// the authoritative bridge for callers that still own Creature
        /// instances; older genome-only callers remain valid and simply carry
        /// unavailable learning telemetry.
        /// </summary>
        public CreatureEvaluationResult CaptureCreatureEvaluation(Creature creature)
        {
            return creature == null ? null : creature.CaptureEvaluation().WithLearningMetrics(creature);
        }

        public List<CreatureEvaluationResult> CaptureCreatureEvaluations(IReadOnlyList<Creature> creatures)
        {
            var results = new List<CreatureEvaluationResult>();
            if (creatures == null) return results;
            for (int i = 0; i < creatures.Count; i++)
            {
                CreatureEvaluationResult result = CaptureCreatureEvaluation(creatures[i]);
                if (result != null) results.Add(result);
            }
            return results;
        }

        public void RecordEcologyCycle(
            IReadOnlyList<CreatureEvaluationResult> results,
            int births,
            int deaths,
            int predations = 0,
            int interactions = 0)
        {
            var observed = new List<CreatureEvaluationResult>();
            if (results != null)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    if (results[i] != null && results[i].genome != null)
                    {
                        observed.Add(results[i]);
                    }
                }
            }

            observed.Sort((left, right) => right.fitness.CompareTo(left.fitness));
            LastReport = BuildReport(observed);
            LastReport.births = Mathf.Max(0, births);
            LastReport.deaths = Mathf.Max(0, deaths);
            LastReport.predations = Mathf.Max(0, predations);
            LastReport.interactions = Mathf.Max(0, interactions);
            History.Record(LastReport);
            History.RecordIndividuals(observed);
            Generation++;
        }

        private GenerationReport BuildReport(List<CreatureEvaluationResult> ranked)
        {
            if (ranked.Count == 0)
            {
                return new GenerationReport
                {
                    generation = Generation,
                    population = 0,
                    bestFitness = 0f,
                    averageFitness = 0f,
                    bestGenomeId = string.Empty
                };
            }

            float total = 0f;
            var learning = new LearningTelemetrySummary();
            for (int i = 0; i < ranked.Count; i++)
            {
                total += ranked[i].fitness;
                ObserveLearning(learning, ranked[i]);
            }

            return new GenerationReport
            {
                generation = Generation,
                population = ranked.Count,
                bestFitness = ranked[0].fitness,
                averageFitness = total / ranked.Count,
                bestGenomeId = ranked[0].genome.genomeId,
                averageEnergy = AverageEnergy(ranked),
                averageAge = AverageAge(ranked),
                learning = learning
            };
        }

        private static void ObserveLearning(LearningTelemetrySummary summary, CreatureEvaluationResult result)
        {
            if (summary == null || result == null || !result.learningMetricsAvailable)
            {
                return;
            }

            summary.Observe(
                result.lifetimeLearningEnabled,
                result.learningSignal,
                result.learningAdaptationMagnitude);
        }

        private static float AverageEnergy(List<CreatureEvaluationResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return 0f;
            }

            float total = 0f;
            for (int i = 0; i < results.Count; i++)
            {
                total += Mathf.Max(0f, results[i].energy);
            }

            return total / results.Count;
        }

        private static float AverageAge(List<CreatureEvaluationResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return 0f;
            }

            float total = 0f;
            for (int i = 0; i < results.Count; i++)
            {
                total += Mathf.Max(0f, results[i].age);
            }

            return total / results.Count;
        }

        private CreatureGenome PickParent(List<CreatureEvaluationResult> ranked)
        {
            if (ranked == null || ranked.Count == 0)
            {
                return null;
            }

            if (ranked.Count == 1)
            {
                return ranked[0].genome;
            }

            int selectionPool = Mathf.Clamp(ranked.Count / 2, 2, ranked.Count);
            int totalWeight = selectionPool * (selectionPool + 1) / 2;
            int roll = random.Next(0, Mathf.Max(1, totalWeight));
            int cumulative = 0;
            for (int i = 0; i < selectionPool; i++)
            {
                cumulative += selectionPool - i;
                if (roll < cumulative)
                {
                    return ranked[i].genome;
                }
            }

            return ranked[0].genome;
        }

        private string CreateGenomeId(int generation, int populationIndex)
        {
            genomeSerial++;
            return string.Format("G{0:0000}-C{1:000000}-P{2:00}", generation, genomeSerial, populationIndex);
        }
    }
}
