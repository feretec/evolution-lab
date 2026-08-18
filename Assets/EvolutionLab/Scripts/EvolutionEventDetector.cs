using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLab
{
    /// <summary>
    /// Detects noteworthy population-scale changes from snapshots. It records
    /// observations only and never feeds a score or role back into evolution.
    /// </summary>
    public sealed class EvolutionEventDetector
    {
        private bool hasBaseline;
        private int previousPopulation;
        private float previousMeanPartCount;
        private float previousMeanMass;

        public void Reset()
        {
            hasBaseline = false;
            previousPopulation = 0;
            previousMeanPartCount = 0f;
            previousMeanMass = 0f;
        }

        public List<EvolutionEventRecord> Detect(
            GenerationReport report,
            IReadOnlyList<CreatureGenome> population)
        {
            var detected = new List<EvolutionEventRecord>();
            if (report == null)
            {
                return detected;
            }

            MeasureMorphology(population, out float meanParts, out float meanMass);
            if (hasBaseline)
            {
                int populationDelta = report.population - previousPopulation;
                float populationRatio = previousPopulation <= 0
                    ? 0f
                    : Mathf.Abs(populationDelta) / (float)previousPopulation;
                if (Mathf.Abs(populationDelta) >= 2 && populationRatio >= 0.30f)
                {
                    detected.Add(new EvolutionEventRecord
                    {
                        generation = report.generation,
                        type = EvolutionEventType.PopulationChange,
                        subjectId = report.bestGenomeId ?? string.Empty,
                        message = populationDelta > 0
                            ? "Population expanded rapidly during this period."
                            : "Population contracted rapidly during this period.",
                        value = populationDelta
                    });
                }

                float partDelta = meanParts - previousMeanPartCount;
                float massRatio = previousMeanMass <= 0.001f
                    ? 0f
                    : Mathf.Abs(meanMass - previousMeanMass) / previousMeanMass;
                if (Mathf.Abs(partDelta) >= 0.65f || massRatio >= 0.22f)
                {
                    detected.Add(new EvolutionEventRecord
                    {
                        generation = report.generation,
                        type = EvolutionEventType.MajorMorphology,
                        subjectId = report.bestGenomeId ?? string.Empty,
                        message = partDelta >= 0f
                            ? "The population shifted toward larger or more articulated bodies."
                            : "The population shifted toward simpler or lighter bodies.",
                        value = Mathf.Abs(partDelta) >= 0.65f ? partDelta : meanMass - previousMeanMass
                    });
                }
            }

            hasBaseline = true;
            previousPopulation = Mathf.Max(0, report.population);
            previousMeanPartCount = meanParts;
            previousMeanMass = meanMass;
            return detected;
        }

        private static void MeasureMorphology(
            IReadOnlyList<CreatureGenome> population,
            out float meanParts,
            out float meanMass)
        {
            float parts = 0f;
            float mass = 0f;
            int genomeCount = 0;
            if (population != null)
            {
                for (int i = 0; i < population.Count; i++)
                {
                    CreatureGenome genome = population[i];
                    if (genome == null || genome.bodyParts == null)
                    {
                        continue;
                    }

                    genomeCount++;
                    parts += genome.bodyParts.Count;
                    for (int partIndex = 0; partIndex < genome.bodyParts.Count; partIndex++)
                    {
                        float partMass = genome.bodyParts[partIndex].mass;
                        if (!float.IsNaN(partMass) && !float.IsInfinity(partMass))
                        {
                            mass += Mathf.Max(0f, partMass);
                        }
                    }
                }
            }

            meanParts = genomeCount == 0 ? 0f : parts / genomeCount;
            meanMass = genomeCount == 0 ? 0f : mass / genomeCount;
        }
    }
}
