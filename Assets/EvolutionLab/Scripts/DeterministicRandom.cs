using System;

namespace EvolutionLab
{
    /// <summary>
    /// Serializable, explicit xorshift32 PRNG. The sequence is independent of
    /// the runtime/framework implementation of System.Random while remaining
    /// usable by the existing Genome APIs.
    /// </summary>
    [Serializable]
    public sealed class DeterministicRandom : Random
    {
        private const uint DefaultState = 0xA341316Cu;
        private uint state;

        public DeterministicRandom(int seed)
        {
            state = SeedToState(seed);
        }

        public DeterministicRandom(uint serializedState)
        {
            State = serializedState;
        }

        public uint State
        {
            get { return state; }
            set { state = value == 0u ? DefaultState : value; }
        }

        public void Reset(int seed)
        {
            state = SeedToState(seed);
        }

        public DeterministicRandom Clone()
        {
            return new DeterministicRandom(State);
        }

        public override int Next()
        {
            return (int)(NextUInt() & 0x7FFFFFFFu);
        }

        public override int Next(int maxValue)
        {
            if (maxValue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxValue));
            }

            if (maxValue == 0)
            {
                return 0;
            }

            return (int)((ulong)NextUInt() * (uint)maxValue >> 32);
        }

        public override int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(minValue));
            }

            long range = (long)maxValue - minValue;
            if (range == 0)
            {
                return minValue;
            }

            if (range <= int.MaxValue)
            {
                return minValue + Next((int)range);
            }

            return (int)(minValue + (long)(NextDouble() * range));
        }

        public override double NextDouble()
        {
            ulong high = (ulong)(NextUInt() >> 5);
            ulong low = (ulong)(NextUInt() >> 6);
            return ((high << 26) + low) / 9007199254740992.0;
        }

        public override void NextBytes(byte[] buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte)(NextUInt() >> 24);
            }
        }

        public uint NextUInt()
        {
            uint value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value == 0u ? DefaultState : value;
            return state;
        }

        private static uint SeedToState(int seed)
        {
            unchecked
            {
                uint value = (uint)seed + 0x9E3779B9u;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value == 0u ? DefaultState : value;
            }
        }
    }
}
