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

        public CreatureEvaluationResult(CreatureGenome genome, float fitness, float distance)
        {
            this.genome = genome;
            this.fitness = fitness;
            this.distance = distance;
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

        public GenerationReport Clone()
        {
            return new GenerationReport
            {
                generation = generation,
                population = population,
                bestFitness = bestFitness,
                averageFitness = averageFitness,
                bestGenomeId = bestGenomeId
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

        public GenerationRecord Clone()
        {
            return new GenerationRecord
            {
                generation = generation,
                population = population,
                bestFitness = bestFitness,
                averageFitness = averageFitness,
                bestGenomeId = bestGenomeId
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
        public CreatureGenome genome;

        public static IndividualHistoryRecord FromGenome(
            CreatureGenome source,
            float recordedFitness,
            float recordedDistance,
            bool includeFitness)
        {
            if (source == null)
            {
                return null;
            }

            source.Repair();
            return new IndividualHistoryRecord
            {
                genomeId = source.genomeId,
                parentId = source.parentId,
                secondaryParentId = source.secondaryParentId,
                generation = source.generation,
                fitness = recordedFitness,
                distance = recordedDistance,
                bodyPartCount = source.bodyParts == null ? 0 : source.bodyParts.Count,
                jointCount = source.JointCount,
                hasFitness = includeFitness,
                genome = source.Clone()
            };
        }
    }

    [Serializable]
    public sealed class SimulationHistoryArchive
    {
        public List<GenerationRecord> generations = new List<GenerationRecord>();
        public List<IndividualHistoryRecord> individuals = new List<IndividualHistoryRecord>();
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

        private Dictionary<string, IndividualHistoryRecord> individualById;

        public IReadOnlyList<GenerationRecord> Generations
        {
            get { return generations; }
        }

        public IReadOnlyList<IndividualHistoryRecord> Individuals
        {
            get { return individuals; }
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
                bestGenomeId = report.bestGenomeId
            });

            // History is intentionally bounded so a long observation run does not grow forever.
            if (generations.Count > MaxGenerationRecords)
            {
                generations.RemoveAt(0);
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

                RegisterGenome(result.genome, result.fitness, result.distance, true);
            }
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

        private void RegisterGenome(
            CreatureGenome source,
            float recordedFitness,
            float recordedDistance,
            bool includeFitness)
        {
            if (source == null || string.IsNullOrEmpty(source.genomeId))
            {
                return;
            }

            EnsureIndex();
            if (individualById.TryGetValue(source.genomeId, out IndividualHistoryRecord existing))
            {
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
                }

                return;
            }

            IndividualHistoryRecord record = IndividualHistoryRecord.FromGenome(
                source,
                recordedFitness,
                recordedDistance,
                includeFitness);
            if (record == null)
            {
                return;
            }

            individuals.Add(record);
            individualById.Add(record.genomeId, record);
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
                genome = source.genome.Clone()
            };
        }
    }

    /// <summary>
    /// Genome-only selection and breeding. It never creates or destroys Unity objects.
    /// </summary>
    public sealed class EvolutionEngine
    {
        private readonly System.Random random;
        private readonly int populationSize;
        private int genomeSerial;

        public EvolutionEngine(int populationSize, int seed)
        {
            this.populationSize = Mathf.Max(2, populationSize);
            random = new System.Random(seed);
            CurrentPopulation = new List<CreatureGenome>(this.populationSize);
            History = new SimulationHistory();
            LastReport = new GenerationReport();
        }

        public int Generation { get; private set; } = 1;

        public List<CreatureGenome> CurrentPopulation { get; private set; }

        public GenerationReport LastReport { get; private set; }

        public SimulationHistory History { get; private set; }

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
            for (int i = 0; i < ranked.Count; i++)
            {
                total += ranked[i].fitness;
            }

            return new GenerationReport
            {
                generation = Generation,
                population = ranked.Count,
                bestFitness = ranked[0].fitness,
                averageFitness = total / ranked.Count,
                bestGenomeId = ranked[0].genome.genomeId
            };
        }

        private CreatureGenome PickParent(List<CreatureEvaluationResult> ranked)
        {
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
