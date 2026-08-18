using System.Collections.Generic;
using NUnit.Framework;

namespace EvolutionLab.Tests
{
    public sealed class SimulationHistoryTests
    {
        [Test]
        public void JsonRoundTrip_PreservesGenerationsIndividualsAndEvents()
        {
            SimulationHistory history = BuildHistory(out CreatureGenome founder, out CreatureGenome child, out CreatureGenome grandchild);
            string json = history.ToJson();
            var restored = new SimulationHistory();

            Assert.That(restored.TryLoadJson(json), Is.True);
            Assert.That(restored.CurrentCycle, Is.EqualTo(history.CurrentCycle));
            Assert.That(restored.Generations.Count, Is.EqualTo(history.Generations.Count));
            Assert.That(restored.Individuals.Count, Is.EqualTo(history.Individuals.Count));
            Assert.That(restored.Events.Count, Is.EqualTo(history.Events.Count));
            Assert.That(restored.TryGetIndividual(grandchild.genomeId, out IndividualHistoryRecord record), Is.True);
            Assert.That(record.parentId, Is.EqualTo(child.genomeId));
            Assert.That(record.genome.bodyParts.Count, Is.EqualTo(grandchild.bodyParts.Count));
            Assert.That(founder.genomeId, Is.EqualTo("founder"));
        }

        [Test]
        public void AncestryAndDescendants_FollowPrimaryAndSecondaryParentLinks()
        {
            SimulationHistory history = BuildHistory(out CreatureGenome founder, out CreatureGenome child, out CreatureGenome grandchild);

            List<IndividualHistoryRecord> ancestry = history.GetAncestry(grandchild, 10);
            List<IndividualHistoryRecord> descendants = history.GetDescendants(founder.genomeId, 10);

            Assert.That(ancestry.Count, Is.EqualTo(3));
            Assert.That(ancestry[0].genomeId, Is.EqualTo(grandchild.genomeId));
            Assert.That(ancestry[1].genomeId, Is.EqualTo(child.genomeId));
            Assert.That(ancestry[2].genomeId, Is.EqualTo(founder.genomeId));
            Assert.That(descendants.Exists(x => x.genomeId == child.genomeId), Is.True);
            Assert.That(descendants.Exists(x => x.genomeId == grandchild.genomeId), Is.True);
        }

        private static SimulationHistory BuildHistory(out CreatureGenome founder, out CreatureGenome child, out CreatureGenome grandchild)
        {
            founder = CreatureGenome.CreateFounder(new System.Random(1), 1, "founder");
            CreatureGenome mate = CreatureGenome.CreateFounder(new System.Random(2), 1, "mate");
            child = CreatureGenome.Crossover(founder, mate, new System.Random(3), 2, "child");
            grandchild = CreatureGenome.Crossover(child, founder, new System.Random(4), 3, "grandchild");

            var history = new SimulationHistory();
            history.Record(new GenerationReport { generation = 1, population = 2, bestFitness = 2.5f, averageFitness = 1.2f, bestGenomeId = founder.genomeId });
            history.Record(new GenerationReport { generation = 2, population = 1, bestFitness = 4.5f, averageFitness = 4.5f, bestGenomeId = child.genomeId });
            history.RecordEvent(EvolutionEventType.MajorMorphology, 2, child.genomeId, founder.genomeId, "test event", 1.5f);
            history.RecordIndividuals(new[]
            {
                new CreatureEvaluationResult(founder, 2.5f, 1f, 8f, 2f, 1, string.Empty, false),
                new CreatureEvaluationResult(child, 4.5f, 3f, 10f, 1f, 1, string.Empty, true),
                new CreatureEvaluationResult(grandchild, 5.5f, 5f, 12f, 0.5f, 0, string.Empty, true)
            });
            return history;
        }
    }
}
