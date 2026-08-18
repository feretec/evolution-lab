using System;
using NUnit.Framework;

namespace EvolutionLab.Tests
{
    public sealed class DeterministicRandomTests
    {
        [Test]
        public void NextDouble_IsAlwaysInSystemRandomRange()
        {
            var random = new DeterministicRandom(123456);
            double minimum = double.MaxValue;
            double maximum = double.MinValue;
            for (int i = 0; i < 100000; i++)
            {
                double value = random.NextDouble();
                minimum = Math.Min(minimum, value);
                maximum = Math.Max(maximum, value);
            }

            Assert.That(minimum, Is.GreaterThanOrEqualTo(0d));
            Assert.That(maximum, Is.LessThan(1d));
        }

        [Test]
        public void NextMinMax_RespectsBoundsAndEdgeContracts()
        {
            var random = new DeterministicRandom(9876);
            Assert.That(random.Next(7, 7), Is.EqualTo(7));

            int[,] ranges =
            {
                { -10, 10 },
                { 0, 1 },
                { int.MinValue, int.MaxValue },
                { int.MaxValue - 1, int.MaxValue }
            };

            for (int rangeIndex = 0; rangeIndex < ranges.GetLength(0); rangeIndex++)
            {
                int min = ranges[rangeIndex, 0];
                int max = ranges[rangeIndex, 1];
                for (int sample = 0; sample < 1000; sample++)
                {
                    int value = random.Next(min, max);
                    Assert.That(value, Is.GreaterThanOrEqualTo(min));
                    Assert.That(value, Is.LessThan(max));
                }
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(2, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(-1));
        }

        [Test]
        public void CapturedState_ReplaysTheSameSequence()
        {
            var random = new DeterministicRandom(2468);
            random.Next();
            random.NextDouble();
            uint state = random.State;
            var resumed = new DeterministicRandom(state);

            for (int i = 0; i < 256; i++)
            {
                Assert.That(resumed.Next(), Is.EqualTo(random.Next()));
                Assert.That(resumed.NextDouble(), Is.EqualTo(random.NextDouble()));
                Assert.That(resumed.Next(-100, 100), Is.EqualTo(random.Next(-100, 100)));
            }
        }

        [Test]
        public void EvolutionEngine_RestoredStateReplaysOffspringRandomness()
        {
            var first = new EvolutionEngine(6, 13579);
            first.Initialize();
            uint state = first.RandomState;
            CreatureGenome firstChild = first.CreateOffspring(
                first.CurrentPopulation[0],
                first.CurrentPopulation[1]);

            var resumed = new EvolutionEngine(6, 13579);
            resumed.Initialize();
            resumed.RestoreRandomState(state, 13579);
            CreatureGenome resumedChild = resumed.CreateOffspring(
                resumed.CurrentPopulation[0],
                resumed.CurrentPopulation[1]);

            Assert.That(resumedChild.ToJson(), Is.EqualTo(firstChild.ToJson()));
        }
    }
}
