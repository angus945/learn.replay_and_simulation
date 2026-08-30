using System;
using System.Linq;
using NUnit.Framework;
using Testability;

namespace GameplaySimulation.Tests
{
    public sealed class ToolControlTests
    {
        [Test]
        public void PortsDoNotExposeEachOthersAuthority()
        {
            GameplaySession session = new GameplaySession();
            Assert.That(session.Gameplay, Is.Not.InstanceOf<ITestSession<GameplayScenario>>());
            Assert.That(session.Gameplay, Is.Not.InstanceOf<ISimulationControl>());
            Assert.That(session.Simulation, Is.Not.InstanceOf<IGameplayControl>());
            Assert.That(session.Results, Is.Not.InstanceOf<IGameplayControl>());
            Assert.That(session.Admin, Is.Not.InstanceOf<ISimulationControl>());
            Assert.That(session.Diagnostics, Is.Not.InstanceOf<ISimulationControl>());
        }

        [Test]
        public void RealtimeClockCannotBeDrivenManuallyOrClaimedTwice()
        {
            GameplaySession session = new GameplaySession(SimulationDriveMode.Realtime);
            session.Admin.Start(new GameplayScenario());
            IRealtimeTickDriver clock = session.ClaimRealtimeDriver();
            Assert.Throws<InvalidOperationException>(() => session.Step());
            Assert.Throws<InvalidOperationException>(() => session.Simulation.Step());
            Assert.Throws<InvalidOperationException>(() => session.ClaimRealtimeDriver());
            Assert.That(session.CurrentTick, Is.Zero);
            clock.AdvanceTick();
            Assert.That(session.CurrentTick, Is.EqualTo(1));
            session.Admin.Reset(new GameplayScenario());
            Assert.Throws<InvalidOperationException>(() => session.ClaimRealtimeDriver());
            clock.AdvanceTick();
            Assert.That(session.CurrentTick, Is.EqualTo(1));
        }

        [Test]
        public void ManualClockCannotClaimRealtimeAuthority()
        {
            GameplaySession session = new GameplaySession();
            Assert.Throws<InvalidOperationException>(() => session.ClaimRealtimeDriver());
            session.Admin.Start(new GameplayScenario());
            session.Simulation.Step();
            Assert.That(session.CurrentTick, Is.EqualTo(1));
        }

        [Test]
        public void ResultsAreQueryableWithoutTraceAndPagesUseCompletionOrder()
        {
            GameplaySession session = new GameplaySession();
            session.Admin.Start(new GameplayScenario(traceCapacity: 1));
            session.Gameplay.Submit(new GameplayRequest(session.Id, 20, 1, GameplayActionKind.Move, 1));
            session.Gameplay.Submit(new GameplayRequest(session.Id, 10, 2, GameplayActionKind.Move, 1));
            Assert.That(session.Results.Find(session.Id, 20).State, Is.EqualTo(ActionLookupState.Pending));
            session.Simulation.Step(); session.Simulation.Step();
            ActionResultPage first = session.Results.Read(session.Id, 0, 1);
            ActionResultPage second = session.Results.Read(session.Id, first.NextIndex, 1);
            Assert.That(first.Items[0].Sequence, Is.EqualTo(20));
            Assert.That(first.HasMore, Is.True);
            Assert.That(second.Items[0].Sequence, Is.EqualTo(10));
            Assert.That(second.HasMore, Is.False);
            Assert.That(session.Results.Find(session.Id, 20).Result.Code, Is.EqualTo("move.applied"));
            Assert.That(session.Results.Find(session.Id, 999).State, Is.EqualTo(ActionLookupState.Unknown));
            Assert.That(session.CurrentTick, Is.EqualTo(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Results.Read(session.Id, 0, 1025));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Results.Read(session.Id, 3, 1));
        }

        [Test]
        public void StopCancelsPendingAndResetInvalidatesResultsAndCursors()
        {
            GameplaySession session = new GameplaySession();
            session.Start(new GameplayScenario());
            string old = session.Id;
            session.Submit(new GameplayRequest(old, 1, 2, GameplayActionKind.Move, 1));
            session.Admin.Stop();
            Assert.That(session.Results.Find(old, 1).State, Is.EqualTo(ActionLookupState.Cancelled));
            session.Admin.Reset(new GameplayScenario());
            Assert.That(session.Results.Find(old, 1).State, Is.EqualTo(ActionLookupState.StaleSession));
            Assert.Throws<InvalidOperationException>(() => session.Results.Read(old, 0, 1));
        }

        [Test]
        public void CapabilitiesDescribeLifecycleModeAndActionCatalog()
        {
            GameplaySession session = new GameplaySession();
            Assert.That(session.Capabilities.Describe().CanStep, Is.False);
            session.Start(new GameplayScenario(maxActions: 3));
            GameplayCapabilities snapshot = session.Capabilities.Describe();
            Assert.That(snapshot.CanStep, Is.True);
            Assert.That(snapshot.Scenario.MaxActions, Is.EqualTo(3));
            Assert.That(snapshot.Actions.Select(item => item.Kind), Is.EqualTo(new[] { GameplayActionKind.Move, GameplayActionKind.Attack }));
            Assert.That(snapshot.Actions[1].RequiresTarget, Is.True);
            Assert.That(snapshot.SupportsRemoteProtocol, Is.False);
            session.Stop();
            Assert.That(snapshot.CanSubmit, Is.True);
            Assert.That(session.Capabilities.Describe().CanSubmit, Is.False);
        }

        [Test]
        public void RerunExplainsHashResultAndFailureDifferences()
        {
            FailureArtifact original = Capture();
            FailureArtifact changed = new FailureArtifact(original.SessionId, original.Scenario, original.FailureTick, 42,
                "changed", null, original.Actions, new[] { new ActionResult(1, 1, ActionStatus.Rejected, "changed") },
                new[] { new HashCheckpoint(0, "changed") }, original.Trace, 0, diagnosticPolicy: original.DiagnosticPolicy);
            RerunReport report = FailureRerun.Compare(changed);
            Assert.That(report.Executed, Is.True);
            Assert.That(report.Matches, Is.False);
            Assert.That(report.FirstDivergentTick, Is.EqualTo(0));
            Assert.That(report.Differences.Select(item => item.Category), Does.Contain("action_result"));
            Assert.That(report.Differences.Select(item => item.Category), Does.Contain("failure.exception_type"));
            Assert.That(report.Differences.Select(item => item.Category), Does.Contain("failure.action"));
        }

        [Test]
        public void RerunReportsPolicyMismatchAndBuildWarning()
        {
            FailureArtifact original = Capture();
            RerunReport matched = FailureRerun.Compare(original, currentBuild: "different-build");
            Assert.That(matched.Matches, Is.True);
            Assert.That(matched.Warnings.Any(item => item.StartsWith("build.mismatch")), Is.True);
            RerunReport mismatch = FailureRerun.Compare(original, new GameplaySession(policyRevision: "v2"));
            Assert.That(mismatch.Matches, Is.False);
            Assert.That(mismatch.Differences.Any(item => item.Category == "policy"), Is.True);
        }

        [Test]
        public void RerunRejectsUnsupportedArtifactAndNonFreshSessionWithoutStepping()
        {
            Assert.That(FailureRerun.Compare(null).Differences[0].Category, Is.EqualTo("schema"));
            GameplaySession used = new GameplaySession();
            used.Start(new GameplayScenario());
            Assert.That(FailureRerun.Compare(Capture(), used).Executed, Is.False);
            Assert.That(used.CurrentTick, Is.Zero);
        }

        [Test]
        public void FaultCancelsFutureActionsButPreservesCompletedResults()
        {
            GameplaySession session = new GameplaySession();
            session.Start(new GameplayScenario(tickDelta: 2, speed: float.MaxValue));
            session.Submit(new GameplayRequest(session.Id, 1, 1, GameplayActionKind.Move, 1, x: 1));
            session.Submit(new GameplayRequest(session.Id, 2, 2, GameplayActionKind.Move, 1));
            session.Step();
            Assert.That(session.Results.Find(session.Id, 1).State, Is.EqualTo(ActionLookupState.Completed));
            Assert.That(session.Results.Find(session.Id, 2).State, Is.EqualTo(ActionLookupState.Cancelled));
        }

        private static FailureArtifact Capture()
        {
            GameplaySession session = new GameplaySession();
            session.Start(new GameplayScenario(tickDelta: 2, speed: float.MaxValue));
            session.Submit(new GameplayRequest(session.Id, 1, 1, GameplayActionKind.Move, 1, x: 1));
            session.Step();
            return session.Failure;
        }
    }
}
