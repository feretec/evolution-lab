using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionLab.Tests
{
    public sealed class CreatureGenomeTests
    {
        [Test]
        public void Clone_IsDeepCopyAndPreservesGenomeData()
        {
            CreatureGenome original = CreateGenome("original", 4);
            CreatureGenome clone = original.Clone();

            Assert.That(clone, Is.Not.SameAs(original));
            Assert.That(clone.genomeId, Is.EqualTo(original.genomeId));
            Assert.That(clone.bodyParts.Count, Is.EqualTo(original.bodyParts.Count));
            Assert.That(clone.brain, Is.Not.SameAs(original.brain));
            Assert.That(clone.brain.inputHiddenWeights, Is.Not.SameAs(original.brain.inputHiddenWeights));
            Assert.That(clone.ecology, Is.Not.SameAs(original.ecology));

            float originalLength = original.bodyParts[1].length;
            float originalWeight = original.brain.inputHiddenWeights[0];
            clone.bodyParts[1] = WithLength(clone.bodyParts[1], originalLength + 0.2f);
            clone.brain.inputHiddenWeights[0] = originalWeight + 0.2f;

            Assert.That(original.bodyParts[1].length, Is.EqualTo(originalLength));
            Assert.That(original.brain.inputHiddenWeights[0], Is.EqualTo(originalWeight));
        }

        [Test]
        public void Repair_RestoresShapeBoundsAndParentOrdering()
        {
            var genome = new CreatureGenome
            {
                schemaVersion = -10,
                bodyParts = new System.Collections.Generic.List<BodyPartGene>(),
                brain = null,
                ecology = null
            };
            genome.Repair();

            Assert.That(genome.schemaVersion, Is.EqualTo(CreatureGenome.CurrentSchemaVersion));
            Assert.That(genome.bodyParts.Count, Is.GreaterThanOrEqualTo(CreatureGenome.MinBodyParts));
            Assert.That(genome.bodyParts.Count, Is.LessThanOrEqualTo(CreatureGenome.MaxBodyParts));
            Assert.That(genome.brain.inputHiddenWeights.Length, Is.EqualTo(BrainGene.InputCount * BrainGene.HiddenCount));
            Assert.That(genome.brain.hiddenOutputWeights.Length, Is.EqualTo(BrainGene.HiddenCount * BrainGene.MaxOutputCount));
            Assert.That(genome.ecology.sensorRange, Is.InRange(2f, 20f));

            for (int i = 0; i < genome.bodyParts.Count; i++)
            {
                BodyPartGene part = genome.bodyParts[i];
                if (i == 0)
                {
                    Assert.That(part.parentIndex, Is.EqualTo(-1));
                }
                else
                {
                    Assert.That(part.parentIndex, Is.InRange(0, i - 1));
                }
                Assert.That(part.length, Is.InRange(0.3f, 1.8f));
                Assert.That(part.thickness, Is.InRange(0.08f, 0.65f));
                Assert.That(part.mass, Is.InRange(0.12f, 2.8f));
            }
        }

        [Test]
        public void Crossover_ProducesValidChildWithBothParentLinks()
        {
            CreatureGenome first = CreateGenome("first", 3);
            CreatureGenome second = CreateGenome("second", 5);

            CreatureGenome child = CreatureGenome.Crossover(first, second, new System.Random(17), 6, "child");

            Assert.That(child.genomeId, Is.EqualTo("child"));
            Assert.That(child.parentId, Is.EqualTo("first"));
            Assert.That(child.secondaryParentId, Is.EqualTo("second"));
            Assert.That(child.generation, Is.EqualTo(6));
            AssertValidGenome(child);
        }

        [Test]
        public void Mutation_PreservesGenomeInvariants()
        {
            CreatureGenome genome = CreateGenome("mutant", 4);
            genome.mutationRate = 1f;
            genome.Mutate(new System.Random(1234));

            AssertValidGenome(genome);
            Assert.That(genome.mutationRate, Is.InRange(0.04f, 0.38f));
        }

        private static CreatureGenome CreateGenome(string id, int parts)
        {
            CreatureGenome genome = CreatureGenome.CreateFounder(new System.Random(id.GetHashCode()), 1, id);
            while (genome.bodyParts.Count < parts)
            {
                BodyPartGene parent = genome.bodyParts[genome.bodyParts.Count - 1];
                genome.bodyParts.Add(new BodyPartGene
                {
                    parentIndex = genome.bodyParts.Count - 1,
                    localOffset = Vector3.right * 0.5f,
                    localEulerAngles = Vector3.zero,
                    length = parent.length,
                    thickness = parent.thickness,
                    mass = parent.mass,
                    jointLimit = parent.jointLimit,
                    driveStrength = parent.driveStrength
                });
            }
            genome.Repair();
            return genome;
        }

        private static BodyPartGene WithLength(BodyPartGene part, float length)
        {
            part.length = length;
            return part;
        }

        private static void AssertValidGenome(CreatureGenome genome)
        {
            Assert.That(genome, Is.Not.Null);
            Assert.That(genome.bodyParts.Count, Is.InRange(CreatureGenome.MinBodyParts, CreatureGenome.MaxBodyParts));
            Assert.That(genome.brain.inputHiddenWeights.Length, Is.EqualTo(BrainGene.InputCount * BrainGene.HiddenCount));
            Assert.That(genome.brain.hiddenBiases.Length, Is.EqualTo(BrainGene.HiddenCount));
            Assert.That(genome.brain.hiddenOutputWeights.Length, Is.EqualTo(BrainGene.HiddenCount * BrainGene.MaxOutputCount));
            Assert.That(genome.brain.outputBiases.Length, Is.EqualTo(BrainGene.MaxOutputCount));
            for (int i = 0; i < genome.bodyParts.Count; i++)
            {
                if (i == 0)
                {
                    Assert.That(genome.bodyParts[i].parentIndex, Is.EqualTo(-1));
                }
                else
                {
                    Assert.That(genome.bodyParts[i].parentIndex, Is.InRange(0, i - 1));
                }
            }
        }
    }
}
