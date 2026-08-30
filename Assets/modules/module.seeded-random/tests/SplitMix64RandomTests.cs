using System;
using NUnit.Framework;
using SeededRandom.Contract;

namespace SeededRandom.Tests
{
    public sealed class SplitMix64RandomTests
    {
        [Test]
        public void SeedZero_MatchesPinnedReferenceVector()
        {
            var random = new SplitMix64Random(0);
            ulong[] expected =
            {
                0xE220A8397B1DCDAFUL, 0x6E789E6AA1B965F4UL,
                0x06C45D188009454FUL, 0xF88BB8A8724C81ECUL
            };
            foreach (ulong value in expected) Assert.That(random.NextUInt64(), Is.EqualTo(value));
        }

        [Test]
        public void SameSeedAndCalls_ProduceSameSequence()
        {
            var first = new SplitMix64Random(ulong.MaxValue);
            var second = new SplitMix64Random(ulong.MaxValue);
            for (int i = 0; i < 100; i++)
            {
                Assert.That(first.NextUInt64(), Is.EqualTo(second.NextUInt64()));
                Assert.That(first.NextInt(-100, 700), Is.EqualTo(second.NextInt(-100, 700)));
                Assert.That(first.NextSingle(), Is.EqualTo(second.NextSingle()));
                Assert.That(first.NextDouble(), Is.EqualTo(second.NextDouble()));
            }
        }

        [Test]
        public void Restore_ResumesExactMixedDrawSequenceInAnotherInstance()
        {
            var random = new SplitMix64Random(42);
            random.NextUInt64();
            RandomState state = random.CaptureState();
            uint integer = random.NextUInt32(3000000000u);
            float single = random.NextSingle();
            double fractional = random.NextDouble();
            ulong raw = random.NextUInt64();
            var restored = new SplitMix64Random(999);
            restored.RestoreState(state);
            Assert.That(restored.NextUInt32(3000000000u), Is.EqualTo(integer));
            Assert.That(restored.NextSingle(), Is.EqualTo(single));
            Assert.That(restored.NextDouble(), Is.EqualTo(fractional));
            Assert.That(restored.NextUInt64(), Is.EqualTo(raw));
        }

        [Test]
        public void Bounds_IncludeNegativeAndFullWidthIntRanges()
        {
            var random = new SplitMix64Random(11);
            for (int i = 0; i < 2000; i++)
            {
                Assert.That(random.NextUInt32(1), Is.Zero);
                Assert.That(random.NextUInt32(uint.MaxValue), Is.LessThan(uint.MaxValue));
                Assert.That(random.NextInt(-9, -3), Is.InRange(-9, -4));
                Assert.That(random.NextInt(int.MinValue, int.MaxValue), Is.LessThan(int.MaxValue));
                Assert.That(random.NextInt(int.MaxValue - 1, int.MaxValue), Is.EqualTo(int.MaxValue - 1));
                Assert.That(random.NextSingle(), Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                Assert.That(random.NextDouble(), Is.GreaterThanOrEqualTo(0d).And.LessThan(1d));
            }
        }

        [Test]
        public void InvalidRangesAndVersions_DoNotAdvanceState()
        {
            var random = new SplitMix64Random(7);
            RandomState before = random.CaptureState();
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextUInt32(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(3, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(4, 3));
            Assert.Throws<NotSupportedException>(() => random.RestoreState(default));
            Assert.Throws<NotSupportedException>(() => random.RestoreState(new RandomState(99, 1)));
            Assert.That(random.CaptureState().Value, Is.EqualTo(before.Value));
        }

        [Test]
        public void NumericStreams_AreReproducibleAndHaveSeparateState()
        {
            var first = SplitMix64Random.FromStream(123, 1);
            var again = SplitMix64Random.FromStream(123, 1);
            var other = SplitMix64Random.FromStream(123, 2);
            Assert.That(first.CaptureState().Value, Is.Not.EqualTo(other.CaptureState().Value));
            for (int i = 0; i < 10; i++) other.NextUInt64();
            Assert.That(first.NextUInt64(), Is.EqualTo(again.NextUInt64()));
        }

        [Test]
        public void BoundedDraw_RejectsLowValuesInsteadOfIntroducingModuloBias()
        {
            var random = new SplitMix64Random(0);
            const uint bound = 0x80000001u;
            Assert.That(random.NextUInt32(bound), Is.EqualTo(0x6220A838u));
            // Reference draws 2 and 3 are below rejection threshold 0x7fffffff.
            Assert.That(random.NextUInt32(bound), Is.EqualTo(0x788BB8A7u));
            var reference = new SplitMix64Random(0);
            for (int i = 0; i < 4; i++) reference.NextUInt64();
            Assert.That(random.CaptureState().Value, Is.EqualTo(reference.CaptureState().Value));
        }

        [Test]
        public void FloatConversions_FollowDocumentedBitMapping()
        {
            var raw = new SplitMix64Random(0);
            var single = new SplitMix64Random(0);
            var fractional = new SplitMix64Random(0);
            ulong first = raw.NextUInt64();
            Assert.That(single.NextSingle(), Is.EqualTo((first >> 40) * (1f / 16777216f)));
            Assert.That(fractional.NextDouble(), Is.EqualTo((first >> 11) * (1.0 / 9007199254740992.0)));
        }
    }
}
