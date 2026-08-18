using NUnit.Framework;

namespace EvolutionLab.Tests
{
    public sealed class NaturalHistoryTests
    {
        [Test]
        public void Catalog_HasConsistentLineageAndSpeciesAggregates()
        {
            var history = new SimulationHistory();
            CreatureGenome founder = CreatureGenome.CreateFounder(new System.Random(11), 1, "root");
            CreatureGenome child = CreatureGenome.Crossover(founder, null, new System.Random(12), 2, "child");
            CreatureGenome independent = CreatureGenome.CreateFounder(new System.Random(13), 1, "independent");
            history.RecordIndividuals(new[]
            {
                new CreatureEvaluationResult(founder, 3f, 2f, 0f, 0f, 0, "old age", false),
                new CreatureEvaluationResult(child, 6f, 4f, 5f, 1f, 0, string.Empty, true),
                new CreatureEvaluationResult(independent, 1f, 1f, 0f, 0f, 0, "extinct", false)
            });

            NaturalHistoryCatalog catalog = history.NaturalHistory;

            Assert.That(catalog.Lineages.Count, Is.EqualTo(2));
            Assert.That(catalog.Species.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(SumLineageMembers(catalog), Is.EqualTo(history.Individuals.Count));
            Assert.That(SumSpeciesMembers(catalog), Is.EqualTo(history.Individuals.Count));
            for (int i = 0; i < catalog.Lineages.Count; i++)
            {
                LineageSummary lineage = catalog.Lineages[i];
                Assert.That(lineage.lineageId, Is.Not.Empty);
                Assert.That(lineage.memberCount, Is.GreaterThan(0));
                Assert.That(lineage.latestGeneration, Is.GreaterThanOrEqualTo(lineage.earliestGeneration));
                Assert.That(lineage.livingCount, Is.InRange(0, lineage.memberCount));
            }
            for (int i = 0; i < catalog.Species.Count; i++)
            {
                SpeciesSummary species = catalog.Species[i];
                Assert.That(species.speciesKey, Is.Not.Empty);
                Assert.That(species.memberCount, Is.GreaterThan(0));
                Assert.That(species.livingCount, Is.InRange(0, species.memberCount));
                Assert.That(species.generationLastSeen, Is.GreaterThanOrEqualTo(species.generationFirstSeen));
            }
        }

        private static int SumLineageMembers(NaturalHistoryCatalog catalog)
        {
            int total = 0;
            for (int i = 0; i < catalog.Lineages.Count; i++) total += catalog.Lineages[i].memberCount;
            return total;
        }

        private static int SumSpeciesMembers(NaturalHistoryCatalog catalog)
        {
            int total = 0;
            for (int i = 0; i < catalog.Species.Count; i++) total += catalog.Species[i].memberCount;
            return total;
        }
    }
}
