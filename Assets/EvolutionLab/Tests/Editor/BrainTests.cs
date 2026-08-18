using NUnit.Framework;

namespace EvolutionLab.Tests
{
    public sealed class BrainTests
    {
        [Test]
        public void Evaluate_IsDeterministicForSameGeneAndInputs()
        {
            BrainGene gene = BrainGene.CreateRandom(new System.Random(42));
            float[] inputs = new float[BrainGene.InputCount];
            for (int i = 0; i < inputs.Length; i++) inputs[i] = (i - 7) * 0.13f;

            // A fresh controller is the "before learning" reference. A single
            // Brain intentionally carries traces and memory between steps.
            var firstBrain = new Brain(gene);
            var secondBrain = new Brain(gene);
            float[] first = firstBrain.Evaluate(inputs);
            float[] second = secondBrain.Evaluate(inputs);

            Assert.That(first.Length, Is.EqualTo(BrainGene.MaxOutputCount));
            Assert.That(second, Is.Not.SameAs(first));
            for (int i = 0; i < first.Length; i++)
            {
                Assert.That(first[i], Is.EqualTo(second[i]));
                Assert.That(float.IsNaN(first[i]) || float.IsInfinity(first[i]), Is.False);
                Assert.That(first[i], Is.InRange(-1f, 1f));
            }
        }

        [Test]
        public void Evaluate_HandlesNullAndShortInputsWithFiniteOutputs()
        {
            var brain = new Brain(BrainGene.CreateRandom(new System.Random(7)));
            float[] outputs = brain.Evaluate(null);

            Assert.That(outputs.Length, Is.EqualTo(brain.OutputCount));
            for (int i = 0; i < outputs.Length; i++)
            {
                Assert.That(float.IsNaN(outputs[i]) || float.IsInfinity(outputs[i]), Is.False);
            }
        }
    }
}
