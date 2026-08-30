using System;
using SeededRandom.Contract;

namespace SeededRandom
{
    /// <summary>
    /// Fixed SplitMix64 algorithm. Single-threaded and non-cryptographic.
    /// Version 1 includes the bounded-number and floating-point conversion rules.
    /// Reference (public domain): https://prng.di.unimi.it/splitmix64.c
    /// </summary>
    public sealed class SplitMix64Random : ISeededRandom
    {
        public const int AlgorithmVersion = 1;
        private const ulong Increment = 0x9E3779B97F4A7C15UL;
        private ulong state;

        public SplitMix64Random(ulong seed)
        {
            state = seed;
        }

        /// <summary>
        /// Deterministic keyed generator; construction does not consume another generator.
        /// Numeric stream IDs must be stable across runs. Streams are not guaranteed disjoint.
        /// </summary>
        public static SplitMix64Random FromStream(ulong seed, ulong streamId)
        {
            return new SplitMix64Random(Mix(seed ^ Mix(unchecked(streamId + Increment))));
        }

        public ulong NextUInt64()
        {
            state = unchecked(state + Increment);
            return Mix(state);
        }

        public uint NextUInt32() => (uint)(NextUInt64() >> 32);

        /// <summary>Uniform integer in [0, exclusiveMax), using rejection to avoid modulo bias.</summary>
        public uint NextUInt32(uint exclusiveMax)
        {
            if (exclusiveMax == 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            uint threshold = unchecked(0u - exclusiveMax) % exclusiveMax;
            uint value;
            do { value = NextUInt32(); } while (value < threshold);
            return value % exclusiveMax;
        }

        /// <summary>Uniform integer in [inclusiveMin, exclusiveMax). Empty ranges throw before drawing.</summary>
        public int NextInt(int inclusiveMin, int exclusiveMax)
        {
            if (inclusiveMin >= exclusiveMax)
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax), "Range must be nonempty.");
            uint width = (uint)((long)exclusiveMax - inclusiveMin);
            return (int)((long)inclusiveMin + NextUInt32(width));
        }

        /// <summary>24 random bits mapped to [0, 1); exactly one generator draw.</summary>
        public float NextSingle() => (NextUInt64() >> 40) * (1f / 16777216f);

        /// <summary>53 random bits mapped to [0, 1); exactly one generator draw.</summary>
        public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

        public RandomState CaptureState() => new RandomState(AlgorithmVersion, state);

        public void RestoreState(RandomState snapshot)
        {
            if (snapshot.AlgorithmVersion != AlgorithmVersion)
                throw new NotSupportedException($"Unsupported RNG state version {snapshot.AlgorithmVersion}.");
            state = snapshot.Value;
        }

        private static ulong Mix(ulong value)
        {
            unchecked
            {
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }
    }
}
