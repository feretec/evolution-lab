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

    /// <summary>
    /// Aggregated observations of runtime lifetime-learning metrics.
    /// A sample is counted only when the producer explicitly marks it as
    /// available; missing values are never converted into zero-valued claims.
    /// </summary>
    [Serializable]
    public sealed class LearningTelemetrySummary
    {
        public int observedCount;
        public int enabledCount;
        public float enabledRate;
        public float averageAdaptation;
        public float maximumAdaptation;
        public float averageSignal;

        [NonSerialized]
        private float adaptationTotal;
        [NonSerialized]
        private float signalTotal;

        public void Observe(bool enabled, float signal, float adaptation)
        {
            if (!IsFinite(signal) || !IsFinite(adaptation)) return;
            observedCount++;
            if (enabled) enabledCount++;
            adaptationTotal += Mathf.Max(0f, adaptation);
            signalTotal += signal;
            maximumAdaptation = Mathf.Max(maximumAdaptation, adaptation);
            Recalculate();
        }

        public void Merge(LearningTelemetrySummary other)
        {
            if (other == null || other.observedCount <= 0) return;
            observedCount += other.observedCount;
            enabledCount += Mathf.Max(0, other.enabledCount);
            adaptationTotal += other.averageAdaptation * other.observedCount;
            signalTotal += other.averageSignal * other.observedCount;
            maximumAdaptation = Mathf.Max(maximumAdaptation, other.maximumAdaptation);
            Recalculate();
        }

        public LearningTelemetrySummary Clone()
        {
            var clone = new LearningTelemetrySummary
            {
                observedCount = observedCount,
                enabledCount = enabledCount,
                enabledRate = enabledRate,
                averageAdaptation = averageAdaptation,
                maximumAdaptation = maximumAdaptation,
                averageSignal = averageSignal
            };
            clone.adaptationTotal = adaptationTotal > 0f ? adaptationTotal : averageAdaptation * observedCount;
            clone.signalTotal = signalTotal != 0f ? signalTotal : averageSignal * observedCount;
            return clone;
        }

        private void Recalculate()
        {
            if (observedCount <= 0)
            {
                enabledRate = 0f;
                averageAdaptation = 0f;
                averageSignal = 0f;
                return;
            }

            enabledRate = (float)enabledCount / observedCount;
            averageAdaptation = adaptationTotal / observedCount;
            averageSignal = signalTotal / observedCount;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
