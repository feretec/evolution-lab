using NUnit.Framework;
using UnityEngine;

namespace EvolutionLab.Tests
{
    public sealed class GenomeDistanceContractTests
    {
        // Schema 6 descriptor/hash contract coverage.
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

        [Test]
        public void JointAxesAndAngularLimits_AffectDistanceAndContentHash()
        {
            CreatureGenome source = CreatureGenome.CreateFounder(new System.Random(31), 1, "axis-source");
            CreatureGenome changed = source.Clone();
            BodyPartGene part = changed.bodyParts[0];
            part.jointAxis = new UnityEngine.Vector3(0f, 1f, 0f);
            part.secondaryAxis = new UnityEngine.Vector3(1f, 0f, 0f);
            part.angularYLimit = 42f;
            part.angularZLimit = 37f;
            changed.bodyParts[0] = part;

            Assert.That(GenomeDistance.Between(GenomeDistance.Describe(source), GenomeDistance.Describe(changed)), Is.GreaterThan(0f));
            Assert.That(GenomeDistance.ContentHash(changed), Is.Not.EqualTo(GenomeDistance.ContentHash(source)));
            Assert.That(GenomeDistance.StableSignature(changed), Is.Not.EqualTo(GenomeDistance.StableSignature(source)));
        }

        [Test]
        public void SensorAndMouthOrgans_AffectDistanceAndContentHash()
        {
            CreatureGenome source = CreatureGenome.CreateFounder(new System.Random(32), 1, "organ-source");
            CreatureGenome changed = source.Clone();
            SensorGene sensor = changed.sensors[0];
            sensor.fieldOfView = 47f;
            sensor.sensitivity = 2.2f;
            changed.sensors[0] = sensor;
            changed.mouth.reach = 3.2f;
            changed.mouth.efficiency = 1.45f;

            Assert.That(GenomeDistance.Between(GenomeDistance.Describe(source), GenomeDistance.Describe(changed)), Is.GreaterThan(0f));
            Assert.That(GenomeDistance.ContentHash(changed), Is.Not.EqualTo(GenomeDistance.ContentHash(source)));
            Assert.That(GenomeDistance.StableSignature(changed), Is.Not.EqualTo(GenomeDistance.StableSignature(source)));
        }

        [Test]
        public void MaximumSensorTopology_HasIndependentDescriptorStorage()
        {
            CreatureGenome source = CreatureGenome.CreateFounder(new System.Random(35), 1, "sensor-source");
            while (source.sensors.Count < CreatureGenome.MaxSensors)
            {
                source.sensors.Add(SensorGene.CreateDefault());
            }
            source.Repair();
            CreatureGenome sensorChanged = source.Clone();
            SensorGene lastSensor = sensorChanged.sensors[CreatureGenome.MaxSensors - 1];
            lastSensor.sensitivity = 2.75f;
            sensorChanged.sensors[CreatureGenome.MaxSensors - 1] = lastSensor;
            CreatureGenome mouthChanged = source.Clone();
            mouthChanged.mouth.bodyPartIndex = Mathf.Min(1, mouthChanged.bodyParts.Count - 1);
            mouthChanged.mouth.efficiency = 1.75f;

            Assert.DoesNotThrow(() => GenomeDistance.Describe(source));
            Assert.That(GenomeDistance.Between(GenomeDistance.Describe(source), GenomeDistance.Describe(sensorChanged)), Is.GreaterThan(0f));
            Assert.That(GenomeDistance.Between(GenomeDistance.Describe(source), GenomeDistance.Describe(mouthChanged)), Is.GreaterThan(0f));
            Assert.That(GenomeDistance.StableSignature(sensorChanged), Is.Not.EqualTo(GenomeDistance.StableSignature(mouthChanged)));
        }

        [Test]
        public void TopologyChange_AffectsDistanceAndContentHash()
        {
            CreatureGenome source = CreatureGenome.CreateFounder(new System.Random(33), 1, "topology-source");
            CreatureGenome changed = source.Clone();
            changed.bodyParts.Add(BodyPartGene.CreateRoot(0.9f, 0.24f, 0.5f));
            changed.sensors.Add(SensorGene.CreateDefault());

            Assert.That(GenomeDistance.Between(GenomeDistance.Describe(source), GenomeDistance.Describe(changed)), Is.GreaterThan(0f));
            Assert.That(GenomeDistance.ContentHash(changed), Is.Not.EqualTo(GenomeDistance.ContentHash(source)));
            Assert.That(GenomeDistance.StableSignature(changed), Is.Not.EqualTo(GenomeDistance.StableSignature(source)));
        }

        [Test]
        public void LearningRuleChange_AffectsDistanceAndContentHashWithoutChangingCandidateBucket()
        {
            CreatureGenome source = CreatureGenome.CreateFounder(new System.Random(34), 1, "learning-source");
            CreatureGenome changed = source.Clone();
            changed.brain.activeHiddenCount = Mathf.Max(2, source.brain.activeHiddenCount - 1);
            changed.brain.learning.learningRate = 0.071f;
            changed.brain.learning.eligibilityDecay = 0.61f;
            changed.brain.learning.rewardBaselineRate = 0.19f;
            changed.brain.learning.plasticityDecay = 0.008f;

            GenomeDistance.Descriptor first = GenomeDistance.Describe(source);
            GenomeDistance.Descriptor second = GenomeDistance.Describe(changed);
            Assert.That(GenomeDistance.Between(first, second), Is.GreaterThan(0f));
            Assert.That(GenomeDistance.ContentHash(changed), Is.Not.EqualTo(GenomeDistance.ContentHash(source)));
            Assert.That(GenomeDistance.StableSignature(changed), Is.Not.EqualTo(GenomeDistance.StableSignature(source)));
            Assert.That(GenomeDistance.CandidateBucket(second), Is.EqualTo(GenomeDistance.CandidateBucket(first)));
        }
    }
}
