using CharacterMovement.Domain;
using CharacterMovement.Integration;
using MovementDemo.Unity;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MovementDemo.Tests
{
    public sealed class MovementDemoTests
    {
        [Test]
        public void InputWaitsForTickThenMovesAndInterpolates()
        {
            var view = new View();
            var session = new MovementDemoSession(view, 4, .25f);
            session.CaptureAxes(1, 0);
            session.AdvanceTime(.125f);
            Assert.That(session.TickNumber, Is.Zero);
            Assert.That(session.CurrentPosition.X, Is.Zero);
            session.AdvanceTime(.25f);
            session.UpdatePresentation();
            Assert.That(session.TickNumber, Is.EqualTo(1));
            Assert.That(session.CurrentPosition.X, Is.EqualTo(1));
            Assert.That(view.Position.X, Is.EqualTo(.5f));
        }

        [Test]
        public void CatchUpTicksReuseHeldInputAndReleaseStopsNextTick()
        {
            var session = new MovementDemoSession(new View(), 4, .25f);
            session.CaptureAxes(1, 0);
            session.AdvanceTime(1);
            Assert.That(session.TickNumber, Is.EqualTo(4));
            Assert.That(session.CurrentPosition.X, Is.EqualTo(4));
            session.CaptureAxes(0, 0);
            session.AdvanceTime(.25f);
            Assert.That(session.CurrentPosition.X, Is.EqualTo(4));
        }

        [Test]
        public void SameTickInputHasSameStateAcrossRenderSchedules()
        {
            var fast = new MovementDemoSession(new View(), 4, .125f);
            var slow = new MovementDemoSession(new View(), 4, .125f);
            fast.CaptureAxes(1, 1);
            slow.CaptureAxes(1, 1);
            for (int i = 0; i < 64; i++)
            {
                fast.AdvanceTime(.015625f);
                fast.UpdatePresentation();
            }
            slow.AdvanceTime(1);
            slow.UpdatePresentation();
            Assert.That(fast.TickNumber, Is.EqualTo(slow.TickNumber));
            Assert.That(fast.CurrentPosition.X, Is.EqualTo(slow.CurrentPosition.X));
            Assert.That(fast.CurrentPosition.Y, Is.EqualTo(slow.CurrentPosition.Y));
        }

        [Test]
        public void LastInputBeforeTickWinsAndRenderOnlyDoesNotAdvanceState()
        {
            var session = new MovementDemoSession(new View(), 4, .25f);
            session.CaptureAxes(1, 0);
            session.CaptureAxes(-1, 0);
            session.UpdatePresentation();
            Assert.That(session.TickNumber, Is.Zero);
            session.AdvanceTime(.25f);
            Assert.That(session.CurrentPosition.X, Is.EqualTo(-1));
        }

        [Test]
        public void KeyboardAdapterSupportsArrowsOppositesAndFocusLoss()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                var session = new MovementDemoSession(new View(), 4, .25f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
                InputSystem.Update();
                KeyboardMovementInput.Capture(keyboard, true, session);
                session.AdvanceTime(.25f);
                Assert.That(session.CurrentPosition.X, Is.EqualTo(1));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.A));
                InputSystem.Update();
                KeyboardMovementInput.Capture(keyboard, true, session);
                session.AdvanceTime(.25f);
                Assert.That(session.CurrentPosition.X, Is.EqualTo(1));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                InputSystem.Update();
                KeyboardMovementInput.Capture(keyboard, false, session);
                session.AdvanceTime(.25f);
                Assert.That(session.CurrentPosition.Y, Is.Zero);
                KeyboardMovementInput.Capture(keyboard, true, session);
                session.AdvanceTime(.25f);
                Assert.That(session.CurrentPosition.Y, Is.EqualTo(1));
                KeyboardMovementInput.Capture(null, true, session);
                session.AdvanceTime(.25f);
                Assert.That(session.CurrentPosition.Y, Is.EqualTo(1));
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        private sealed class View : ICharacterMovementView
        {
            public MovementPosition Position;
            public void SetPosition(MovementPosition position) => Position = position;
        }
    }
}
