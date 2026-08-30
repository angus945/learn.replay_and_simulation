using System;
using DiagnosticTrace;
using GameplaySimulation;
using Invariants;
using NUnit.Framework;
using Testability;

namespace DebugOverlay.Tests
{
    public sealed class DiagnosticReaderTests
    {
        [Test]
        public void ReaderCannotBecomeGameplayOrAdminController()
        {
            GameplaySession session = new GameplaySession();
            Assert.That(session.Diagnostics, Is.Not.InstanceOf<GameplaySession>());
            Assert.That(session.Diagnostics, Is.Not.InstanceOf<IGameplayControl>());
            Assert.That(session.Diagnostics, Is.Not.InstanceOf<ITestSession<GameplayScenario>>());
        }

        [Test]
        public void PollDoesNotAdvanceTickEvaluateRulesOrRecordTrace()
        {
            int calls = 0;
            GameplaySession session = new GameplaySession();
            session.RegisterInvariant(() => new CountingCheck(() => calls++));
            session.Start(new GameplayScenario());
            Assert.That(session.Diagnostics.ObserveDiagnostics().Invariants.Evaluated, Is.False);
            session.Step();
            int traceCount = session.ReadTrace().Count;
            ReadOnlyDiagnosticsModel<GameplayObservation> model = new ReadOnlyDiagnosticsModel<GameplayObservation>(session.Diagnostics);
            for (int i = 0; i < 10; i++) model.Poll();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(session.CurrentTick, Is.EqualTo(1));
            Assert.That(session.ReadTrace().Count, Is.EqualTo(traceCount));
            Assert.That(model.History.Count, Is.EqualTo(traceCount));
            Assert.That(model.Snapshot.Invariants.Evaluated, Is.True);
        }

        [Test]
        public void ResetChangesStreamClearsPanelAndLeavesOldSnapshotIntact()
        {
            GameplaySession session = new GameplaySession(); session.Start(new GameplayScenario()); session.Step();
            ReadOnlyDiagnosticsModel<GameplayObservation> model = new ReadOnlyDiagnosticsModel<GameplayObservation>(session.Diagnostics);
            model.Poll();
            DiagnosticSnapshot<GameplayObservation> old = model.Snapshot;
            session.Reset(new GameplayScenario());
            model.Poll();
            Assert.That(model.Snapshot.SessionId, Is.Not.EqualTo(old.SessionId));
            Assert.That(model.Snapshot.Tick, Is.Zero);
            Assert.That(model.Snapshot.Invariants.Evaluated, Is.False);
            Assert.That(model.StreamChanged, Is.True);
            Assert.That(model.History, Is.Empty);
            Assert.That(old.Tick, Is.EqualTo(1));
        }

        [Test]
        public void PanelHasItsOwnBoundAndReportsSourceGaps()
        {
            GameplaySession session = new GameplaySession(); session.Start(new GameplayScenario(traceCapacity: 2));
            ReadOnlyDiagnosticsModel<GameplayObservation> model = new ReadOnlyDiagnosticsModel<GameplayObservation>(session.Diagnostics, 1, 1);
            model.Poll();
            for (int i = 0; i < 5; i++) session.Step();
            long missed = session.Diagnostics.ReadTrace(default(TraceCursor), 1).MissedCount;
            Assert.That(missed, Is.GreaterThan(0));
            model.Poll();
            Assert.That(model.MissedCount, Is.EqualTo(missed));
            Assert.That(model.HasMore, Is.True);
            model.Poll();
            Assert.That(model.History.Count, Is.EqualTo(1));
            Assert.That(model.LocalEvictedCount, Is.EqualTo(1));
            Assert.That(model.MissedCount, Is.EqualTo(missed));
            Assert.That(model.HasMore, Is.False);
        }

        [Test]
        public void FailureDisplaysFaultWithoutClaimingRulesPassedAtFailedTick()
        {
            GameplaySession session = new GameplaySession();
            session.Start(new GameplayScenario(tickDelta: 2, speed: float.MaxValue));
            session.Step(); // tick 1 succeeds, no movement
            session.Submit(new GameplayRequest(session.Id, 1, 2, GameplayActionKind.Move, 1, x: 1));
            session.Step();
            DiagnosticSnapshot<GameplayObservation> snapshot = session.Diagnostics.ObserveDiagnostics();
            Assert.That(snapshot.State, Is.EqualTo(SessionState.Faulted));
            Assert.That(snapshot.FaultCode, Is.EqualTo("simulation.exception"));
            Assert.That(snapshot.Tick, Is.EqualTo(2));
            Assert.That(snapshot.Invariants.Tick, Is.EqualTo(1));
            Assert.That(snapshot.Invariants.Evaluated, Is.True);
        }

        [Test]
        public void InvariantViolationsRemainReadableAfterFault()
        {
            GameplaySession session = new GameplaySession();
            session.RegisterInvariant(() => new FailingCheck()); session.Start(new GameplayScenario()); session.Step();
            ReadOnlyDiagnosticsModel<GameplayObservation> model = new ReadOnlyDiagnosticsModel<GameplayObservation>(session.Diagnostics);
            model.Poll();
            Assert.That(model.Snapshot.Invariants.Violations[0].Code, Is.EqualTo("injected"));
            Assert.That(model.Snapshot.State, Is.EqualTo(SessionState.Faulted));
        }
        private sealed class CountingCheck : IInvariant<GameplayObservation>
        {
            private readonly Action count;
            public CountingCheck(Action count) { this.count = count; }
            public string Code => "counter";
            public InvariantViolation Evaluate(GameplayObservation observation) { count(); return null; }
        }
        private sealed class FailingCheck : IInvariant<GameplayObservation>
        {
            public string Code => "injected";
            public InvariantViolation Evaluate(GameplayObservation observation) => new InvariantViolation(Code, "test failure");
        }
    }
}
