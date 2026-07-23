using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimulationInput.Contract;
using SimulationInput.Domain;

namespace SimulationInput.Test.Domain
{
    [TestFixture]
    public sealed class TickInputFrameTests
    {
        [Test]
        public void Constructor_NullInputArrays_UsesEmptyArraysAndStoresTick()
        {
            // Act
            TickInputFrame frame = new TickInputFrame(42, null, null);

            // Assert
            Assert.AreEqual(42ul, frame.Tick);
            Assert.AreEqual(0, frame.Buttons.Length);
            Assert.AreEqual(0, frame.Axes.Length);
        }

        [Test]
        public void SetTick_UpdatesFrameTick()
        {
            // Arrange
            TickInputFrame frame = new TickInputFrame(
                new Dictionary<Type, int>(),
                new Dictionary<Type, int>(),
                null,
                null);

            // Act
            frame.SetTick(7);

            // Assert
            Assert.AreEqual(7ul, frame.Tick);
        }

        [Test]
        public void GetRegisteredInputs_ReturnsValuesByKey()
        {
            // Arrange
            ButtonInputState jumpState = new ButtonInputState(true, true, false);
            AxisInputEvent horizontalState = new AxisInputEvent(123f);
            TickInputFrame frame = CreateFrame(
                new[] { jumpState },
                new[] { horizontalState });

            // Act / Assert
            Assert.AreEqual(jumpState, frame.GetButton<JumpButton>());
            Assert.AreEqual(horizontalState, frame.GetAxis<HorizontalAxis>());
        }

        [Test]
        public void TryGetRegisteredInputs_ReturnsTrueAndValue()
        {
            // Arrange
            ButtonInputState jumpState = new ButtonInputState(true, true, false);
            AxisInputEvent horizontalState = new AxisInputEvent(123f);
            TickInputFrame frame = CreateFrame(
                new[] { jumpState },
                new[] { horizontalState });

            // Act
            bool foundButton = frame.TryGetButton<JumpButton>(
                out ButtonInputState foundButtonState);
            bool foundAxis = frame.TryGetAxis<HorizontalAxis>(
                out AxisInputEvent foundAxisState);

            // Assert
            Assert.IsTrue(foundButton);
            Assert.AreEqual(jumpState, foundButtonState);
            Assert.IsTrue(foundAxis);
            Assert.AreEqual(horizontalState, foundAxisState);
        }

        [Test]
        public void TryGetUnregisteredInputs_ReturnsFalseAndDefault()
        {
            // Arrange
            TickInputFrame frame = CreateFrame(
                new[] { new ButtonInputState(true, true, false) },
                new[] { new AxisInputEvent(123f) });

            // Act
            bool foundButton = frame.TryGetButton<FireButton>(
                out ButtonInputState foundButtonState);
            bool foundAxis = frame.TryGetAxis<VerticalAxis>(
                out AxisInputEvent foundAxisState);

            // Assert
            Assert.IsFalse(foundButton);
            Assert.AreEqual(default(ButtonInputState), foundButtonState);
            Assert.IsFalse(foundAxis);
            Assert.AreEqual(default(AxisInputEvent), foundAxisState);
        }

        [Test]
        public void GetUnregisteredInputs_Throws()
        {
            // Arrange
            TickInputFrame frame = CreateFrame(
                new[] { new ButtonInputState(true, true, false) },
                new[] { new AxisInputEvent(123f) });

            // Act / Assert
            Assert.Throws<KeyNotFoundException>(
                () => frame.GetButton<FireButton>());
            Assert.Throws<KeyNotFoundException>(
                () => frame.GetAxis<VerticalAxis>());
        }

        private static TickInputFrame CreateFrame(
            ButtonInputState[] buttons,
            AxisInputEvent[] axes)
        {
            return new TickInputFrame(
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
