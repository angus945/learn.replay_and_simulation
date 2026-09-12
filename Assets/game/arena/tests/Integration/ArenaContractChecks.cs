using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arena.Application;
using Arena.Composition;
using Arena.Domain;
using Arena.Infrastructure;
using Arena.Integration;
using RuntimeControl;
using RuntimeObservation;
using TestabilityEvidence;
using TestabilityOracles;
using TraceBuffering;

namespace Arena.Tests
{
    /// <summary>Executable acceptance examples shared by Unity EditMode and the headless runner.</summary>
    public static class ArenaContractChecks
    {
        public static void Domain()
        {
            Actor actor = new Actor(new ActorId(1), ActorKind.Player, new Position(0, 0), 4, 30);
            actor.SetDirection(1, 0);
            Require(actor.Position.X == 0, "Direction is not time advancement.");
            actor.Advance(.25f);
            Require(actor.Position.X == 1, "Domain movement.");
            actor.SetDirection(1, 1);
            actor.Advance(.25f);
            double magnitude = actor.Direction.X * actor.Direction.X + actor.Direction.Y * actor.Direction.Y;
            Require(Math.Abs(magnitude - 1) < .00001, "Unit diagonal.");
            bool rejected = false;
            try
            {
                actor.SetDirection(float.NaN, 0);
            }
            catch (ArgumentOutOfRangeException)
            {
                rejected = true;
            }
            Require(rejected, "Non-finite direction is rejected.");
            Require(actor.TakeDamage(100) == 30 && actor.IsDead && actor.Direction.X == 0, "Death clamps and stops.");
        }

        public static void Application()
        {
            ActorRepository repository = new ActorRepository();
            RegistryLifecycle lifetime = new RegistryLifecycle(repository);
            ArenaRules rules = new ArenaRules(damage: 100, respawnMinTicks: 2, respawnMaxTicks: 2);
            ArenaApplication application = new ArenaApplication(repository, lifetime, new SpawnRandom(42), rules);
            ArenaResult result = application.Execute(new ArenaRequest(ArenaAction.Attack, application.PlayerId, new ActorId(2)));
            Require(result.Code == "defeated" && result.Facts.Count == 2, "Use case returns facts.");
            Require(lifetime.IsActive(new ActorId(2)), "A domain fact alone does not commit lifetime.");
            application.OnDefeated(new ActorId(2));
            application.ScheduleRespawn(1);
            application.Advance(1, .25f);
            application.Commit(1);
            Require(application.Actors.Count == 1 && application.PendingRespawnTicks.Single() == 3, "Application owns due tick.");
        }

        public static void Simulation()
        {
            using (ArenaSession session = new ArenaDefinition().CreateSession(new ArenaScenario(tickDelta: .25f)))
            {
                Submit(session, 1, new ArenaInput(ArenaAction.Move, 1, x: 1));
                Require(session.Observe().FindActor(1).X == 0, "Submit cannot run Domain.");
                session.Step();
                Require(session.Observe().FindActor(1).X == 1, "PrePhysics calls Application.");
                session.Step();
                Require(session.Observe().FindActor(1).X == 2, "Fixed delta persists.");
            }
            using (ArenaSession fresh = new ArenaDefinition().CreateSession(new ArenaScenario(tickDelta: .125f)))
            {
                Require(fresh.CurrentTick == 0 && fresh.Observe().FindActor(1).X == 0, "The host creates a fresh environment explicitly.");
            }
        }

        public static void Input()
        {
            using (ArenaSession session = new ArenaDefinition().CreateSession(new ArenaScenario(tickDelta: .25f, maxInputs: 4)))
            {
                OperationAdmission first = Submit(session, 2, new ArenaInput(ArenaAction.Move, 999, x: 1));
                OperationAdmission second = Submit(session, 2, new ArenaInput(ArenaAction.Move, 1, x: 0));
                OperationAdmission third = Submit(session, 2, new ArenaInput(ArenaAction.Move, 1, x: 1));
                Require(session.Step().Results.Count == 0, "Future tick.");
                ArenaTickEvidence tick = session.Step();
                long[] sequences = tick.Results.Select(GetSequence).ToArray();
                Require(sequences.SequenceEqual(new long[] { 1, 2, 3 }), "Stable operation order.");
                Require(tick.Results[0].State == OperationState.Rejected, "Admission and product rejection remain separate.");
                ArenaOperationLookup lookup = session.Controls.Find(first.Handle);
                Require(lookup.ReadState == OperationReadState.Found && lookup.State == OperationState.Rejected, "Result lookup.");
                Require(session.Controls.Read(0, 2).HasMore, "Result paging.");
                OperationAdmission fourth = Submit(session, 4, new ArenaInput(ArenaAction.Move, 1));
                OperationAdmission overBudget = session.Submit(new ArenaInput(ArenaAction.Move, 1), 5);
                Require(fourth.IsAdmitted && !overBudget.IsAdmitted, "Input budget.");
                Require(second.Handle.Sequence == 2 && third.Handle.Sequence == 3, "Handles preserve admission identity.");
                session.Stop();
                ArenaOperationLookup cancelled = session.Controls.Find(fourth.Handle);
                Require(cancelled.ReadState == OperationReadState.Found && cancelled.State == OperationState.Cancelled, "Stop must preserve an explicit terminal result for pending work.");
            }
        }

        public static void Lifecycle()
        {
            ArenaScenario scenario = new ArenaScenario(damage: 100, respawnMinTicks: 2, respawnMaxTicks: 2, maxEnemySpawns: 2);
            using (ArenaSession session = new ArenaDefinition().CreateSession(scenario))
            {
                ulong random = session.Observe().HealthRandomState;
                Submit(session, 1, new ArenaInput(ArenaAction.Attack, 1, 2));
                Submit(session, 1, new ArenaInput(ArenaAction.Move, 2, x: 1));
                ArenaTickEvidence first = session.Step();
                Require(first.Results[1].Code == "actor-dead", "Death prevents a later same-tick action.");
                Require(session.Observe().FindActor(2) == null && session.Observe().PendingRespawnTicks.Single() == 3, "Destruction and schedule.");
                Require(session.Observe().HealthRandomState == random, "Delay scheduling does not draw the health stream.");
                session.Step();
                session.Step();
                Require(session.Observe().FindActor(3) != null, "Respawn gets a fresh identity.");
                ArenaTraceEntry[] trace = session.CaptureRecording().Trace.ToArray();
                Require(trace.Any(IsDefeatTrace), "Domain fact causation is retained in trace.");
            }
        }

        public static void Observation()
        {
            using (ArenaSession first = new ArenaDefinition().CreateSession(new ArenaScenario(tickDelta: .25f)))
            using (ArenaSession second = new ArenaDefinition().CreateSession(new ArenaScenario(tickDelta: .25f)))
            {
                ObservationRead<ArenaObservation> initial = first.ObservationReader.ReadLatest();
                ArenaObservation before = initial.Observation;
                byte[] bytes = ArenaCanonicalState.Encode(before);
                Submit(first, 1, new ArenaInput(ArenaAction.Move, 1, x: 1));
                first.Step();
                second.Step();
                Require(before.FindActor(1).X == 0 && bytes.SequenceEqual(ArenaCanonicalState.Encode(before)), "Observation is detached.");
                Require(first.ObservationReader.Read(initial.Reference).Observation == before, "An exact retained observation remains readable.");
                Require(second.Observe().FindActor(1).X == 0, "Session isolation.");
            }
        }

        public static void Diagnostics()
        {
            using (ArenaSession session = new ArenaDefinition().CreateSession(new ArenaScenario(traceCapacity: 8)))
            {
                ArenaDiagnosticSnapshot initial = session.Diagnostics.ReadSnapshot();
                Require(initial.Evaluation.Verdict == TestVerdict.Passed, "Initial observation is evaluated explicitly by the Arena host.");
                session.Step();
                TraceBatch<ArenaTraceEntry> page = session.Diagnostics.ReadTrace(default(TraceCursor), 256);
                ulong tick = session.CurrentTick;
                session.Diagnostics.ReadSnapshot();
                Require(session.CurrentTick == tick && session.Diagnostics.ReadTrace(page.NextCursor, 256).Items.Count == 0, "Diagnostic reads do not advance or evaluate.");
                Require(page.OverwrittenCount > 0, "Bounded trace reports overwrite.");
                EvidenceBundle evidence = session.CaptureEvidence("run", "case");
                Require(evidence.Entries.Count > 0, "Evidence is built only when the host asks.");
            }
            using (ArenaSession failure = new ArenaDefinition(true).CreateSession(new ArenaScenario(tickDelta: .25f)))
            {
                Submit(failure, 1, new ArenaInput(ArenaAction.Move, 1, x: 1));
                failure.Step();
                failure.Step();
                Require(failure.State == ArenaSessionState.Faulted && failure.LastCompletedTick == 1, "Oracle failure evidence.");
                Require(failure.Diagnostics.ReadSnapshot().ObservationTick == 2, "The post-tick observation was safely published before evaluation.");
            }
        }

        public static void Replay()
        {
            ArenaRecording recording = CreateRecording(false);
            float[] frames = { 1f / 30, 1f / 144, .37f };
            foreach (float frame in frames)
            {
                using (ArenaReplay replay = new ArenaDefinition().CreateReplay(RoundTrip(recording)))
                {
                    replay.Play();
                    int guard = 10000;
                    while (replay.State == ArenaReplayState.Playing && guard > 0)
                    {
                        replay.AdvanceTime(frame);
                        guard--;
                    }
                    Require(replay.State == ArenaReplayState.Completed && replay.FirstDifference == null, "Replay frame schedule.");
                    replay.Restart();
                    Require(replay.CurrentTick == 0, "Restart reconstructs a fresh Arena host.");
                    replay.Play();
                    replay.Pause();
                    replay.Step();
                    Require(replay.CurrentTick == 1, "Pause and single step.");
                }
            }
            using (ArenaReplay failure = new ArenaDefinition(true).CreateReplay(RoundTrip(CreateRecording(true))))
            {
                failure.Step();
                failure.Step();
                Require(failure.State == ArenaReplayState.ReproducedFailure, "Expected failure replay.");
            }
            List<ArenaRecordedTick> changed = new List<ArenaRecordedTick>(recording.Ticks);
            ArenaRecordedTick original = changed[0];
            changed[0] = new ArenaRecordedTick(original.Tick, "changed", original.Results, original.Failure);
            using (ArenaReplay replay = new ArenaDefinition().CreateReplay(Copy(recording, changed, null)))
            {
                replay.Step();
                Require(replay.FirstDifference != null && replay.FirstDifference.Category == "state_digest", "Digest divergence.");
            }
        }

        public static void Realtime()
        {
            using (ArenaLiveSession stopped = new ArenaLiveSession(new ArenaScenario(tickDelta: .25f, maxTicks: 1)))
            {
                stopped.CaptureAxes(1, 0);
                stopped.AdvanceTime(.25f);
                Require(stopped.State == ArenaSessionState.Stopped && stopped.Observe().FindActor(1).X == 1, "Tick budget termination.");
            }
            using (ArenaLiveSession live = new ArenaLiveSession(new ArenaScenario(tickDelta: .25f)))
            {
                int health = live.Observe().FindActor(2).Health;
                live.CaptureAttack(true);
                live.CaptureAttack(false);
                live.AdvanceTime(.75f);
                Require(live.TickNumber == 3 && live.Observe().FindActor(2).Health == health - 10, "Buffered input across catch-up ticks.");
                using (ArenaReplay replay = new ArenaDefinition().CreateReplay(live.CaptureRecording()))
                {
                    while (replay.State == ArenaReplayState.Paused) replay.Step();
                    Require(replay.State == ArenaReplayState.Completed && live.TickNumber == 3, "Replay never advances live state.");
                }
            }
        }

        public static ArenaRecording CreateRecording(bool failure)
        {
            ArenaScenario scenario = new ArenaScenario(tickDelta: .25f, damage: 100, respawnMinTicks: 2, respawnMaxTicks: 4);
            using (ArenaSession session = new ArenaDefinition(failure).CreateSession(scenario))
            {
                ArenaAction action = failure ? ArenaAction.Move : ArenaAction.Attack;
                float x = failure ? 1 : 0;
                Submit(session, 1, new ArenaInput(action, 1, 2, x: x));
                while (session.CurrentTick < 8 && session.State == ArenaSessionState.Running) session.Step();
                return session.CaptureRecording();
            }
        }

        public static ArenaRecording RoundTrip(ArenaRecording value)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                ArenaRecordingIO.Write(stream, value);
                stream.Position = 0;
                return ArenaRecordingIO.Read(stream);
            }
        }

        private static ArenaRecording Copy(ArenaRecording source, IEnumerable<ArenaRecordedTick> ticks, string policy)
        {
            string actualPolicy = policy ?? source.Policy;
            IEnumerable<ArenaRecordedTick> actualTicks = ticks ?? source.Ticks;
            return new ArenaRecording(actualPolicy, source.Runtime, source.Scenario, source.TickDelta, source.Limits, source.InitialDigest, source.Inputs, actualTicks, source.Trace, source.DroppedTraceEntries);
        }

        private static OperationAdmission Submit(ArenaSession session, ulong tick, ArenaInput input)
        {
            OperationAdmission admission = session.Submit(input, tick);
            Require(admission.IsAdmitted, "Input admission: " + admission.Code);
            return admission;
        }

        private static long GetSequence(ArenaOperationResult result)
        {
            return result.Sequence;
        }

        private static bool IsDefeatTrace(ArenaTraceEntry entry)
        {
            return entry.Type == "Defeated" && entry.Sequence == 1 && entry.Target == 2;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
