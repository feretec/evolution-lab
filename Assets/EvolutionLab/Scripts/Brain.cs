using System;
using UnityEngine;

namespace EvolutionLab
{
    /// <summary>
    /// Runtime evaluator for the controller encoded by a CreatureGenome.
    /// The network shape is intentionally small and fixed for the first prototypes;
    /// Prototype 2 appends ecological observations without changing the genome boundary.
    /// </summary>
    public sealed class Brain
    {
        private readonly BrainGene gene;
        private readonly float[] hidden;
        private readonly float[] outputs;

        public Brain(BrainGene source)
        {
            gene = source == null ? new BrainGene() : source;
            gene.EnsureShape();
            hidden = new float[BrainGene.HiddenCount];
            outputs = new float[BrainGene.MaxOutputCount];
        }

        public int OutputCount
        {
            get { return outputs.Length; }
        }

        public float[] Evaluate(float[] inputs)
        {
            if (inputs == null)
            {
                inputs = Array.Empty<float>();
            }

            for (int h = 0; h < hidden.Length; h++)
            {
                float sum = gene.hiddenBiases[h];
                int weightOffset = h * BrainGene.InputCount;
                for (int i = 0; i < BrainGene.InputCount; i++)
                {
                    float input = i < inputs.Length ? inputs[i] : 0f;
                    sum += input * gene.inputHiddenWeights[weightOffset + i];
                }

                hidden[h] = Tanh(sum);
            }

            for (int o = 0; o < outputs.Length; o++)
            {
                float sum = gene.outputBiases[o];
                int weightOffset = o;
                for (int h = 0; h < hidden.Length; h++)
                {
                    sum += hidden[h] * gene.hiddenOutputWeights[h * BrainGene.MaxOutputCount + weightOffset];
                }

                outputs[o] = Tanh(sum);
            }

            return outputs;
        }

        private static float Tanh(float value)
        {
            return (float)Math.Tanh(value);
        }
    }
}
