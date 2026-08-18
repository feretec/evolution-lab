using System;
using NUnit.Framework;

namespace EvolutionLab.Tests
{
    public sealed class LifetimeLearningTests
    {
        [Test]
        public void SameInputBeforeLearning_IsDeterministicAcrossFreshBrains()
        {
            BrainGene gene = CreateLearningGene();
            float[] inputs = CreateInputs(0.35f);

            float[] first = new Brain(gene).Evaluate(inputs);
            float[] second = new Brain(gene).Evaluate(inputs);

            AssertOutputsEqual(first, second);
        }

        [Test]
        public void PositiveAndNegativeHomeostaticSignals_AdaptFastWeights()
        {
            BrainGene gene = CreateLearningGene();
            float[] inputs = CreateInputs(0.6f);
            Brain positive = new Brain(gene);
            Brain negative = new Brain(gene);

            positive.Evaluate(inputs);
            negative.Evaluate(inputs);
            positive.ApplyHomeostaticLearning(1f, 0.02f);
            negative.ApplyHomeostaticLearning(-1f, 0.02f);

            Assert.That(positive.AdaptationMagnitude, Is.GreaterThan(0f));
            Assert.That(negative.AdaptationMagnitude, Is.GreaterThan(0f));
            Assert.That(positive.LastHomeostaticSignal, Is.EqualTo(1f));
            Assert.That(negative.LastHomeostaticSignal, Is.EqualTo(-1f));

            float[] positiveOutput = positive.Evaluate(inputs);
            float[] negativeOutput = negative.Evaluate(inputs);
            Assert.That(OutputsDiffer(positiveOutput, negativeOutput), Is.True,
                "Opposite homeostatic signals should produce opposite-signed fast-weight updates.");
        }

        [Test]
        public void ResetRuntimeState_RemovesAcquiredAdaptation()
        {
            BrainGene gene = CreateLearningGene();
            float[] inputs = CreateInputs(0.45f);
            Brain brain = new Brain(gene);
            Brain reference = new Brain(gene);

            brain.Evaluate(inputs);
            brain.ApplyHomeostaticLearning(1f, 0.02f);
            Assert.That(brain.AdaptationMagnitude, Is.GreaterThan(0f));

            brain.ResetRuntimeState();
            float[] resetOutput = brain.Evaluate(inputs);
            float[] referenceOutput = reference.Evaluate(inputs);

            Assert.That(brain.AdaptationMagnitude, Is.EqualTo(0f));
            Assert.That(brain.LastHomeostaticSignal, Is.EqualTo(0f));
            AssertOutputsEqual(referenceOutput, resetOutput);
        }

        [Test]
        public void RepeatedSignal_BuildsAHomeostaticBaselineThatIsSnapshotSafe()
        {
            BrainGene gene = CreateLearningGene();
            Brain brain = new Brain(gene);
            brain.Evaluate(CreateInputs(0.3f));
            for (int i = 0; i < 100; i++) brain.ApplyHomeostaticLearning(0.8f, 0.02f);

            var snapshot = new BrainRuntimeSnapshot();
            brain.CaptureRuntimeState(snapshot);

            Assert.That(snapshot.rewardBaseline, Is.GreaterThan(0.5f));
            Assert.That(snapshot.rewardBaseline, Is.LessThanOrEqualTo(0.8f));
            Brain restored = new Brain(gene);
            restored.RestoreRuntimeState(snapshot);
            var restoredSnapshot = new BrainRuntimeSnapshot();
            restored.CaptureRuntimeState(restoredSnapshot);
            Assert.That(restoredSnapshot.rewardBaseline, Is.EqualTo(snapshot.rewardBaseline).Within(0.000001f));
        }

        [Test]
        public void Learning_DoesNotModifyInheritedBaseWeightArrays()
        {
            BrainGene gene = CreateLearningGene();
            float[] inputHiddenBefore = (float[])gene.inputHiddenWeights.Clone();
            float[] hiddenBiasBefore = (float[])gene.hiddenBiases.Clone();
            float[] hiddenOutputBefore = (float[])gene.hiddenOutputWeights.Clone();
            float[] outputBiasBefore = (float[])gene.outputBiases.Clone();

            Brain brain = new Brain(gene);
            brain.Evaluate(CreateInputs(0.8f));
            brain.ApplyHomeostaticLearning(1f, 0.02f);
            brain.ApplyHomeostaticLearning(-1f, 0.02f);

            CollectionAssert.AreEqual(inputHiddenBefore, gene.inputHiddenWeights);
            CollectionAssert.AreEqual(hiddenBiasBefore, gene.hiddenBiases);
            CollectionAssert.AreEqual(hiddenOutputBefore, gene.hiddenOutputWeights);
            CollectionAssert.AreEqual(outputBiasBefore, gene.outputBiases);
        }

        [Test]
        public void BrainAndLearningSignal_AreFiniteForNonFiniteInputsAndFeedback()
        {
            BrainGene gene = CreateLearningGene();
            gene.inputHiddenWeights[0] = float.NaN;
            gene.hiddenOutputWeights[0] = float.PositiveInfinity;
            gene.learning.learningRate = float.NaN;
            gene.learning.fastWeightLimit = float.NegativeInfinity;
            Brain brain = new Brain(gene);

            float[] inputs = CreateInputs(float.NaN);
            inputs[1] = float.PositiveInfinity;
            float[] outputs = brain.Evaluate(inputs, float.NaN);
            brain.AccumulateHomeostaticFeedback(float.NaN, float.PositiveInfinity, float.NegativeInfinity, true);
            brain.AccumulateControlCost(float.NaN);
            brain.ApplyPendingLearning(float.PositiveInfinity);
            brain.ApplyHomeostaticLearning(float.NegativeInfinity, float.NaN);

            AssertFinite(outputs);
            Assert.That(IsFinite(brain.AdaptationMagnitude), Is.True);
            Assert.That(IsFinite(brain.LastHomeostaticSignal), Is.True);
            // Invalid inherited values are tolerated at evaluation time and
            // remain untouched: repair of serialized arrays is not a side
            // effect of lifetime learning.
            Assert.That(float.IsNaN(gene.inputHiddenWeights[0]), Is.True);
            Assert.That(float.IsPositiveInfinity(gene.hiddenOutputWeights[0]), Is.True);
        }

        [Test]
        public void LearningGene_CloneCrossoverMutationAndRepair_PreserveInvariants()
        {
            LifetimeLearningGene first = CreateLearningGene().learning;
            LifetimeLearningGene clone = first.Clone();
            Assert.That(clone, Is.Not.SameAs(first));
            AssertLearningGeneValid(clone);

            LifetimeLearningGene second = first.Clone();
            second.learningRate = 0.06f;
            LifetimeLearningGene child = LifetimeLearningGene.Crossover(first, second, new Random(4));
            Assert.That(child.learningRate, Is.EqualTo((first.learningRate + second.learningRate) * 0.5f));
            AssertLearningGeneValid(child);

            child.Mutate(new Random(7), 1f);
            AssertLearningGeneValid(child);

            var oldSchema = new LifetimeLearningGene
            {
                schemaVersion = 0,
                enabled = false,
                learningRate = float.NaN,
                eligibilityDecay = float.PositiveInfinity,
                memoryRetention = float.NegativeInfinity,
                fastWeightLimit = float.NaN,
                energyDeltaScale = float.NaN,
                damageScale = float.NaN,
                controlCostScale = float.NaN,
                survivalBias = float.NaN,
                rewardBaselineRate = float.NaN,
                plasticityDecay = float.NaN
            };
            oldSchema.Repair();
            Assert.That(oldSchema.schemaVersion, Is.EqualTo(LifetimeLearningGene.CurrentSchemaVersion));
            Assert.That(oldSchema.enabled, Is.True);
            AssertLearningGeneValid(oldSchema);

            var schemaOne = new LifetimeLearningGene
            {
                schemaVersion = 1,
                enabled = false,
                learningRate = 0.02f,
                eligibilityDecay = 0.85f,
                memoryRetention = 0.7f,
                fastWeightLimit = 0.6f,
                energyDeltaScale = 4f,
                damageScale = 4f,
                controlCostScale = 0.25f,
                survivalBias = 0.01f,
                rewardBaselineRate = 0f,
                plasticityDecay = 0f
            };
            schemaOne.Repair();
            Assert.That(schemaOne.schemaVersion, Is.EqualTo(2));
            Assert.That(schemaOne.enabled, Is.False);
            Assert.That(schemaOne.rewardBaselineRate, Is.EqualTo(0.035f));
            Assert.That(schemaOne.plasticityDecay, Is.EqualTo(0.0006f));
        }

        private static BrainGene CreateLearningGene()
        {
            BrainGene gene = BrainGene.CreateRandom(new Random(123));
            gene.learning.enabled = true;
            gene.learning.learningRate = 0.08f;
            gene.learning.fastWeightLimit = 1.5f;
            gene.learning.eligibilityDecay = 0.9f;
            gene.learning.memoryRetention = 0.5f;
            gene.learning.Repair();
            return gene;
        }

        private static float[] CreateInputs(float value)
        {
            var inputs = new float[BrainGene.InputCount];
            for (int i = 0; i < inputs.Length; i++) inputs[i] = value;
            return inputs;
        }

        private static void AssertOutputsEqual(float[] expected, float[] actual)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]).Within(0.000001f));
            }
        }

        private static bool OutputsDiffer(float[] first, float[] second)
        {
            for (int i = 0; i < first.Length; i++)
            {
                if (Math.Abs(first[i] - second[i]) > 0.000001f) return true;
            }
            return false;
        }

        private static void AssertFinite(float[] values)
        {
            for (int i = 0; i < values.Length; i++) Assert.That(IsFinite(values[i]), Is.True);
        }

        private static void AssertLearningGeneValid(LifetimeLearningGene gene)
        {
            Assert.That(gene.schemaVersion, Is.EqualTo(LifetimeLearningGene.CurrentSchemaVersion));
            Assert.That(gene.learningRate, Is.InRange(0.001f, 0.08f));
            Assert.That(gene.eligibilityDecay, Is.InRange(0.55f, 0.995f));
            Assert.That(gene.memoryRetention, Is.InRange(0.25f, 0.995f));
            Assert.That(gene.fastWeightLimit, Is.InRange(0.1f, 1.5f));
            Assert.That(gene.energyDeltaScale, Is.InRange(0.25f, 12f));
            Assert.That(gene.damageScale, Is.InRange(0.25f, 12f));
            Assert.That(gene.controlCostScale, Is.InRange(0f, 2f));
            Assert.That(gene.survivalBias, Is.InRange(0f, 0.08f));
            Assert.That(gene.rewardBaselineRate, Is.InRange(0.001f, 0.25f));
            Assert.That(gene.plasticityDecay, Is.InRange(0f, 0.01f));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
