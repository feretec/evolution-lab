using NUnit.Framework;

namespace EvolutionLab.Tests
{
    public sealed class LearningTelemetryTests
    {
        [Test]
        public void CreatureMetrics_AreAggregatedIntoGenerationAndIndividualHistory()
        {
            CreatureGenome first = CreatureGenome.CreateFounder(new System.Random(31), 1, "learning-a");
            CreatureGenome second = CreatureGenome.CreateFounder(new System.Random(32), 1, "learning-b");
            var firstResult = new CreatureEvaluationResult(first, 2f, 1f).WithLearningMetrics(true, 0.25f, 0.5f);
            var secondResult = new CreatureEvaluationResult(second, 1f, 0.5f).WithLearningMetrics(false, -0.1f, 0.1f);
            var history = new SimulationHistory();
            var engine = new EvolutionEngine(2, 99);
            engine.Initialize();

            history.RecordIndividuals(new[] { firstResult, secondResult });
            Assert.That(history.TryGetIndividual(first.genomeId, out IndividualHistoryRecord record), Is.True);
            Assert.That(record.learningMetricsAvailable, Is.True);
            Assert.That(record.lifetimeLearningEnabled, Is.True);
            Assert.That(record.learningAdaptationMagnitude, Is.EqualTo(0.5f).Within(0.0001f));

            var report = new GenerationReport { generation = 1, population = 2 };
            report.learning.Observe(true, 0.25f, 0.5f);
            report.learning.Observe(false, -0.1f, 0.1f);
            history.Record(report);
            string json = history.ToJson();
            var restored = new SimulationHistory();
            Assert.That(restored.TryLoadJson(json), Is.True);
            Assert.That(restored.Generations[0].learning.observedCount, Is.EqualTo(2));
            Assert.That(restored.Generations[0].learning.enabledRate, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(engine.History.Revision, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void NaturalHistory_ReportsOnlyAvailableLearningSamples()
        {
            var history = new SimulationHistory();
            CreatureGenome genome = CreatureGenome.CreateFounder(new System.Random(33), 1, "learning-c");
            var result = new CreatureEvaluationResult(genome, 3f, 2f).WithLearningMetrics(true, 0.4f, 0.75f);
            history.RecordIndividuals(new[] { result });

            NaturalHistoryCatalog catalog = history.NaturalHistory;
            Assert.That(catalog.Lineages[0].learning.observedCount, Is.EqualTo(1));
            Assert.That(catalog.Lineages[0].learning.averageAdaptation, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(catalog.Species[0].learning.enabledRate, Is.EqualTo(1f).Within(0.0001f));

            CreatureGenome unknown = CreatureGenome.CreateFounder(new System.Random(34), 1, "learning-d");
            history.RecordIndividuals(new[] { new CreatureEvaluationResult(unknown, 1f, 1f) });
            Assert.That(history.NaturalHistory.Lineages.Count, Is.EqualTo(2));
            Assert.That(history.NaturalHistory.Lineages[1].learning.observedCount, Is.EqualTo(0));
        }

        [Test]
        public void NaturalHistoryRevision_MakesRepeatedBuildAnO1CacheHit()
        {
            var history = new SimulationHistory();
            CreatureGenome genome = CreatureGenome.CreateFounder(new System.Random(35), 1, "revision-a");
            history.RecordIndividuals(new[] { new CreatureEvaluationResult(genome, 1f, 1f) });
            NaturalHistoryCatalog first = history.NaturalHistory;
            NaturalHistoryCatalog second = history.NaturalHistory;

            Assert.That(ReferenceEquals(first, second), Is.True);
        }
    }
}
