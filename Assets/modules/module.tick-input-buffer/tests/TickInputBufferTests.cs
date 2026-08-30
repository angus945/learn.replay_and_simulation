using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TickInputBuffering.Tests
{
    public sealed class TickInputBufferTests
    {
        private static TickInputBuffer Create()
        {
            var input = new TickInputBuffer();
            input.RegisterButton(2);
            input.RegisterAxis(7);
            input.Seal();
            return input;
        }

        [Test]
        public void QuickTap_PreservesBothEdgesUntilConsumed()
        {
            var input = Create();
            input.CaptureButton(2, true);
            input.CaptureButton(2, false);
            var first = input.ConsumeTick(0).GetButton(2);
            Assert.That(first.Pressed && first.Released && !first.Down, Is.True);
            var second = input.ConsumeTick(1).GetButton(2);
            Assert.That(second.Pressed || second.Released || second.Down, Is.False);
        }

        [Test]
        public void HeldButton_DoesNotRepeatPressedAcrossCatchupTicks()
        {
            var input = Create();
            input.CaptureButton(2, true);
            Assert.That(input.ConsumeTick(1).GetButton(2).Pressed, Is.True);
            input.CaptureButton(2, true);
            var held = input.ConsumeTick(2).GetButton(2);
            Assert.That(held.Down, Is.True);
            Assert.That(held.Pressed || held.Released, Is.False);
        }

        [Test]
        public void Snapshot_IsNotChangedByLaterCapturesOrConsumes()
        {
            var input = Create();
            input.CaptureAxis(7, 0.5f);
            input.CaptureButton(2, true);
            var saved = input.ConsumeTick(1);
            input.CaptureAxis(7, -1f);
            input.CaptureButton(2, false);
            input.ConsumeTick(2);
            Assert.That(saved.GetAxis(7).Value, Is.EqualTo(0.5f));
            Assert.That(saved.GetButton(2).Down, Is.True);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<Contract.ButtonInput>)saved.Buttons)[0] = default);
        }

        [Test]
        public void RegistrationOrder_DoesNotChangeSnapshotOrder()
        {
            var input = new TickInputBuffer();
            input.RegisterButton(9);
            input.RegisterButton(1, true);
            input.RegisterAxis(8);
            input.RegisterAxis(0);
            input.Seal();
            var frame = input.ConsumeTick(3);
            Assert.That(frame.Buttons[0].Id, Is.EqualTo(1));
            Assert.That(frame.Axes[0].Id, Is.EqualTo(0));
            Assert.That(frame.Buttons[0].Down, Is.True);
            Assert.That(frame.Buttons[0].Pressed, Is.False);
        }

        [Test]
        public void InvalidTick_DoesNotConsumePendingEdges()
        {
            var input = Create();
            input.ConsumeTick(5);
            input.CaptureButton(2, true);
            Assert.Throws<ArgumentOutOfRangeException>(() => input.ConsumeTick(5));
            Assert.Throws<ArgumentOutOfRangeException>(() => input.ConsumeTick(4));
            Assert.That(input.ConsumeTick(6).GetButton(2).Pressed, Is.True);
        }

        [Test]
        public void InvalidConfigurationAndUnknownInputs_AreRejected()
        {
            var input = new TickInputBuffer();
            Assert.Throws<InvalidOperationException>(() => input.ConsumeTick(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => input.RegisterButton(-1));
            input.RegisterButton(2);
            Assert.Throws<InvalidOperationException>(() => input.RegisterButton(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => input.RegisterAxis(1, float.NaN));
            input.Seal();
            Assert.Throws<InvalidOperationException>(() => input.RegisterButton(3));
            Assert.Throws<KeyNotFoundException>(() => input.CaptureButton(3, true));
            Assert.Throws<KeyNotFoundException>(() => input.CaptureAxis(3, 1f));
        }

        [Test]
        public void Axis_UsesLatestFiniteValueAndPersistsAcrossTicks()
        {
            var input = Create();
            input.CaptureAxis(7, 1f);
            input.CaptureAxis(7, -0.25f);
            Assert.Throws<ArgumentOutOfRangeException>(() => input.CaptureAxis(7, float.PositiveInfinity));
            Assert.That(input.ConsumeTick(1).GetAxis(7).Value, Is.EqualTo(-0.25f));
            Assert.That(input.ConsumeTick(2).GetAxis(7).Value, Is.EqualTo(-0.25f));
        }
    }
}
