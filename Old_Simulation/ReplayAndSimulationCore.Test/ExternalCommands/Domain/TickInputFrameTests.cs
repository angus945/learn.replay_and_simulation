using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimulationCore.ExternalCommands.PlayerInput.Contract;
using SimulationCore.ExternalCommands.PlayerInput.Domain;

namespace ReplayAndSimulationCore.Test.ExternalCommands.Domain
{
    [TestFixture]
    public sealed class TickInputFrameTests
    {
        [Test]
        public void Constructor_NullInputArrays_UsesEmptyArraysAndStoresTick()
        {
            TickInputFrame frame = new TickInputFrame(42, null, null);

            Assert.AreEqual(42ul, frame.Tick);
            Assert.AreEqual(0, frame.Buttons.Length);
            Assert.AreEqual(0, frame.Axes.Length);
        }

        [Test]
        public void SetTick_UpdatesFrameTick()
        {
            TickInputFrame frame = CreateMappedFrame(
                new ButtonInputState[0],
                new AxisInputEvent[0]);

            TestFrameFactory.SetTick(frame, 7);

            Assert.AreEqual(7ul, frame.Tick);
        }

        [Test]
        public void GetRegisteredInputs_ReturnsValuesByKey()
        {
            ButtonInputState jumpState = new ButtonInputState(true, true, false);
            AxisInputEvent horizontalState = new AxisInputEvent(123f);
            TickInputFrame frame = CreateMappedFrame(
                new[] { jumpState },
                new[] { horizontalState });

            Assert.AreEqual(jumpState, frame.GetButton<JumpButton>());
            Assert.AreEqual(horizontalState, frame.GetAxis<HorizontalAxis>());
        }

        [Test]
        public void TryGetUnregisteredInputs_ReturnsFalseAndDefault()
        {
            TickInputFrame frame = CreateMappedFrame(
                new[] { new ButtonInputState(true, true, false) },
                new[] { new AxisInputEvent(123f) });

            bool foundButton = frame.TryGetButton<FireButton>(
                out ButtonInputState foundButtonState);
            bool foundAxis = frame.TryGetAxis<VerticalAxis>(
                out AxisInputEvent foundAxisState);

            Assert.IsFalse(foundButton);
            Assert.AreEqual(default(ButtonInputState), foundButtonState);
            Assert.IsFalse(foundAxis);
            Assert.AreEqual(default(AxisInputEvent), foundAxisState);
        }

        [Test]
        public void GetUnregisteredInputs_Throws()
        {
            TickInputFrame frame = CreateMappedFrame(
                new[] { new ButtonInputState(true, true, false) },
                new[] { new AxisInputEvent(123f) });

            Assert.Throws<KeyNotFoundException>(
                () => frame.GetButton<FireButton>());
            Assert.Throws<KeyNotFoundException>(
                () => frame.GetAxis<VerticalAxis>());
        }

        [Test]
        public void KeyLookup_ReplayingSameFrameReads_ProducesSameValues()
        {
            CollectionAssert.AreEqual(
                RunFrameLookupScript(),
                RunFrameLookupScript());
        }

        private static string[] RunFrameLookupScript()
        {
            TickInputFrame frame = TestFrameFactory.CreateFrame(
                new Dictionary<Type, int>
                {
                    { typeof(JumpButton), 0 },
                    { typeof(FireButton), 1 }
                },
                new Dictionary<Type, int>
                {
                    { typeof(HorizontalAxis), 0 },
                    { typeof(VerticalAxis), 1 }
                },
                new[]
                {
                    new ButtonInputState(true, true, false),
                    new ButtonInputState(false, false, true)
                },
                new[]
                {
                    new AxisInputEvent(10f),
                    new AxisInputEvent(-20f)
                });

            return new[]
            {
                ToSignature(frame.GetButton<JumpButton>()),
                ToSignature(frame.GetButton<FireButton>()),
                frame.GetAxis<HorizontalAxis>().Value.ToString(),
                frame.GetAxis<VerticalAxis>().Value.ToString()
            };
        }

        private static TickInputFrame CreateMappedFrame(
            ButtonInputState[] buttons,
            AxisInputEvent[] axes)
        {
            return TestFrameFactory.CreateFrame(
                new Dictionary<Type, int>
                {
                    { typeof(JumpButton), 0 }
                },
                new Dictionary<Type, int>
                {
                    { typeof(HorizontalAxis), 0 }
                },
                buttons,
                axes);
        }

        private static string ToSignature(ButtonInputState state)
        {
            return $"{state.IsPressed}|{state.IsDown}|{state.IsReleased}";
        }

        private readonly struct JumpButton : IButtonInputKey
        {
        }

        private readonly struct FireButton : IButtonInputKey
        {
        }

        private readonly struct HorizontalAxis : IAxisInputKey
        {
        }

        private readonly struct VerticalAxis : IAxisInputKey
        {
        }
    }
}
