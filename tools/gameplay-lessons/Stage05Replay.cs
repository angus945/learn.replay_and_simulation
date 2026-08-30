using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GameplaySimulation;
using InvariantChecks;
using Testability;
using Testability.Templates;

namespace GameplayLessons
{
    internal static class Stage05Replay
    {
        internal static void Run()
        {
            GameplayDefinition definition = new GameplayDefinition();
            TemplateRecording recording = RoundTrip(Record(definition));
            LessonAssert.That(recording.Inputs.Count == 3, "Only submitted external inputs are recorded");
            foreach (float seconds in new float[] { 1f / 30f, 1f / 144f, .7f })
            {
                using (TemplateReplay<GameplayWorld, GameplayScenario, GameplayInput, GameplayObservation> replay =
                    definition.CreateReplay(recording))
                {
                    replay.Play();
                    for (int frame = 0; frame < 2048 && replay.State == TemplateReplayState.Playing; frame++)
                        replay.AdvanceTime(seconds);
                    LessonAssert.That(replay.State == TemplateReplayState.Completed && replay.FirstDifference == null,
                        "Different playback frame schedules must reproduce the same tick evidence");
                    LessonAssert.Near(GameplayLessonState.Player(replay.Observe()).X, 1f, "Replay should end at the recorded position");
                    LessonAssert.That(replay.Observe().EnemiesSpawned == 2 && replay.Observe().PendingRespawnTicks.Count == 0,
                        "Replay must also reproduce seeded health and the completed respawn schedule");
                }
            }

            using (TemplateReplay<GameplayWorld, GameplayScenario, GameplayInput, GameplayObservation> changed =
                definition.CreateReplay(ChangeDirection(recording)))
            {
                changed.Step();
                LessonAssert.That(changed.State == TemplateReplayState.Diverged && changed.FirstDifference.Tick == 1,
                    "Changing a recorded input must report the first divergence");
            }
            VerifyFailureReplay();
            Console.WriteLine("  Seeded health + delayed respawn + JSON + three frame schedules: Completed; changed input: Diverged t1; invariant: ReproducedFailure t2.");
        }

        private static TemplateRecording Record(GameplayDefinition definition)
        {
            GameplayScenario scenario = new GameplayScenario(tickDelta: .25f, damage: 100,
                maxTicks: 16, maxActions: 8, seed: 814731, respawnEnemies: true,
                enemyHealthMin: 20, enemyHealthMax: 40, maxEnemySpawns: 2, randomRespawnDelay: true);
            using (TestableSimulationSession<GameplayWorld, GameplayScenario, GameplayInput, GameplayObservation> session =
                definition.CreateTestSession(scenario))
            {
                ulong player = session.Observe().PlayerId;
                ulong enemy = 0;
                foreach (ActorObservation actor in session.Observe().Actors)
                    if (actor.Id != player && actor.Active) { enemy = actor.Id; break; }
                LessonAssert.That(enemy != 0, "This scenario must contain the existing game's enemy");
                Submit(session, 1, 1, new GameplayInput(GameplayActionKind.Move, player, x: 1f));
                Submit(session, 2, 1, new GameplayInput(GameplayActionKind.Attack, player, enemy));
                Submit(session, 3, 2, new GameplayInput(GameplayActionKind.Move, player));
                for (int tick = 0; tick < 16; tick++) session.Simulation.Step();
                LessonAssert.That(session.State == SessionState.Running, "Normal recording must not fault");
                GameplayObservation observation = session.Observe();
                LessonAssert.That(!GameplayLessonState.Actor(observation, enemy).Active,
                    "Attack and structural destruction are reconstructed game behavior");
                LessonAssert.That(observation.EnemiesSpawned == 2 && observation.PendingRespawnTicks.Count == 0,
                    "Sixteen ticks must cover the seeded one-to-three-second respawn delay");
                ActorObservation replacement = null;
                foreach (ActorObservation actor in observation.Actors)
                    if (actor.Id != player && actor.Id != enemy && actor.Active) { replacement = actor; break; }
                LessonAssert.That(replacement != null && replacement.Health >= 20 && replacement.Health <= 40,
                    "The replacement enemy must have a new identity and seeded health in the configured range");
                LessonAssert.Near(GameplayLessonState.Player(observation).X, 1f, "Stop input should retain X=1");
                return session.CaptureRecording();
            }
        }

        private static void VerifyFailureReplay()
        {
            GameplayDefinition definition = new GameplayDefinition(
                new Func<IInvariant<GameplayObservation>>[] { () => new PositionLimit() }, "lessons/position-limit-v1");
            GameplayScenario scenario = new GameplayScenario(tickDelta: .25f, includeEnemy: false, maxTicks: 8, maxActions: 8);
            TemplateRecording recording;
            using (TestableSimulationSession<GameplayWorld, GameplayScenario, GameplayInput, GameplayObservation> session =
                definition.CreateTestSession(scenario))
            {
                Submit(session, 1, 1, new GameplayInput(GameplayActionKind.Move, session.Observe().PlayerId, x: 1f));
                session.Simulation.Step();
                session.Simulation.Step();
                LessonAssert.That(session.State == SessionState.Faulted && session.Failure.Tick == 2,
                    "The injected oracle should detect a non-crash failure at tick two");
                recording = RoundTrip(session.CaptureRecording());
            }
            using (TemplateReplay<GameplayWorld, GameplayScenario, GameplayInput, GameplayObservation> replay = definition.CreateReplay(recording))
            {
                replay.Step();
                replay.Step();
                LessonAssert.That(replay.State == TemplateReplayState.ReproducedFailure && replay.FirstDifference == null,
                    "The same diagnostic policy must reproduce the recorded failure");
                LessonAssert.That(recording.Failure.Code == "lesson.position_limit", "Failure has a machine-readable code");
            }
        }

        private static void Submit(TestableSimulationSession<GameplayWorld, GameplayScenario, GameplayInput, GameplayObservation> session,
            ulong sequence, ulong tick, GameplayInput input)
        {
            SubmissionResult result = session.Gameplay.Submit(session.Id, sequence, tick, input);
            LessonAssert.That(result.Queued, "Lesson input admission failed: " + result.Code);
        }

        private static TemplateRecording RoundTrip(TemplateRecording recording)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                TemplateRecordingIO.Write(stream, recording);
                stream.Position = 0;
                return TemplateRecordingIO.Read(stream);
            }
        }

        private static TemplateRecording ChangeDirection(TemplateRecording recording)
        {
            List<RecordedInput> inputs = new List<RecordedInput>(recording.Inputs);
            RecordedInput first = inputs[0];
            GameplayInput original;
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(first.Payload)))
                original = ArtifactJson.Read<GameplayInput>(stream);
            using (MemoryStream stream = new MemoryStream())
            {
                ArtifactJson.Write(stream, new GameplayInput(GameplayActionKind.Move, original.Actor, x: -1f));
                inputs[0] = new RecordedInput(first.Sequence, first.Tick, Encoding.UTF8.GetString(stream.ToArray()));
            }
            return new TemplateRecording(recording.Policy, recording.Runtime, recording.Scenario, recording.TickDelta,
                recording.Limits, recording.InitialHash, inputs, recording.Ticks, recording.Failure, recording.Trace, recording.DroppedTraceEntries);
        }

        private sealed class PositionLimit : IInvariant<GameplayObservation>
        {
            public string Code => "lesson.position_limit";
            public InvariantViolation Evaluate(GameplayObservation observation)
                => GameplayLessonState.Player(observation).X > 1.5f ? new InvariantViolation(Code, "Lesson boundary exceeded.") : null;
        }
    }
}
