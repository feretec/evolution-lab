using System;
using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLab
{
    [Serializable]
    public sealed class WorldCreatureSnapshot
    {
        public string genomeId = string.Empty;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public bool hasFullRuntimeState;
        public float energy;
        public float age;
        public int offspringCount;
        public int killCount;
        public float damageTaken;
        public float totalEnergyAcquired;
        public float reproductionCooldownRemaining;
        public float startX;
        public float bestX;
        public float brainClock;
        public bool alive = true;
        public bool evaluationActive = true;
        public string deathReason = string.Empty;
        public float interactionIntent;
        public float reproductionIntent;
        public float socialIntent;
        public float foragingIntent;
        public List<WorldRigidbodySnapshot> bodyParts = new List<WorldRigidbodySnapshot>();
        public BrainRuntimeSnapshot brain = new BrainRuntimeSnapshot();
    }

    /// <summary>
    /// Acquired controller state. Base weights remain in CreatureGenome; this
    /// object contains only the individual's lifetime plasticity state.
    /// </summary>
    [Serializable]
    public sealed class BrainRuntimeSnapshot
    {
        public bool hasState;
        public float[] hidden;
        public float[] outputs;
        public float[] lastInputs;
        public float[] inputEligibility;
        public float[] hiddenEligibility;
        public float[] shortTermMemory;
        public float[] fastInputHiddenWeights;
        public float[] fastHiddenOutputWeights;
        public bool hasActivation;
        public float pendingHomeostaticSignal;
        public float lastHomeostaticSignal;
        public float adaptationMagnitude;
    }

    [Serializable]
    public sealed class WorldResourceSnapshot
    {
        public int index;
        public bool hasTransform;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public float currentEnergy;
        public float respawnRemaining;
    }

    [Serializable]
    public sealed class WorldRigidbodySnapshot
    {
        public int index;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 linearVelocity;
        public Vector3 angularVelocity;
    }

    [Serializable]
    public sealed class WorldSnapshotArchive
    {
        public int schemaVersion = 4;
        public int randomSeed;
        public uint engineRandomState;
        public uint environmentResourceRandomState;
        public uint environmentFeatureRandomState;
        public int generation = 1;
        public float evaluationElapsed;
        public float simulationSpeed = 1f;
        public bool paused;
        public bool renderWorld = true;
        public string historyJson = string.Empty;
        public List<CreatureGenome> population = new List<CreatureGenome>();
        public List<WorldCreatureSnapshot> creatures = new List<WorldCreatureSnapshot>();
        public List<WorldResourceSnapshot> resources = new List<WorldResourceSnapshot>();
        public List<WorldRigidbodySnapshot> movableFeatures = new List<WorldRigidbodySnapshot>();
    }
}
