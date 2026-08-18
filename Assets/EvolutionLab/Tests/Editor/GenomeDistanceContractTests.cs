using NUnit.Framework;

namespace EvolutionLab.Tests
{
    public sealed class GenomeDistanceContractTests
    {
        [Test]
        public void Clone_HasZeroDistance()
        {
            CreatureGenome genome = CreatureGenome.CreateFounder(new System.Random(7), 1, "clone");

            float distance = GenomeDistance.Between(GenomeDistance.Describe(genome), GenomeDistance.Describe(genome.Clone()));

            Assert.That(distance, Is.EqualTo(0f).Within(0.000001f));
        }

        [Test]
        public void ClearlyDifferentGenome_HasGreaterDistanceThanSmallChange()
        {
            CreatureGenome source = CreatureGenome.CreateFounder(new System.Random(8), 1, "source");
            CreatureGenome near = source.Clone();
            BodyPartGene nearRoot = near.bodyParts[0];
            nearRoot.length += 0.02f;
            near.bodyParts[0] = nearRoot;
            CreatureGenome far = source.Clone();
            BodyPartGene farRoot = far.bodyParts[0];
            farRoot.length = 1.8f;
            far.bodyParts[0] = farRoot;
            far.bodyParts.Add(BodyPartGene.CreateRoot(1.8f, 0.65f, 2.8f));
            far.brain.inputHiddenWeights[0] = 3f;
            far.ecology.predationDrive = 1f;

            float nearDistance = GenomeDistance.Between(GenomeDistance.Describe(source), GenomeDistance.Describe(near));
            float farDistance = GenomeDistance.Between(GenomeDistance.Describe(source), GenomeDistance.Describe(far));

            Assert.That(farDistance, Is.GreaterThan(nearDistance));
        }

        [Test]
        public void Distance_IsSymmetric()
        {
            CreatureGenome first = CreatureGenome.CreateFounder(new System.Random(9), 1, "first");
            CreatureGenome second = CreatureGenome.CreateFounder(new System.Random(10), 1, "second");

            float forward = GenomeDistance.Between(GenomeDistance.Describe(first), GenomeDistance.Describe(second));
            float reverse = GenomeDistance.Between(GenomeDistance.Describe(second), GenomeDistance.Describe(first));

            Assert.That(forward, Is.EqualTo(reverse).Within(0.000001f));
        }

        [Test]
        public void NullAndOldPartialGenomes_AreSafe()
        {
            CreatureGenome oldPartial = new CreatureGenome { schemaVersion = 0, genomeId = "old", brain = new BrainGene() };
            oldPartial.bodyParts.Clear();
            oldPartial.brain.inputHiddenWeights = new[] { 1f };

            Assert.DoesNotThrow(() => GenomeDistance.Describe(null));
            Assert.DoesNotThrow(() => GenomeDistance.Describe(oldPartial));
            Assert.That(GenomeDistance.Between(GenomeDistance.Describe(null), GenomeDistance.Describe(oldPartial)), Is.EqualTo(1f));
            Assert.That(GenomeDistance.StableSignature(null), Is.EqualTo("missing"));
        }
    }
}
