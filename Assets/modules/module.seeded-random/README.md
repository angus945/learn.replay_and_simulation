# module.seeded-random

Single-threaded deterministic SplitMix64. No System.Random, Unity, global mutable RNG or wall clock.

Algorithm reference: https://prng.di.unimi.it/splitmix64.c (Sebastiano Vigna, public domain).

- Explicit ulong seed; all seeds including zero are valid.
- NextUInt64 is SplitMix64; NextUInt32 uses its upper 32 bits.
- Bounded integers use rejection sampling, not biased modulo alone. Bounds are [min, max).
  A bounded draw may consume multiple raw draws; invalid arguments consume none.
- NextSingle uses the upper 24 bits; NextDouble uses the upper 53 bits, both in [0, 1).
- CaptureState / RestoreState preserve the exact future sequence. State is (algorithmVersion, value).
  Persist both fields losslessly (e.g. ulong hex or binary, not a floating-point JSON number).
  Unsupported versions/default state are rejected without changing the generator.
- FromStream(seed, numericStreamId) derives separately owned deterministic generators.
  IDs must be stable (never string.GetHashCode). This does not promise disjoint subsequences.
- No cryptographic security. Stable RNG alone does not guarantee deterministic gameplay,
  floating-point physics, or nondeterministic caller ordering.
- Algorithm/version changes require an explicit replay compatibility decision.

## Minimal usage

```csharp
var random = SeededRandom.SplitMix64Random.FromStream(seed: 123, streamId: 1);
var checkpoint = random.CaptureState();
int roll = random.NextInt(1, 7);
random.RestoreState(checkpoint);
int repeatedRoll = random.NextInt(1, 7); // identical
```
