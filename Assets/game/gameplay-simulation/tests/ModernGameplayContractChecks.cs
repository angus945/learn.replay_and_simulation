using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CharacterMovement.Domain;
using CharacterMovement.Integration;
using InvariantChecks;
using MovementDemo;
using Testability;
using Testability.Templates;
using ModernSession = Testability.Templates.TestableSimulationSession<GameplaySimulation.GameplayWorld, GameplaySimulation.GameplayScenario, GameplaySimulation.GameplayInput, GameplaySimulation.GameplayObservation>;
using ModernReplay = Testability.Templates.TemplateReplay<GameplaySimulation.GameplayWorld, GameplaySimulation.GameplayScenario, GameplaySimulation.GameplayInput, GameplaySimulation.GameplayObservation>;

namespace GameplaySimulation.Tests
{
    /// <summary>Game behavior checks shared by NUnit and the headless host, through the modern public boundary.</summary>
    public static class ModernGameplayContractChecks
    {
        public static void ScenarioLimits()
        {
            GameplayScenario original = new GameplayScenario(includeEnemy: false, maxTicks: 4, maxActions: 4, traceCapacity: 64);
            GameplayScenario scenario = new GameplayScenario(includeEnemy: false, maxTicks: 1, maxActions: 1, traceCapacity: 16);
            GameplayDefinition definition = new GameplayDefinition();
            using (ModernSession session = definition.CreateTestSession(original))
            {
                Check(session.Limits.MaxTicks == 4 && session.Limits.MaxInputs == 4 && session.Limits.TraceCapacity == 64,
                    "Initial scenario budgets were replaced by template defaults.");
                session.Admin.Reset(scenario);
                ulong player = session.Observe().PlayerId;
                Check(session.Limits.MaxTicks == 1 && session.Limits.MaxInputs == 1 && session.Limits.TraceCapacity == 16,
                    "Reset retained the previous scenario's default budgets.");
                Check(session.Gameplay.Submit(session.Id, 1, 2, new GameplayInput(GameplayActionKind.Move, player, x: 1)).Code == "tick.out_of_range",
                    "Input exceeded the scenario tick budget.");
                Check(session.Gameplay.Submit(session.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, player, x: 1)).Queued,
                    "Valid input was not admitted after a rejected submission.");
                Check(session.Gameplay.Submit(session.Id, 2, 1, new GameplayInput(GameplayActionKind.Move, player)).Code == "input.capacity",
                    "Input exceeded the scenario action budget.");
                session.Simulation.Step();
                Expect<InvalidOperationException>(() => session.Simulation.Step());
                Check(session.CurrentTick == 1 && session.State == SessionState.Stopped, "Tick budget did not stop the session at its boundary.");
                TemplateRecording recording = RoundTrip(session.CaptureRecording());
                Check(recording.Limits.MaxTicks == 1 && recording.Inputs.Count == 1, "Recording did not retain effective run limits.");
                using (ModernReplay replay = definition.CreateReplay(recording))
                { replay.Step(); Check(replay.State == TemplateReplayState.Completed, "Budget-limited recording could not replay."); }
            }
            TemplateLimits explicitLimits = new TemplateLimits(maxTicks: 4, maxInputs: 3, traceCapacity: 64);
            using (ModernSession session = definition.CreateTestSession(original, explicitLimits))
            {
                session.Admin.Reset(scenario);
                Check(ReferenceEquals(session.Limits, explicitLimits), "Reset replaced explicit run limits with scenario defaults.");
                ulong player = session.Observe().PlayerId;
                Check(session.Gameplay.Submit(session.Id, 1, 1, new GameplayInput(GameplayActionKind.Move, player, x: 1)).Queued &&
                    session.Gameplay.Submit(session.Id, 2, 2, new GameplayInput(GameplayActionKind.Move, player)).Queued,
                    "Reset ignored explicit tick or input budgets.");
                session.Simulation.Step(); session.Simulation.Step();
                Check(session.CurrentTick == 2 && session.State == SessionState.Running &&
                    session.Results.Find(session.Id, 2).State == "Completed", "Explicit limits did not govern the new world.");
                TemplateRecording recording = RoundTrip(session.CaptureRecording());
                Check(recording.Limits.MaxTicks == 4 && recording.Limits.MaxInputs == 3,
                    "Reset recording lost explicit run limits.");
                using (ModernReplay replay = definition.CreateReplay(recording))
                { replay.Step(); replay.Step(); Check(replay.State == TemplateReplayState.Completed, "Reset override recording could not replay."); }
            }
        }

        public static void CustomInvariantIsolationAndPolicy()
        {
            Func<IInvariant<GameplayObservation>> factory = () => new EvaluationWindow();
            Expect<ArgumentException>(() => new GameplayDefinition(new[] { factory }));
            List<Func<IInvariant<GameplayObservation>>> factories = new List<Func<IInvariant<GameplayObservation>>> { factory };
            GameplayDefinition definition = new GameplayDefinition(factories, "modern-checks/evaluation-window-v1");
            factories.Clear(); // The caller cannot later change the definition's composition.
            GameplayScenario scenario = new GameplayScenario(includeEnemy: false);
            using (ModernSession first = definition.CreateTestSession(scenario))
            using (ModernSession second = definition.CreateTestSession(scenario))
            {
                Check(first.InvariantReport.CheckCount == 2 && second.InvariantReport.CheckCount == 2, "Custom oracle composition was not captured.");
                first.Simulation.Step(); second.Simulation.Step();
                Check(first.State == SessionState.Running && second.State == SessionState.Running,
                    "Sessions shared a stateful invariant instance.");
                first.Simulation.Step();
                Check(first.Failure != null && first.Failure.Tick == 2 && first.Failure.Code == "test.evaluation_window" &&
                    first.Failure.Stage == "Invariant" && second.State == SessionState.Running && second.CurrentTick == 1,
                    "Custom invariant failure was not isolated to the intended session.");
                TemplateRecording failure = RoundTrip(first.CaptureRecording());
                using (ModernReplay replay = definition.CreateReplay(failure))
                { replay.Step(); replay.Step(); Check(replay.State == TemplateReplayState.ReproducedFailure, "Custom oracle failure did not reproduce."); }
                using (ModernReplay incompatible = new GameplayDefinition().CreateReplay(failure))
                {
                    Check(incompatible.State == TemplateReplayState.Diverged && incompatible.FirstDifference.Category == "policy" && incompatible.CurrentTick == 0,
                        "Missing custom oracle policy was silently accepted.");
                }
                string previousId = first.Id;
                first.Admin.Reset(scenario); first.Simulation.Step();
                Check(first.Id != previousId && first.State == SessionState.Running && first.Failure == null,
                    "Reset reused custom invariant state or failure evidence.");
            }
        }

        public static void EventCausation()
        {
            GameplayDefinition definition = new GameplayDefinition();
            GameplayScenario scenario = new GameplayScenario(health: 10, damage: 10, traceCapacity: 128);
            using (ModernSession session = definition.CreateTestSession(scenario))
            {
                GameplayObservation initial = session.Observe();
                ulong player = initial.PlayerId;
                ulong enemy = initial.Actors.Single(actor => actor.Id != player && actor.Active).Id;
                session.Gameplay.Submit(session.Id, 41, 1, new GameplayInput(GameplayActionKind.Attack, player, enemy));
                session.Gameplay.Submit(session.Id, 42, 1, new GameplayInput(GameplayActionKind.Move, enemy, x: 1));
                TemplateTick tick = session.Simulation.Step();
                Check(tick.Results[0].Status == ActionStatus.Accepted && tick.Results[1].Code == "actor.dead",
                    "Same-tick commands did not observe the attack's domain result.");
                Check(!session.Observe().FindActor(enemy).Active, "Death did not commit the target lifecycle.");
                TemplateRecording recording = RoundTrip(session.CaptureRecording());
                Check(recording.Inputs.Count == 2, "Internal damage/death facts were recorded as external inputs.");
                foreach (string factType in new[] { "ActorDamaged", "ActorDied" })
                {
                    TraceEntry fact = recording.Trace.Single(entry => entry.Stage == "DomainEvent" && entry.Type == factType);
                    Check(fact.Sequence == 41 && fact.Actor == player && fact.Target == enemy, "Event lost input causation: " + factType);
                }
                TraceEntry damage = recording.Trace.Single(entry => entry.Type == "ActorDamaged");
                Check(damage.Code == "damage=10", "Damage diagnostic detail was lost.");
                using (ModernReplay replay = definition.CreateReplay(recording))
                { replay.Step(); Check(replay.State == TemplateReplayState.Completed && !replay.Observe().FindActor(enemy).Active, "Combat recording did not replay exactly once."); }
            }
        }

        public static void DiagonalMovementAndReplay()
        {
            GameplayScenario scenario = new GameplayScenario(tickDelta: .125f, speed: 4, includeEnemy: false);
            GameplayDefinition definition = new GameplayDefinition();
            float[,] directions = { { 1, 1 }, { 1, 1 }, { -1, 1 }, { -1, -1 }, { .25f, -.5f }, { 0, 0 }, { 0, 0 } };
            using (ModernSession session = definition.CreateTestSession(scenario))
            using (MovementDemoSession demo = new MovementDemoSession(new View(), speed: 4, tickDeltaTime: .125f, includeEnemy: false))
            {
                ActorObservation stoppedAt = null;
                for (int index = 0; index < directions.GetLength(0); index++)
                {
                    ulong tick = (ulong)index + 1;
                    float x = directions[index, 0];
                    float y = directions[index, 1];
                    session.Gameplay.Submit(session.Id, tick, tick, new GameplayInput(GameplayActionKind.Move, session.Observe().PlayerId, x: x, y: y));
                    TemplateTick expected = session.Simulation.Step();
                    demo.CaptureAxes(x, y);
                    demo.AdvanceTime(.03125f);
                    Check(demo.TickNumber == tick - 1, "A partial rendered frame advanced a full gameplay tick.");
                    demo.AdvanceTime(.09375f);
                    TemplateRecording observed = demo.CaptureReplay();
                    Check(demo.TickNumber == tick && observed.Ticks[index].Hash == expected.Hash,
                        "Demo and formal session disagree for nonzero movement at tick " + tick);
                    ActorObservation player = session.Observe().FindActor(session.Observe().PlayerId);
                    if (index == 0)
                    {
                        double distance = Math.Sqrt((double)player.X * player.X + (double)player.Y * player.Y);
                        Check(player.X > 0 && player.Y > 0 && Math.Abs(player.X - player.Y) < .000001 && Math.Abs(distance - .5) < .000001,
                            "Diagonal movement was zero, unnormalized or used the wrong fixed delta.");
                    }
                    if (index == 4) stoppedAt = player;
                    if (index > 4) Check(player.X == stoppedAt.X && player.Y == stoppedAt.Y, "Zero input did not stop movement.");
                }
                TemplateRecording recording = RoundTrip(demo.CaptureReplay());
                foreach (float frameDelta in new[] { 1f / 30, 1f / 60, 1f / 144, .37f })
                {
                    using (ModernReplay replay = definition.CreateReplay(recording))
                    {
                        replay.Play();
                        for (int frame = 0; frame < 10000 && replay.State == TemplateReplayState.Playing; frame++) replay.AdvanceTime(frameDelta);
                        Check(replay.State == TemplateReplayState.Completed && replay.FirstDifference == null,
                            "Diagonal recording diverged with frame delta " + frameDelta);
                    }
                }
                Check(demo.TickNumber == (ulong)directions.GetLength(0) && session.CurrentTick == demo.TickNumber,
                    "Replay advanced a live session.");
            }
        }

        private sealed class EvaluationWindow : IInvariant<GameplayObservation>
        {
            private int evaluations;
            public string Code => "test.evaluation_window";
            public InvariantViolation Evaluate(GameplayObservation observation)
                => ++evaluations > 1 ? new InvariantViolation(Code, "Deliberate second-evaluation failure for session isolation checks.") : null;
        }
        private sealed class View : ICharacterMovementView
        {
            public void SetPosition(MovementPosition position) { }
        }
        private static TemplateRecording RoundTrip(TemplateRecording recording)
        {
            using (MemoryStream stream = new MemoryStream())
            { TemplateRecordingIO.Write(stream, recording); stream.Position = 0; return TemplateRecordingIO.Read(stream); }
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Modern gameplay: " + message); }
        private static void Expect<TException>(Action action) where TException : Exception
        {
            try { action(); }
            catch (TException) { return; }
            throw new InvalidOperationException("Modern gameplay: expected " + typeof(TException).Name);
        }
    }
}
