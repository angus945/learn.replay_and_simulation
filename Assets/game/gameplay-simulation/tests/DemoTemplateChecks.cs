using System;
using System.IO;
using CharacterMovement.Domain;
using CharacterMovement.Integration;
using MovementDemo;
using Testability;
using Testability.Templates;

namespace GameplaySimulation.Tests
{
    public static class DemoTemplateChecks
    {
        public static void Verify()
        {
            GameplayScenario scenario = new GameplayScenario(tickDelta: .125f, includeEnemy: true,
                respawnEnemies: true, enemyHealthMin: 20, enemyHealthMax: 40, randomRespawnDelay: true);
            GameplaySession legacy = new GameplaySession();
            legacy.Start(scenario);
            using (MovementDemoSession demo = new MovementDemoSession(new View(), tickDeltaTime: .125f,
                includeEnemy: true, respawnEnemies: true, enemyHealthMin: 20, enemyHealthMax: 40, randomRespawnDelay: true))
            {
                ulong sequence = 0;
                for (ulong tick = 1; tick <= 160; tick++)
                {
                    legacy.Submit(new GameplayRequest(legacy.Id, ++sequence, tick, GameplayActionKind.Move, 1));
                    if (tick % 3 == 0)
                    {
                        ulong target = 2;
                        foreach (ActorObservation actor in legacy.Observe().Actors)
                            if (actor.Id != 1 && actor.Active) { target = actor.Id; break; }
                        legacy.Submit(new GameplayRequest(legacy.Id, ++sequence, tick, GameplayActionKind.Attack, 1, target));
                        demo.RequestAttack();
                    }
                    legacy.Step(); demo.AdvanceTime(.125f);
                    Require(demo.State == SessionState.Running, "Demo faulted");
                    Require(GameplayStateHasher.Compute(legacy.Observe(), scenario) == GameplayStateHasher.Compute(demo.Observe(), scenario), "Legacy gameplay parity at " + tick);
                }
                Require(demo.Observe().EnemiesSpawned > 2, "Respawn not exercised");
                TemplateRecording recording;
                using (MemoryStream stream = new MemoryStream())
                {
                    TemplateRecordingIO.Write(stream, demo.CaptureReplay()); stream.Position = 0;
                    recording = TemplateRecordingIO.Read(stream);
                }
                Require(recording.Policy.StartsWith("gameplay-template-v1", StringComparison.Ordinal), "Legacy recording path");
                foreach (float delta in new float[] { 1f / 30, 1f / 60, 1f / 144, .7f })
                {
                    using (TemplateReplay<GameplayWorld, GameplayScenario, GameplayInput, GameplayObservation> replay = new GameplayDefinition().CreateReplay(recording))
                    {
                        replay.Step();
                        Require(replay.PreviousObservation.Tick == 0 && replay.CurrentTick == 1 && replay.PresentationAlpha == 1, "Single-step presentation");
                        replay.Restart(); replay.Play();
                        for (int frame = 0; frame < 10000 && replay.State == TemplateReplayState.Playing; frame++) replay.AdvanceTime(delta);
                        Require(replay.State == TemplateReplayState.Completed && replay.FirstDifference == null, "Replay diverged");
                        Require(replay.Observe().Tick == demo.TickNumber, "Playback advanced live session");
                    }
                }
                demo.CaptureAxes(1, 0); demo.AdvanceTime(.125f);
                Require(demo.CurrentPosition.X == .5f, "Live movement after replay");
                demo.RequestAttack(); demo.ClearInput(); demo.AdvanceTime(.125f);
                TemplateRecording resumed = demo.CaptureReplay();
                Require(resumed.Ticks[resumed.Ticks.Count - 1].Results.Count == 1, "Pending attack leaked across mode switch");
            }
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        private sealed class View : ICharacterMovementView
        { public void SetPosition(MovementPosition position) { } }
    }
}
