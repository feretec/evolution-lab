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
    }

    [Serializable]
    public sealed class SimulationHistory
    {
        [SerializeField]
        private List<GenerationRecord> generations = new List<GenerationRecord>();

        public IReadOnlyList<GenerationRecord> Generations
        {
            get { return generations; }
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

            // Prototype history is intentionally bounded so a long observation run does not grow forever.
            if (generations.Count > 2000)
            {
                generations.RemoveAt(0);
            }
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
