using System.Collections.Generic;
using NUnit.Framework;

namespace EvolutionLab.Tests
{
    public sealed class NaturalHistoryContractTests
    {
        [Test]
        public void Build_IsIndependentOfInputOrder()
        {
            CreatureGenome first = CreatureGenome.CreateFounder(new System.Random(21), 1, "first");
            CreatureGenome second = CreatureGenome.CreateFounder(new System.Random(22), 2, "second");
            CreatureGenome child = CreatureGenome.Crossover(first, second, new System.Random(23), 3, "child");
            var records = new List<IndividualHistoryRecord>
            {
                IndividualHistoryRecord.FromGenome(first, 1f, 1f, true),
                IndividualHistoryRecord.FromGenome(second, 2f, 2f, true),
                IndividualHistoryRecord.FromGenome(child, 3f, 3f, true)
            };
            List<IndividualHistoryRecord> reversed = new List<IndividualHistoryRecord>(records);
            reversed.Reverse();

            NaturalHistoryCatalog a = NaturalHistoryCatalog.Build(records);
            NaturalHistoryCatalog b = NaturalHistoryCatalog.Build(reversed);

            Assert.That(b.Species.Count, Is.EqualTo(a.Species.Count));
            for (int i = 0; i < a.Species.Count; i++)
            {
                Assert.That(b.Species[i].speciesKey, Is.EqualTo(a.Species[i].speciesKey));
                Assert.That(b.Species[i].memberCount, Is.EqualTo(a.Species[i].memberCount));
                Assert.That(b.Species[i].representativeGenomeId, Is.EqualTo(a.Species[i].representativeGenomeId));
            }
        }

        [Test]
        public void Build_SafelyHandlesMissingGenomeRecords()
        {
            var records = new List<IndividualHistoryRecord>
            {
                new IndividualHistoryRecord { genomeId = "missing", generation = 4, hasFitness = true, fitness = 1f },
                null
            };

            NaturalHistoryCatalog catalog = null;
            Assert.DoesNotThrow(() => catalog = NaturalHistoryCatalog.Build(records));
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Species.Count, Is.EqualTo(1));
            Assert.That(catalog.Species[0].memberCount, Is.EqualTo(1));
        }
    }
}
