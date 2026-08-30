using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arena.Application;
using Arena.Composition;
using Arena.Domain;
using Arena.Infrastructure;
using Arena.Integration;
using Testability;
using Testability.Templates;
using TraceBuffering;
using Session = Testability.Templates.TestableSimulationSession<Arena.Integration.ArenaRuntime, Arena.Integration.ArenaScenario, Arena.Integration.ArenaInput, Arena.Integration.ArenaObservation>;
using Playback = Testability.Templates.TemplateReplay<Arena.Integration.ArenaRuntime, Arena.Integration.ArenaScenario, Arena.Integration.ArenaInput, Arena.Integration.ArenaObservation>;

namespace Arena.Tests
{
    /// <summary>Executable chapter examples; shared by NUnit and the headless host, never by gameplay.</summary>
    public static class ArenaContractChecks
    {
        public static void Domain()
        {
            Actor actor = new Actor(new ActorId(1), ActorKind.Player, new Position(0, 0), 4, 30);
            actor.SetDirection(1, 0); Require(actor.Position.X == 0, "Direction is not time advancement.");
            actor.Advance(.25f); Require(actor.Position.X == 1, "Domain movement.");
            actor.SetDirection(1, 1); actor.Advance(.25f);
            Require(Math.Abs(actor.Direction.X * actor.Direction.X + actor.Direction.Y * actor.Direction.Y - 1) < .00001, "Unit diagonal.");
            Expect<ArgumentOutOfRangeException>(() => actor.SetDirection(float.NaN, 0));
            Require(actor.TakeDamage(100) == 30 && actor.IsDead && actor.Direction.X == 0, "Death clamps and stops.");
            float x = actor.Position.X; actor.Advance(1); Require(actor.Position.X == x, "Dead actor cannot advance.");
        }
        public static void Application()
        {
            ActorRepository repository = new ActorRepository();
            RegistryLifecycle lifetime = new RegistryLifecycle(repository);
            ArenaApplication app = new ArenaApplication(repository, lifetime, new SpawnRandom(42), new ArenaRules(damage: 100, respawnMinTicks: 2, respawnMaxTicks: 2));
            ArenaResult result = app.Execute(new ArenaRequest(ArenaAction.Attack, app.PlayerId, new ActorId(2)));
            Require(result.Code == "defeated" && result.Facts.Count == 2, "Use case returns facts.");
            Require(lifetime.IsActive(new ActorId(2)), "A domain fact alone does not commit lifetime.");
            app.OnDefeated(new ActorId(2)); app.ScheduleRespawn(1); app.Advance(1, .25f); app.Commit(1);
            Require(app.Actors.Count == 1 && app.PendingRespawnTicks.Single() == 3, "Application owns due tick.");
            app.Advance(2, .25f); app.Commit(2); app.Advance(3, .25f); app.Commit(3);
            Require(app.Actors.Count == 2 && app.LastActorId == 3, "Fresh identity, same application.");
        }
        public static void Simulation()
        {
            using (Session session = new ArenaDefinition().CreateTestSession(new ArenaScenario(tickDelta: .25f)))
            {
                Submit(session, 1, 1, new ArenaInput(ArenaAction.Move, 1, x: 1));
                Require(session.Observe().FindActor(1).X == 0, "Submit cannot run Domain.");
                session.Step(); Require(session.Observe().FindActor(1).X == 1, "PrePhysics calls Application.");
                session.Step(); Require(session.Observe().FindActor(1).X == 2, "Fixed delta persists.");
                string id = session.Id; session.Reset(new ArenaScenario(tickDelta: .125f));
                Require(id != session.Id && session.CurrentTick == 0 && session.Observe().FindActor(1).X == 0, "Fresh reset.");
                ArenaScenario malformed = ArenaCodecs.Decode<ArenaScenario>("{\"TickDelta\":-1}");
                string survivingId = session.Id;
                Expect<ArgumentException>(() => session.Reset(malformed));
                Require(session.State == SessionState.Running && session.Id == survivingId && session.CurrentTick == 0,
                    "A malformed deserialized scenario must not destroy the current run during Reset.");
                session.Step(); Require(session.CurrentTick == 1, "The surviving session remains usable.");
            }
        }
        public static void Input()
        {
            using (Session session = new ArenaDefinition().CreateTestSession(new ArenaScenario(tickDelta: .25f, maxInputs: 4)))
            {
                Submit(session, 3, 2, new ArenaInput(ArenaAction.Move, 999, x: 1));
                Submit(session, 2, 2, new ArenaInput(ArenaAction.Move, 1, x: 0));
                Submit(session, 1, 2, new ArenaInput(ArenaAction.Move, 1, x: 1));
                Require(!session.Submit(session.Id, 1, 2, new ArenaInput(ArenaAction.Move, 1)).Queued, "Duplicate sequence.");
                Require(!session.Submit("stale", 4, 2, new ArenaInput(ArenaAction.Move, 1)).Queued, "Stale identity.");
                Require(session.Step().Results.Count == 0, "Future tick.");
                TemplateTick second = session.Step();
                Require(second.Results.Select(item => item.Sequence).SequenceEqual(new ulong[] { 1, 2, 3 }), "Sequence order, not arrival order.");
                Require(second.Results[2].Status == ActionStatus.Rejected && session.Observe().FindActor(1).X == 0, "Queued is not Accepted.");
                Require(session.Results.Read(session.Id, 0, 2).HasMore && session.Results.Find(session.Id, 3).State == "Completed", "Result paging.");
                Submit(session, 4, 4, new ArenaInput(ArenaAction.Move, 1));
                Require(!session.Submit(session.Id, 5, 5, new ArenaInput(ArenaAction.Move, 1)).Queued, "Input budget.");
                session.Stop(); Require(session.Results.Find(session.Id, 4).State == "Cancelled", "Stop cancels future input.");
            }
        }
        public static void Lifecycle()
        {
            using (Session session = new ArenaDefinition().CreateTestSession(new ArenaScenario(damage: 100, respawnMinTicks: 2, respawnMaxTicks: 2, maxEnemySpawns: 2)))
            {
                ulong random = session.Observe().HealthRandomState;
                Submit(session, 1, 1, new ArenaInput(ArenaAction.Attack, 1, 2));
                Submit(session, 2, 1, new ArenaInput(ArenaAction.Move, 2, x: 1));
                TemplateTick first = session.Step();
                Require(first.Results[1].Code == "actor-dead", "Death prevents later same-tick action.");
                Require(session.Observe().FindActor(2) == null && session.Observe().PendingRespawnTicks.Single() == 3, "Destruction and schedule.");
                Require(session.Observe().HealthRandomState == random, "Delay scheduling does not draw health stream.");
                session.Step(); session.Step();
                Require(session.Observe().FindActor(3) != null && session.Observe().EnemiesSpawned == 2, "New identity at due tick.");
                ulong delay = session.Observe().DelayRandomState;
                Submit(session, 3, 4, new ArenaInput(ArenaAction.Attack, 1, 3)); session.Step();
                Require(session.Observe().PendingRespawnTicks.Count == 0 && session.Observe().DelayRandomState == delay, "Budget refusal does not consume RNG.");
                TraceEntry[] trace = session.CaptureRecording().Trace.ToArray();
                Require(trace.Any(entry => entry.Type == "Defeated" && entry.Sequence == 1 && entry.Target == 2), "Domain fact causation.");
                Require(trace.Any(entry => entry.Type == "ScheduleRespawn" && entry.Sequence == 1), "Event to internal command reaction.");
                Require(session.CaptureRecording().Inputs.Count == 3, "Internal commands are not external recording inputs.");
            }
        }
        public static void Observation()
        {
            using (Session first = new ArenaDefinition().CreateTestSession(new ArenaScenario(tickDelta: .25f)))
            using (Session second = new ArenaDefinition().CreateTestSession(new ArenaScenario(tickDelta: .25f)))
            {
                ArenaObservation before = first.Observe(); byte[] bytes = ArenaCanonicalState.Encode(before);
                Submit(first, 1, 1, new ArenaInput(ArenaAction.Move, 1, x: 1)); first.Step(); second.Step();
                Require(before.FindActor(1).X == 0 && bytes.SequenceEqual(ArenaCanonicalState.Encode(before)), "Observation is detached.");
                Require(second.Observe().FindActor(1).X == 0, "Session isolation.");
                Require(!ArenaCanonicalState.Encode(first.Observe()).SequenceEqual(ArenaCanonicalState.Encode(second.Observe())), "Game state affects canonical bytes.");
                Require(before.RegistryEvidence.Count > 0 && before.LastActorId == 2, "Identity allocation evidence.");
            }
            using (Session first = new ArenaDefinition().CreateTestSession(new ArenaScenario(damage: 10)))
            using (Session second = new ArenaDefinition().CreateTestSession(new ArenaScenario(damage: 20)))
                Require(first.CaptureRecording().InitialHash != second.CaptureRecording().InitialHash, "Future-affecting immutable rules are part of canonical state.");
        }
        public static void Diagnostics()
        {
            using (Session session = new ArenaDefinition().CreateTestSession(new ArenaScenario(traceCapacity: 8)))
            {
                Require(!session.Diagnostics.ObserveDiagnostics().Invariants.Evaluated, "Initial checks are not yet evaluated.");
                session.Step();
                TraceBatch<TraceEntry> page = session.Diagnostics.ReadTrace(default, 256);
                ulong tick = session.CurrentTick;
                session.Diagnostics.ObserveDiagnostics();
                Require(session.CurrentTick == tick && session.Diagnostics.ReadTrace(page.NextCursor, 256).Items.Count == 0, "Read-only polling.");
                Require(page.OverwrittenCount > 0, "Bounded trace reports overwritten records.");
                Require(!(session.Diagnostics is ITemplateSimulation), "Reader does not expose drive port.");
                session.Reset(new ArenaScenario());
                Require(session.Diagnostics.ReadTrace(page.NextCursor, 256).StreamChanged, "Reset changes trace stream.");
            }
            using (Session failure = new ArenaDefinition(true).CreateTestSession(new ArenaScenario(tickDelta: .25f)))
            {
                Submit(failure, 1, 1, new ArenaInput(ArenaAction.Move, 1, x: 1)); failure.Step(); failure.Step();
                Require(failure.State == SessionState.Faulted && failure.Failure.Tick == 2 && failure.LastCompletedTick == 1, "Oracle failure evidence.");
                Require(failure.Diagnostics.ObserveDiagnostics().ObservationTick == 2, "Post-tick oracle captured this tick.");
            }
            using (Session failure = new ArenaDefinition().CreateTestSession(new ArenaScenario(tickDelta: 1, speed: float.MaxValue)))
            {
                Submit(failure, 1, 1, new ArenaInput(ArenaAction.Move, 1, x: 1)); failure.Step(); failure.Step();
                Require(failure.Failure != null && failure.Diagnostics.ObserveDiagnostics().ObservationTick == 1, "Exception keeps last captured observation.");
                using (Playback replay = new ArenaDefinition().CreateReplay(RoundTrip(failure.CaptureRecording())))
                { while (replay.State == TemplateReplayState.Paused) replay.Step(); Require(replay.State == TemplateReplayState.ReproducedFailure, "Exception replay."); }
            }
        }
        public static void Replay()
        {
            TemplateRecording recording = CreateRecording(false);
            foreach (float frame in new float[] { 1f / 30, 1f / 144, .37f })
            {
                using (Playback replay = new ArenaDefinition().CreateReplay(RoundTrip(recording)))
                {
                    replay.Play(); int guard = 10000;
                    while (replay.State == TemplateReplayState.Playing && guard-- > 0) replay.AdvanceTime(frame);
                    Require(replay.State == TemplateReplayState.Completed && replay.FirstDifference == null, "Replay frame schedule.");
                    replay.Restart(); Require(replay.CurrentTick == 0, "Restart reconstructs.");
                    replay.Play(); replay.Pause(); replay.Step(); Require(replay.CurrentTick == 1, "Pause and single step.");
                }
            }
            using (Playback failure = new ArenaDefinition(true).CreateReplay(RoundTrip(CreateRecording(true))))
            { failure.Step(); failure.Step(); Require(failure.State == TemplateReplayState.ReproducedFailure, "Non-crash failure replay."); }
            List<TemplateTick> changed = new List<TemplateTick>(recording.Ticks);
            changed[0] = new TemplateTick(1, "changed", changed[0].Results);
            using (Playback replay = new ArenaDefinition().CreateReplay(Copy(recording, ticks: changed)))
            { replay.Step(); Require(replay.FirstDifference.Tick == 1 && replay.FirstDifference.Category == "state_hash", "First hash difference."); }
            changed = new List<TemplateTick>(recording.Ticks);
            ActionResult old = changed[0].Results[0];
            changed[0] = new TemplateTick(1, changed[0].Hash, new[] { new ActionResult(old.Sequence, old.Tick, old.Status, "changed") });
            using (Playback replay = new ArenaDefinition().CreateReplay(Copy(recording, ticks: changed)))
            { replay.Step(); Require(replay.FirstDifference.Category == "action_result", "Result difference."); }
            using (Playback replay = new ArenaDefinition().CreateReplay(Copy(recording, policy: "other")))
                Require(replay.FirstDifference.Tick == 0 && replay.FirstDifference.Category == "policy", "Policy gate.");
        }
        public static void Realtime()
        {
            using (ArenaLiveSession stopped = new ArenaLiveSession(new ArenaScenario(tickDelta: .25f, maxTicks: 1)))
            {
                stopped.CaptureAxes(1, 0); stopped.AdvanceTime(.25f);
                Require(stopped.State == SessionState.Stopped && stopped.Observe().FindActor(1).X == 1 && stopped.PresentationAlpha == 1,
                    "Tick budget termination presents the latest state, not the penultimate state.");
            }
            using (ArenaLiveSession stopped = new ArenaLiveSession(new ArenaScenario(tickDelta: .25f)))
            {
                stopped.CaptureAxes(1, 0); stopped.AdvanceTime(.25f); stopped.Stop();
                Require(stopped.PresentationAlpha == 1, "Explicit stop snaps the final observation.");
                Expect<InvalidOperationException>(() => System.Threading.Tasks.Task.Run(() => stopped.CaptureAttack(true)).GetAwaiter().GetResult());
            }
            using (ArenaLiveSession faulted = new ArenaLiveSession(new ArenaScenario(tickDelta: 1, speed: float.MaxValue)))
            {
                faulted.CaptureAxes(1, 0); faulted.AdvanceTime(2);
                Require(faulted.State == SessionState.Faulted && faulted.PresentationAlpha == 1 && faulted.Observe().Tick == 1,
                    "Failure presents the last captured observation without interpolating backwards.");
            }
            ArenaLiveSession disposed = new ArenaLiveSession(); disposed.Dispose();
            Expect<ObjectDisposedException>(() => disposed.CaptureAxes(0, 0));
            Expect<ObjectDisposedException>(() => disposed.CaptureAttack(true));
            Expect<ObjectDisposedException>(() => disposed.ClearInput());
            using (ArenaLiveSession live = new ArenaLiveSession(new ArenaScenario(tickDelta: .25f)))
            {
                int health = live.Observe().FindActor(2).Health;
                live.CaptureAttack(true); live.CaptureAttack(false); // A tap entirely between ticks is retained.
                live.AdvanceTime(.75f);
                Require(live.TickNumber == 3 && live.PreviousObservation.Tick == 2, "Catchup captures penultimate tick.");
                Require(live.Observe().FindActor(2).Health == health - 10, "Pressed consumed once across catchup ticks.");
                live.Pause(); live.AdvanceTime(10); Require(live.TickNumber == 3, "Pause does not advance.");
                live.Resume(); live.ClearInput(); live.CaptureAttack(true); live.CaptureAttack(false);
                live.CaptureAxes(1, 0); live.AdvanceTime(.25f); live.UpdatePresentation();
                Require(live.Observe().FindActor(1).X == 1, "Resume and input.");
                Require(live.CaptureRecording().Ticks[3].Results.Count == 2, "Clearing old input must not swallow a new press.");
                using (Playback replay = new ArenaDefinition().CreateReplay(live.CaptureRecording()))
                { while (replay.State == TemplateReplayState.Paused) replay.Step(); Require(replay.State == TemplateReplayState.Completed && live.TickNumber == 4, "Replay never advances live."); }
            }
            using (ArenaLiveSession cleared = new ArenaLiveSession(new ArenaScenario(tickDelta: .25f, enemyHealthMin: 40, enemyHealthMax: 40)))
            {
                int health = cleared.Observe().FindActor(2).Health;
                cleared.CaptureAxes(1, 0); cleared.CaptureAttack(true); cleared.CaptureAttack(false);
                cleared.ClearInput(); cleared.ClearInput(); cleared.ClearInput();
                cleared.CaptureAttack(true); cleared.CaptureAttack(false);
                cleared.AdvanceTime(.75f);
                TemplateRecording recording = cleared.CaptureRecording();
                Require(cleared.Observe().FindActor(1).X == 0 && cleared.Observe().FindActor(2).Health == health - 10,
                    "Repeated clears discard stale input without swallowing a subsequently captured press.");
                Require(recording.Ticks[0].Results.Count == 2 && recording.Ticks[1].Results.Count == 1 && recording.Ticks[2].Results.Count == 1,
                    "A new press after repeated clears executes only once across catchup ticks.");
            }
            using (ArenaLiveSession held = new ArenaLiveSession(new ArenaScenario(tickDelta: .25f, enemyHealthMin: 40, enemyHealthMax: 40)))
            {
                held.CaptureAxes(1, 0); held.CaptureAttack(true); held.AdvanceTime(.25f);
                float x = held.Observe().FindActor(1).X;
                int health = held.Observe().FindActor(2).Health;
                // Consuming a tick clears edges, but held axes/button state still needs clearing.
                held.ClearInput(); held.ClearInput(); held.AdvanceTime(.25f);
                Require(held.Observe().FindActor(1).X == x && held.Observe().FindActor(2).Health == health &&
                    held.CaptureRecording().Ticks[1].Results.Count == 1,
                    "Clearing previously consumed held input stops movement and does not produce another attack.");
                held.CaptureAttack(true); held.AdvanceTime(.25f);
                Require(held.Observe().FindActor(2).Health == health - 10,
                    "Clearing held input resets button-down state so a fresh press does not require a synthetic release.");
            }
            using (ArenaLiveSession clean = new ArenaLiveSession())
            {
                Expect<InvalidOperationException>(() => System.Threading.Tasks.Task.Run(() => clean.ClearInput()).GetAwaiter().GetResult());
                clean.CaptureAxes(1, 0); clean.ClearInput();
                Expect<InvalidOperationException>(() => System.Threading.Tasks.Task.Run(() => clean.ClearInput()).GetAwaiter().GetResult());
                Require(clean.TickNumber == 0 && clean.CaptureRecording().Inputs.Count == 0,
                    "The idempotent ClearInput fast path still enforces owner-thread access without advancing or submitting input.");
            }
            using (Session session = new ArenaDefinition().CreateTestSession(new ArenaScenario()))
            {
                using (DeterministicSimulation.Framework.RealtimeSimulationRunner runner = session.CreateRealtimeRunner())
                {
                    Expect<InvalidOperationException>(() => session.Step());
                    Expect<InvalidOperationException>(() => session.Reset(new ArenaScenario()));
                    runner.Pause(); Expect<InvalidOperationException>(() => session.Step());
                }
                session.Step(); Require(session.CurrentTick == 1, "Disposed runner releases drive ownership.");
            }
        }
        public static TemplateRecording CreateRecording(bool failure)
        {
            using (Session session = new ArenaDefinition(failure).CreateTestSession(new ArenaScenario(tickDelta: .25f, damage: 100, respawnMinTicks: 2, respawnMaxTicks: 4)))
            {
                Submit(session, 1, 1, new ArenaInput(failure ? ArenaAction.Move : ArenaAction.Attack, 1, 2, x: failure ? 1 : 0));
                while (session.CurrentTick < 8 && session.State == SessionState.Running) session.Step();
                return session.CaptureRecording();
            }
        }
        public static TemplateRecording RoundTrip(TemplateRecording value)
        {
            using (MemoryStream stream = new MemoryStream())
            { TemplateRecordingIO.Write(stream, value); stream.Position = 0; return TemplateRecordingIO.Read(stream); }
        }
        private static TemplateRecording Copy(TemplateRecording source, IEnumerable<TemplateTick> ticks = null, string policy = null) =>
            new TemplateRecording(policy ?? source.Policy, source.Runtime, source.Scenario, source.TickDelta, source.Limits,
                source.InitialHash, source.Inputs, ticks ?? source.Ticks, source.Failure, source.Trace, source.DroppedTraceEntries);
        private static void Submit(Session session, ulong sequence, ulong tick, ArenaInput input)
            => Require(session.Gameplay.Submit(session.Id, sequence, tick, input).Queued, "Input admission.");
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Expect<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name);
        }
    }
}
