using NUnit.Framework;

namespace EvolutionLab.Tests
{
    public sealed class BrainTopologyTests
    {
        [Test]
        public void LegacyBrainGene_RepairsToFormerEightNeuronTopology()
        {
            var gene = new BrainGene { activeHiddenCount = 0 };

            gene.EnsureShape();

            Assert.That(gene.activeHiddenCount, Is.EqualTo(BrainGene.HiddenCount));
        }

        [Test]
        public void ActiveHiddenCount_IsInheritedClampedAndUsedByRuntimeBrain()
        {
            BrainGene gene = BrainGene.CreateRandom(new System.Random(51));
            gene.activeHiddenCount = 3;
            BrainGene clone = gene.Clone();
            Brain brain = new Brain(clone);

            Assert.That(clone.activeHiddenCount, Is.EqualTo(3));
            Assert.That(brain.ActiveHiddenCount, Is.EqualTo(3));

            clone.activeHiddenCount = 999;
            clone.EnsureShape();
            Assert.That(clone.activeHiddenCount, Is.EqualTo(BrainGene.HiddenCount));
        }

        [Test]
        public void Mutation_PreservesEvolvableTopologyBounds()
        {
            BrainGene gene = BrainGene.CreateRandom(new System.Random(73));
            var random = new System.Random(74);
            for (int i = 0; i < 200; i++) gene.Mutate(random, 1f);

            Assert.That(gene.activeHiddenCount, Is.InRange(2, BrainGene.HiddenCount));
        }
    }
}
