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
        public float energy;
        public float age;
        public int offspringCount;
        public int killCount;
        public float damageTaken;
    }

    [Serializable]
    public sealed class WorldSnapshotArchive
    {
        public int schemaVersion = 1;
        public int generation = 1;
        public float evaluationElapsed;
        public string historyJson = string.Empty;
        public List<CreatureGenome> population = new List<CreatureGenome>();
        public List<WorldCreatureSnapshot> creatures = new List<WorldCreatureSnapshot>();
    }
}
