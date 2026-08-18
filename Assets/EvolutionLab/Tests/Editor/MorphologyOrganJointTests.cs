using System;
using NUnit.Framework;
using UnityEngine;

namespace EvolutionLab.Tests
{
    public sealed class MorphologyOrganJointTests
    {
        [Test]
        public void Repair_AddsSafeMultiAxisAndOrganDefaults()
        {
            var genome = new CreatureGenome
            {
                schemaVersion = CreatureGenome.CurrentSchemaVersion,
                bodyParts = new System.Collections.Generic.List<BodyPartGene>
                {
                    BodyPartGene.CreateRoot(1f, 0.3f, 1f),
                    new BodyPartGene
                    {
                        parentIndex = 0,
                        length = 0.7f,
                        thickness = 0.2f,
                        mass = 0.5f,
                        jointLimit = 90f,
                        driveStrength = 1f,
                        jointAxis = Vector3.zero,
                        secondaryAxis = Vector3.right,
                        angularYLimit = 75f,
                        angularZLimit = 55f
                    },
                },
                sensors = new System.Collections.Generic.List<SensorGene>(),
                mouth = new MouthGene()
            };

            genome.Repair();

            BodyPartGene joint = genome.bodyParts[1];
            Assert.That(joint.jointAxis.sqrMagnitude, Is.GreaterThan(0.99f));
            Assert.That(Vector3.Dot(joint.jointAxis, joint.secondaryAxis), Is.EqualTo(0f).Within(0.001f));
            Assert.That(joint.angularYLimit, Is.EqualTo(75f).Within(0.001f));
            Assert.That(joint.angularZLimit, Is.EqualTo(55f).Within(0.001f));
            Assert.That(genome.sensors.Count, Is.EqualTo(1));
            Assert.That(genome.mouth.reach, Is.InRange(0.25f, 4f));
        }

        [Test]
        public void LegacyGenome_RepairsToSingleAxisCompatibility()
        {
            CreatureGenome legacy = CreatureGenome.CreateFounder(new System.Random(3), 1, "legacy");
            legacy.schemaVersion = 4;
            for (int i = 1; i < legacy.bodyParts.Count; i++)
            {
                BodyPartGene part = legacy.bodyParts[i];
                part.angularYLimit = 90f;
                part.angularZLimit = 90f;
                legacy.bodyParts[i] = part;
            }

            legacy.Repair();

            for (int i = 1; i < legacy.bodyParts.Count; i++)
            {
                Assert.That(legacy.bodyParts[i].angularYLimit, Is.EqualTo(0f));
                Assert.That(legacy.bodyParts[i].angularZLimit, Is.EqualTo(0f));
            }
            Assert.That(legacy.mouth.reach, Is.EqualTo(MouthGene.CreateDefault().reach));
            Assert.That(legacy.mouth.efficiency, Is.EqualTo(MouthGene.CreateDefault().efficiency));
        }

        [Test]
        public void CloneAndCrossover_PreserveSensorAndMouthDataWithoutSharingLists()
        {
            CreatureGenome first = CreatureGenome.CreateFounder(new System.Random(11), 1, "a");
            CreatureGenome second = CreatureGenome.CreateFounder(new System.Random(12), 1, "b");
            CreatureGenome clone = first.Clone();
            CreatureGenome child = CreatureGenome.Crossover(first, second, new System.Random(13), 2, "c");

            Assert.That(clone.sensors, Is.Not.SameAs(first.sensors));
            Assert.That(clone.mouth.reach, Is.EqualTo(first.mouth.reach).Within(0.0001f));
            Assert.That(child.sensors.Count, Is.InRange(1, CreatureGenome.MaxSensors));
            Assert.That(child.mouth.efficiency, Is.InRange(0.05f, 2f));
        }
    }
}
