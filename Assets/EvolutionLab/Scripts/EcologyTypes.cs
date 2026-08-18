using System;
using UnityEngine;

namespace EvolutionLab
{
    /// <summary>
    /// Generic physical observations supplied to a controller. The values do
    /// not identify an object as prey, food, shelter, or a role; they only
    /// describe nearby geometry and moving bodies.
    /// </summary>
    [Serializable]
    public struct CreatureInteractionObservation
    {
        public Vector3 nearestIndividualDirection;
        public float nearestIndividualDistance;
        public Vector3 nearestThreatDirection;
        public float nearestThreatDistance;
        public float obstacleProximity;

        public static CreatureInteractionObservation Empty
        {
            get
            {
                return new CreatureInteractionObservation
                {
                    nearestIndividualDirection = Vector3.zero,
                    nearestIndividualDistance = float.PositiveInfinity,
                    nearestThreatDirection = Vector3.zero,
                    nearestThreatDistance = float.PositiveInfinity,
                    obstacleProximity = 0f
                };
            }
        }
    }

    public enum EvolutionEventType
    {
        Birth,
        Death,
        Predation,
        MajorMorphology,
        PopulationChange,
        LineageExtinction,
        Observation
    }

    [Serializable]
    public sealed class EvolutionEventRecord
    {
        public int cycle;
        public int generation;
        public EvolutionEventType type;
        public string subjectId = string.Empty;
        public string relatedId = string.Empty;
        public string message = string.Empty;
        public float value;

        public EvolutionEventRecord Clone()
        {
            return new EvolutionEventRecord
            {
                cycle = cycle,
                generation = generation,
                type = type,
                subjectId = subjectId,
                relatedId = relatedId,
                message = message,
                value = value
            };
        }
    }
}
