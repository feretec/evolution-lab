using System;
using UnityEngine;

namespace EvolutionLab
{
    /// <summary>
    /// Runtime evaluator for the controller encoded by a CreatureGenome.
    ///
    /// BrainGene contains inherited base weights and inherited learning rules.
    /// The arrays prefixed with "fast" and the traces/memory below are
    /// per-creature runtime state. They are never written back to the genome,
    /// which gives this controller Baldwinian inheritance semantics.
    /// </summary>
    public sealed class Brain
    {
        private const float NominalFixedStepsPerSecond = 50f;
        private const float TraceLimit = 4f;
        private const float MemoryContribution = 0.25f;

        // Inherited controller definition. This reference is never used as a
        // destination for acquired learning updates.
        private readonly BrainGene gene;
        private readonly LifetimeLearningGene learningGene;

        // Runtime embodiment state: each Brain instance belongs to one
        // creature and is reset at birth. None of this is serialized.
        private readonly float[] hidden;
        private readonly float[] outputs;
        private readonly float[] lastInputs;
        private readonly float[] inputEligibility;
        private readonly float[] hiddenEligibility;
        private readonly float[] shortTermMemory;
        private readonly float[] fastInputHiddenWeights;
        private readonly float[] fastHiddenOutputWeights;

        private bool hasActivation;
        private float pendingHomeostaticSignal;
        private float lastHomeostaticSignal;
        private float rewardBaseline;
        private float adaptationMagnitude;

        public Brain(BrainGene source)
        {
            gene = source == null ? new BrainGene() : source;
            gene.EnsureShape();
            learningGene = gene.learning ?? new LifetimeLearningGene();
            learningGene.Repair();

            hidden = new float[BrainGene.HiddenCount];
            outputs = new float[BrainGene.MaxOutputCount];
            lastInputs = new float[BrainGene.InputCount];
            inputEligibility = new float[BrainGene.InputCount];
            hiddenEligibility = new float[BrainGene.HiddenCount];
            shortTermMemory = new float[BrainGene.HiddenCount];
            fastInputHiddenWeights = new float[BrainGene.InputCount * BrainGene.HiddenCount];
            fastHiddenOutputWeights = new float[BrainGene.HiddenCount * BrainGene.MaxOutputCount];
            ResetRuntimeState();
        }

        public int OutputCount
        {
            get { return outputs.Length; }
        }

        public int ActiveHiddenCount
        {
            get { return Mathf.Clamp(gene.activeHiddenCount, 2, BrainGene.HiddenCount); }
        }

        /// <summary>Whether this inherited genome permits lifetime learning.</summary>
        public bool LearningEnabled
        {
            get { return learningGene.enabled; }
        }

        /// <summary>The most recently applied homeostatic learning signal.</summary>
        public float LastHomeostaticSignal
        {
            get { return lastHomeostaticSignal; }
        }

        /// <summary>
        /// Mean absolute runtime fast-weight magnitude. This is a read-only
        /// observation metric; it does not expose mutable learning state.
        /// </summary>
        public float AdaptationMagnitude
        {
            get { return adaptationMagnitude; }
        }

        /// <summary>
        /// Copies acquired lifetime state into a world snapshot. Inherited
        /// BrainGene weights are deliberately excluded; this contract owns
        /// only the mutable state of this Brain instance.
        /// </summary>
        public void CaptureRuntimeState(BrainRuntimeSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            snapshot.hasState = true;
            snapshot.hidden = (float[])hidden.Clone();
            snapshot.outputs = (float[])outputs.Clone();
            snapshot.lastInputs = (float[])lastInputs.Clone();
            snapshot.inputEligibility = (float[])inputEligibility.Clone();
            snapshot.hiddenEligibility = (float[])hiddenEligibility.Clone();
            snapshot.shortTermMemory = (float[])shortTermMemory.Clone();
            snapshot.fastInputHiddenWeights = (float[])fastInputHiddenWeights.Clone();
            snapshot.fastHiddenOutputWeights = (float[])fastHiddenOutputWeights.Clone();
            snapshot.hasActivation = hasActivation;
            snapshot.pendingHomeostaticSignal = SafeClamp(pendingHomeostaticSignal, -1f, 1f);
            snapshot.lastHomeostaticSignal = SafeClamp(lastHomeostaticSignal, -1f, 1f);
            snapshot.rewardBaseline = SafeClamp(rewardBaseline, -1f, 1f);
            snapshot.adaptationMagnitude = SafeClamp(adaptationMagnitude, 0f, 1.5f);
        }

        /// <summary>
        /// Restores acquired lifetime state. Missing or incompatible state is
        /// treated as a fresh lifetime, which keeps schema 1/2 archives safe.
        /// </summary>
        public void RestoreRuntimeState(BrainRuntimeSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.hasState)
            {
                ResetRuntimeState();
                return;
            }

            RestoreArray(hidden, snapshot.hidden);
            RestoreArray(outputs, snapshot.outputs);
            RestoreArray(lastInputs, snapshot.lastInputs);
            RestoreArray(inputEligibility, snapshot.inputEligibility);
            RestoreArray(hiddenEligibility, snapshot.hiddenEligibility);
            RestoreArray(shortTermMemory, snapshot.shortTermMemory);
            RestoreArray(fastInputHiddenWeights, snapshot.fastInputHiddenWeights);
            RestoreArray(fastHiddenOutputWeights, snapshot.fastHiddenOutputWeights);
            hasActivation = snapshot.hasActivation;
            pendingHomeostaticSignal = SafeClamp(snapshot.pendingHomeostaticSignal, -1f, 1f);
            lastHomeostaticSignal = SafeClamp(snapshot.lastHomeostaticSignal, -1f, 1f);
            rewardBaseline = SafeClamp(snapshot.rewardBaseline, -1f, 1f);
            adaptationMagnitude = SafeClamp(snapshot.adaptationMagnitude, 0f, 1.5f);
        }

        /// <summary>
        /// Preserved API for callers that do not supply a timestep. The
        /// controller's original fixed-step cadence is used.
        /// </summary>
        public float[] Evaluate(float[] inputs)
        {
            return Evaluate(inputs, 1f / NominalFixedStepsPerSecond);
        }

        public float[] Evaluate(float[] inputs, float deltaTime)
        {
            if (inputs == null)
            {
                inputs = Array.Empty<float>();
            }

            float traceDecay = Mathf.Clamp(learningGene.eligibilityDecay, 0.55f, 0.995f);
            for (int i = 0; i < lastInputs.Length; i++)
            {
                lastInputs[i] = SafeClamp(i < inputs.Length ? inputs[i] : 0f, -1f, 1f);
                inputEligibility[i] = SafeClamp(
                    inputEligibility[i] * traceDecay + lastInputs[i],
                    -TraceLimit,
                    TraceLimit);
            }

            float retention = Mathf.Clamp(learningGene.memoryRetention, 0.25f, 0.995f);
            int activeHiddenCount = ActiveHiddenCount;
            for (int h = 0; h < activeHiddenCount; h++)
            {
                float sum = SafeWeight(gene.hiddenBiases[h]);
                int weightOffset = h * BrainGene.InputCount;
                for (int i = 0; i < BrainGene.InputCount; i++)
                {
                    float weight = SafeWeight(gene.inputHiddenWeights[weightOffset + i])
                        + fastInputHiddenWeights[weightOffset + i];
                    sum += lastInputs[i] * weight;
                }

                hidden[h] = SafeClamp(Tanh(sum), -1f, 1f);
                shortTermMemory[h] = SafeClamp(
                    shortTermMemory[h] * retention + hidden[h] * (1f - retention),
                    -1f,
                    1f);
                hiddenEligibility[h] = SafeClamp(
                    hiddenEligibility[h] * traceDecay + hidden[h] + shortTermMemory[h] * MemoryContribution,
                    -TraceLimit,
                    TraceLimit);
            }

            for (int h = activeHiddenCount; h < hidden.Length; h++)
            {
                hidden[h] = 0f;
                hiddenEligibility[h] = 0f;
                shortTermMemory[h] = 0f;
            }

            for (int o = 0; o < outputs.Length; o++)
            {
                float sum = SafeWeight(gene.outputBiases[o]);
                for (int h = 0; h < activeHiddenCount; h++)
                {
                    float effectiveHidden = SafeClamp(
                        hidden[h] + shortTermMemory[h] * MemoryContribution,
                        -1f,
                        1f);
                    int weightIndex = h * BrainGene.MaxOutputCount + o;
                    float weight = SafeWeight(gene.hiddenOutputWeights[weightIndex])
                        + fastHiddenOutputWeights[weightIndex];
                    sum += effectiveHidden * weight;
                }

                outputs[o] = SafeClamp(Tanh(sum), -1f, 1f);
            }

            hasActivation = true;
            return outputs;
        }

        /// <summary>
        /// Accumulates only homeostatic feedback. The caller supplies no
        /// semantic task reward: energy balance, damage, actuator effort, and
        /// survival are the complete signal vocabulary.
        /// </summary>
        public void AccumulateHomeostaticFeedback(
            float normalizedEnergyDelta,
            float normalizedDamage,
            float normalizedControlCost,
            bool survived)
        {
            if (!LearningEnabled)
            {
                return;
            }

            float energyTerm = SafeClamp(normalizedEnergyDelta, -1f, 1f)
                * learningGene.energyDeltaScale;
            float damageTerm = Mathf.Abs(SafeClamp(normalizedDamage, -1f, 1f))
                * learningGene.damageScale;
            float controlTerm = Mathf.Abs(SafeClamp(normalizedControlCost, -1f, 1f))
                * learningGene.controlCostScale;
            float survivalTerm = survived ? learningGene.survivalBias : -learningGene.survivalBias * 2f;
            float signal = SafeClamp(energyTerm - damageTerm - controlTerm + survivalTerm, -1f, 1f);
            pendingHomeostaticSignal = SafeClamp(
                pendingHomeostaticSignal + signal,
                -1f,
                1f);
        }

        /// <summary>
        /// Adds actuator effort without adding a second survival bonus. The
        /// movement energy balance is supplied separately by Creature.TickLife.
        /// </summary>
        public void AccumulateControlCost(float normalizedControlCost)
        {
            if (!LearningEnabled)
            {
                return;
            }

            float cost = Mathf.Abs(SafeClamp(normalizedControlCost, -1f, 1f))
                * learningGene.controlCostScale;
            pendingHomeostaticSignal = SafeClamp(
                pendingHomeostaticSignal - cost,
                -1f,
                1f);
        }

        /// <summary>
        /// Applies queued feedback to runtime fast weights. A bounded,
        /// reward-modulated Hebbian rule uses eligibility traces so learning
        /// remains cheap and can associate recent activity with later energy
        /// or damage outcomes.
        /// </summary>
        public void ApplyPendingLearning(float deltaTime)
        {
            float signal = pendingHomeostaticSignal;
            pendingHomeostaticSignal = 0f;
            ApplyHomeostaticLearning(signal, deltaTime);
        }

        public void ApplyHomeostaticLearning(float signal, float deltaTime)
        {
            signal = SafeClamp(signal, -1f, 1f);
            lastHomeostaticSignal = signal;
            if (!LearningEnabled || !hasActivation)
            {
                return;
            }

            float stepScale = SafeClamp(
                (IsFinite(deltaTime) ? deltaTime : 1f / NominalFixedStepsPerSecond)
                    * NominalFixedStepsPerSecond,
                0.25f,
                2f);
            float rate = Mathf.Clamp(learningGene.learningRate, 0.001f, 0.08f);
            float limit = Mathf.Clamp(learningGene.fastWeightLimit, 0.1f, 1.5f);
            float modulator = SafeClamp(signal - rewardBaseline, -1f, 1f);
            float baselineRate = Mathf.Clamp(learningGene.rewardBaselineRate, 0.001f, 0.25f);
            rewardBaseline = SafeClamp(
                Mathf.Lerp(rewardBaseline, signal, baselineRate * stepScale),
                -1f,
                1f);
            float retention = Mathf.Clamp01(
                1f - Mathf.Clamp(learningGene.plasticityDecay, 0f, 0.01f) * stepScale);

            int activeHiddenCount = ActiveHiddenCount;
            for (int h = 0; h < activeHiddenCount; h++)
            {
                int inputOffset = h * BrainGene.InputCount;
                float postTrace = SafeClamp(hiddenEligibility[h], -TraceLimit, TraceLimit);
                for (int i = 0; i < BrainGene.InputCount; i++)
                {
                    float eligibility = SafeClamp(
                        inputEligibility[i] * postTrace,
                        -TraceLimit,
                        TraceLimit);
                    float delta = SafeClamp(rate * stepScale * modulator * eligibility, -0.08f, 0.08f);
                    int index = inputOffset + i;
                    fastInputHiddenWeights[index] = SafeClamp(
                        fastInputHiddenWeights[index] * retention + delta,
                        -limit,
                        limit);
                }

                for (int o = 0; o < BrainGene.MaxOutputCount; o++)
                {
                    int index = h * BrainGene.MaxOutputCount + o;
                    float eligibility = SafeClamp(
                        postTrace * outputs[o],
                        -TraceLimit,
                        TraceLimit);
                    float delta = SafeClamp(rate * stepScale * modulator * eligibility, -0.08f, 0.08f);
                    fastHiddenOutputWeights[index] = SafeClamp(
                        fastHiddenOutputWeights[index] * retention + delta,
                        -limit,
                        limit);
                }
            }

            RecalculateAdaptationMagnitude();
        }

        /// <summary>
        /// Clears all acquired knowledge for a new birth. Inherited Genome
        /// data remains untouched, so offspring start from their base weights.
        /// </summary>
        public void ResetRuntimeState()
        {
            Array.Clear(hidden, 0, hidden.Length);
            Array.Clear(outputs, 0, outputs.Length);
            Array.Clear(lastInputs, 0, lastInputs.Length);
            Array.Clear(inputEligibility, 0, inputEligibility.Length);
            Array.Clear(hiddenEligibility, 0, hiddenEligibility.Length);
            Array.Clear(shortTermMemory, 0, shortTermMemory.Length);
            Array.Clear(fastInputHiddenWeights, 0, fastInputHiddenWeights.Length);
            Array.Clear(fastHiddenOutputWeights, 0, fastHiddenOutputWeights.Length);
            hasActivation = false;
            pendingHomeostaticSignal = 0f;
            lastHomeostaticSignal = 0f;
            rewardBaseline = 0f;
            adaptationMagnitude = 0f;
        }

        private void RecalculateAdaptationMagnitude()
        {
            double total = 0d;
            int activeHiddenCount = ActiveHiddenCount;
            int activeInputWeightCount = activeHiddenCount * BrainGene.InputCount;
            int activeOutputWeightCount = activeHiddenCount * BrainGene.MaxOutputCount;
            int count = activeInputWeightCount + activeOutputWeightCount;
            for (int i = 0; i < activeInputWeightCount; i++)
            {
                total += Math.Abs(SafeWeight(fastInputHiddenWeights[i]));
            }

            for (int i = 0; i < activeOutputWeightCount; i++)
            {
                total += Math.Abs(SafeWeight(fastHiddenOutputWeights[i]));
            }

            adaptationMagnitude = count <= 0
                ? 0f
                : SafeClamp((float)(total / count), 0f, 1.5f);
        }

        private static void RestoreArray(float[] target, float[] source)
        {
            if (target == null)
            {
                return;
            }

            int sourceLength = source == null ? 0 : Mathf.Min(target.Length, source.Length);
            for (int i = 0; i < sourceLength; i++)
            {
                target[i] = SafeClamp(source[i], -float.MaxValue, float.MaxValue);
            }
            for (int i = sourceLength; i < target.Length; i++)
            {
                target[i] = 0f;
            }
        }

        private static float Tanh(float value)
        {
            return (float)Math.Tanh(IsFinite(value) ? value : 0f);
        }

        private static float SafeWeight(float value)
        {
            return IsFinite(value) ? Mathf.Clamp(value, -3f, 3f) : 0f;
        }

        private static float SafeClamp(float value, float min, float max)
        {
            return IsFinite(value) ? Mathf.Clamp(value, min, max) : 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
