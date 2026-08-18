using NUnit.Framework;
using UnityEngine;

namespace EvolutionLab.Tests
{
    public sealed class WorldSnapshotContractTests
    {
        [Test]
        public void Schema3JsonRoundTrip_PreservesEnvironmentCreatureAndRuntimeBrainState()
        {
            var source = new WorldSnapshotArchive { schemaVersion = 3, generation = 27 };
            source.resources.Add(new WorldResourceSnapshot
            {
                index = 4,
                hasTransform = true,
                position = new Vector3(11f, 0.2f, -7f),
                rotation = Quaternion.Euler(0f, 35f, 0f),
                currentEnergy = 12.5f,
                respawnRemaining = 3.25f
            });
            source.movableFeatures.Add(new WorldRigidbodySnapshot
            {
                index = 2,
                position = new Vector3(1f, 2f, 3f),
                rotation = Quaternion.Euler(10f, 20f, 30f),
                linearVelocity = new Vector3(4f, 5f, 6f),
                angularVelocity = new Vector3(7f, 8f, 9f)
            });
            var creature = new WorldCreatureSnapshot { genomeId = "c", hasFullRuntimeState = true };
            creature.bodyParts.Add(new WorldRigidbodySnapshot { index = 1, position = new Vector3(8f, 9f, 10f) });
            creature.brain.hasState = true;
            creature.brain.shortTermMemory = new[] { 0.2f, 0.4f };
            creature.brain.fastInputHiddenWeights = new[] { -0.75f, 0.5f };
            creature.brain.hiddenEligibility = new[] { 0.9f };
            source.creatures.Add(creature);

            WorldSnapshotArchive restored = JsonUtility.FromJson<WorldSnapshotArchive>(JsonUtility.ToJson(source));

            Assert.That(restored.schemaVersion, Is.EqualTo(3));
            Assert.That(restored.resources[0].currentEnergy, Is.EqualTo(12.5f));
            Assert.That(restored.resources[0].respawnRemaining, Is.EqualTo(3.25f));
            Assert.That(restored.resources[0].hasTransform, Is.True);
            Assert.That(restored.resources[0].position, Is.EqualTo(new Vector3(11f, 0.2f, -7f)));
            Assert.That(restored.movableFeatures[0].linearVelocity, Is.EqualTo(new Vector3(4f, 5f, 6f)));
            Assert.That(restored.creatures[0].bodyParts[0].position, Is.EqualTo(new Vector3(8f, 9f, 10f)));
            Assert.That(restored.creatures[0].brain.shortTermMemory[1], Is.EqualTo(0.4f));
            Assert.That(restored.creatures[0].brain.fastInputHiddenWeights[0], Is.EqualTo(-0.75f));
            Assert.That(restored.creatures[0].brain.hiddenEligibility[0], Is.EqualTo(0.9f));
        }
    }
}
