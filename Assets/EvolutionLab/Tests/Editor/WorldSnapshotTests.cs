using NUnit.Framework;
using UnityEngine;

namespace EvolutionLab.Tests
{
    public sealed class WorldSnapshotTests
    {
        [Test]
        public void Schema3RoundTrip_PreservesBodyAndBrainRuntimeState()
        {
            WorldSnapshotArchive source = new WorldSnapshotArchive
            {
                generation = 42,
                schemaVersion = 3
            };
            WorldCreatureSnapshot creature = new WorldCreatureSnapshot
            {
                genomeId = "g-42",
                hasFullRuntimeState = true,
                energy = 73.5f,
                age = 12.25f,
                alive = true,
                evaluationActive = true
            };
            creature.bodyParts.Add(new WorldRigidbodySnapshot
            {
                index = 2,
                position = new Vector3(1f, 2f, 3f),
                rotation = Quaternion.Euler(10f, 20f, 30f),
                linearVelocity = new Vector3(4f, 5f, 6f),
                angularVelocity = new Vector3(7f, 8f, 9f)
            });
            creature.brain.hasState = true;
            creature.brain.fastInputHiddenWeights = new[] { 0.25f, -0.5f };
            creature.brain.hiddenEligibility = new[] { 1.25f };
            source.creatures.Add(creature);

            WorldSnapshotArchive restored = JsonUtility.FromJson<WorldSnapshotArchive>(
                JsonUtility.ToJson(source));

            Assert.That(restored.schemaVersion, Is.EqualTo(3));
            Assert.That(restored.creatures.Count, Is.EqualTo(1));
            Assert.That(restored.creatures[0].hasFullRuntimeState, Is.True);
            Assert.That(restored.creatures[0].bodyParts[0].index, Is.EqualTo(2));
            Assert.That(restored.creatures[0].bodyParts[0].linearVelocity, Is.EqualTo(new Vector3(4f, 5f, 6f)));
            Assert.That(restored.creatures[0].brain.hasState, Is.True);
            Assert.That(restored.creatures[0].brain.fastInputHiddenWeights[1], Is.EqualTo(-0.5f));
            Assert.That(restored.creatures[0].brain.hiddenEligibility[0], Is.EqualTo(1.25f));
        }

        [Test]
        public void LegacySchema2Archive_LeavesFullStateMarkerDisabled()
        {
            WorldSnapshotArchive archive = JsonUtility.FromJson<WorldSnapshotArchive>(
                "{\"schemaVersion\":2,\"generation\":8,\"population\":[],\"creatures\":[{\"genomeId\":\"old\",\"energy\":12}]}");

            Assert.That(archive.schemaVersion, Is.EqualTo(2));
            Assert.That(archive.creatures.Count, Is.EqualTo(1));
            Assert.That(archive.creatures[0].hasFullRuntimeState, Is.False);
            Assert.That(archive.creatures[0].brain == null || !archive.creatures[0].brain.hasState, Is.True);
        }

        [Test]
        public void BrainRuntimeApi_RestoresLearningStateAndRepairsMalformedArrays()
        {
            BrainGene gene = BrainGene.CreateRandom(new System.Random(91));
            Brain source = new Brain(gene);
            float[] inputs = new float[BrainGene.InputCount];
            source.Evaluate(inputs);
            source.ApplyHomeostaticLearning(0.8f, 0.02f);

            BrainRuntimeSnapshot snapshot = new BrainRuntimeSnapshot();
            source.CaptureRuntimeState(snapshot);
            Assert.That(snapshot.hasState, Is.True);

            snapshot.fastInputHiddenWeights = new[] { float.NaN, 0.25f };
            snapshot.hiddenEligibility = new[] { 1f, 2f, 3f, 4f, 5f };
            Brain restored = new Brain(gene);
            Assert.DoesNotThrow(() => restored.RestoreRuntimeState(snapshot));
            Assert.That(restored.AdaptationMagnitude, Is.GreaterThanOrEqualTo(0f));
            Assert.That(restored.Evaluate(inputs), Has.Length.EqualTo(BrainGene.MaxOutputCount));
        }
    }
}
