using Arena.Application;
using SeededRandom;

namespace Arena.Infrastructure
{
    /// <summary>Stable numeric stream identities are part of the replay policy.</summary>
    public sealed class SpawnRandom : ISpawnRandom
    {
        private readonly SplitMix64Random health;
        private readonly SplitMix64Random delay;
        public SpawnRandom(ulong seed)
        {
            health = SplitMix64Random.FromStream(seed, 1);
            delay = SplitMix64Random.FromStream(seed, 2);
        }
        public int NextHealth(int min, int maxInclusive) => health.NextInt(min, checked(maxInclusive + 1));
        public int NextDelay(int min, int maxInclusive) => delay.NextInt(min, checked(maxInclusive + 1));
        public ulong HealthState => health.CaptureState().Value;
        public ulong DelayState => delay.CaptureState().Value;
    }
}
