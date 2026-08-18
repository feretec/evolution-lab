using System.Collections.Generic;
using NUnit.Framework;

namespace EvolutionLab.Tests
{
    public sealed class EvolutionEventDetectorContractTests
    {
        [Test]
        public void Detect_DoesNotEmitEventsInsideConfiguredThresholds()
        {
            var detector = new EvolutionEventDetector();
            var population = new List<CreatureGenome> { Genome("g1", 2), Genome("g2", 2) };
            detector.Detect(Report(1, 10), population);

            List<EvolutionEventRecord> events = detector.Detect(Report(2, 11), population);

            Assert.That(events, Is.Empty);
        }

        [Test]
        public void Detect_ReportsRapidPopulationChange()
        {
            var detector = new EvolutionEventDetector();
            var population = new List<CreatureGenome> { Genome("g1", 2) };
            detector.Detect(Report(1, 10), population);

            List<EvolutionEventRecord> events = detector.Detect(Report(2, 15), population);

            Assert.That(events.Exists(e => e.type == EvolutionEventType.PopulationChange), Is.True);
        }

        [Test]
        public void Detect_ReportsMajorMorphologyChange()
        {
            var detector = new EvolutionEventDetector();
            detector.Detect(Report(1, 10), new List<CreatureGenome> { Genome("g1", 2) });

            List<EvolutionEventRecord> events = detector.Detect(
                Report(2, 10),
                new List<CreatureGenome> { Genome("g2", 5) });

            Assert.That(events.Exists(e => e.type == EvolutionEventType.MajorMorphology), Is.True);
        }

        private static GenerationReport Report(int generation, int population)
        {
            return new GenerationReport { generation = generation, population = population, bestGenomeId = "best" };
        }

        private static CreatureGenome Genome(string id, int partCount)
        {
            CreatureGenome genome = CreatureGenome.CreateFounder(new System.Random(id.GetHashCode()), 1, id);
            while (genome.bodyParts.Count > partCount) genome.bodyParts.RemoveAt(genome.bodyParts.Count - 1);
            while (genome.bodyParts.Count < partCount)
            {
                BodyPartGene part = BodyPartGene.CreateRoot(1f, 0.35f, 1f);
                part.parentIndex = genome.bodyParts.Count - 1;
                part.localOffset = UnityEngine.Vector3.up * 0.5f;
                genome.bodyParts.Add(part);
            }
            return genome;
        }
    }
}
