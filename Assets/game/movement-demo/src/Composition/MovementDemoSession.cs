using System;
using CharacterMovement.Domain;
using CharacterMovement.Integration;
using GameplaySimulation;
using Testability;
using TickInput;
using TickInput.Contract;

namespace MovementDemo
{
    /// <summary>Realtime input/presentation adapter around the same control plane used by tests.</summary>
    public sealed class MovementDemoSession
    {
        private readonly TickInputBuffer input = new TickInputBuffer();
        private readonly GameplaySession gameplay = new GameplaySession(SimulationDriveMode.Realtime);
        private readonly IRealtimeTickDriver clock;
        private readonly ICharacterMovementView view;
        private readonly GameplayScenario scenario;
        private MovementPosition previous;
        private MovementPosition current;
        private double accumulator;
        private ulong sequence;
        private bool attackPending;
        private bool attackDown;

        public MovementDemoSession(ICharacterMovementView view, float speed = 4f, float tickDeltaTime = 1f / 60f, bool includeEnemy = false,
            bool respawnEnemies = false, int enemyHealthMin = 0, int enemyHealthMax = 0, bool randomRespawnDelay = false)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            scenario = new GameplayScenario(tickDelta: tickDeltaTime, speed: speed, includeEnemy: includeEnemy,
                build: Environment.GetEnvironmentVariable("GAMEPLAY_BUILD") ?? "unspecified", respawnEnemies: respawnEnemies,
                enemyHealthMin: enemyHealthMin, enemyHealthMax: enemyHealthMax, randomRespawnDelay: randomRespawnDelay);
            input.RegisterAxis(0); input.RegisterAxis(1); input.Seal();
            gameplay.Start(scenario);
            clock = gameplay.ClaimRealtimeDriver();
            view.SetPosition(default);
        }
        public MovementPosition CurrentPosition => current;
        public ulong TickNumber => gameplay.CurrentTick;
        public float PresentationAlpha => (float)Math.Min(1, Math.Max(0, accumulator / scenario.TickDelta));
        public SessionState State => gameplay.State;
        public GameplayObservation Observe() => gameplay.Observe();
        public FailureArtifact Failure => gameplay.Failure;
        public ReplayArtifact CaptureReplay() => gameplay.CaptureReplay();
        public IDiagnosticReader<GameplayObservation> Diagnostics => gameplay.Diagnostics;
        public void RequestAttack() => attackPending = true;
        public void CaptureAttackButton(bool down)
        {
            if (down && !attackDown) attackPending = true;
            attackDown = down;
        }

        public void CaptureAxes(float horizontal, float vertical)
        {
            MovementDirection.FromAxes(horizontal, vertical);
            input.CaptureAxis(0, horizontal); input.CaptureAxis(1, vertical);
        }
        public void AdvanceTime(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (State != SessionState.Running) return;
            accumulator += seconds;
            while (accumulator >= scenario.TickDelta && State == SessionState.Running)
            {
                if (TickNumber >= (ulong)scenario.MaxTicks) { gameplay.Stop(); break; }
                TickInputFrame frame = input.ConsumeTick(TickNumber + 1);
                SubmissionResult move = gameplay.Submit(new GameplayRequest(gameplay.Id, ++sequence, TickNumber + 1,
                    GameplayActionKind.Move, 1, x: frame.GetAxis(0).Value, y: frame.GetAxis(1).Value));
                if (!move.Queued) { gameplay.Stop(); break; }
                if (attackPending)
                {
                    ulong target = 2;
                    foreach (ActorObservation actor in gameplay.Observe().Actors)
                        if (actor.Id != 1 && actor.Active) { target = actor.Id; break; }
                    SubmissionResult attack = gameplay.Submit(new GameplayRequest(gameplay.Id, ++sequence, TickNumber + 1, GameplayActionKind.Attack, 1, target));
                    attackPending = false;
                    if (!attack.Queued) { gameplay.Stop(); break; }
                }
                clock.AdvanceTick();
                previous = current;
                ActorObservation player = gameplay.Observe().Actors[0];
                current = new MovementPosition(player.X, player.Y);
                accumulator -= scenario.TickDelta;
            }
        }
        public void UpdatePresentation()
        {
            float alpha = PresentationAlpha;
            view.SetPosition(new MovementPosition(previous.X + (current.X - previous.X) * alpha,
                previous.Y + (current.Y - previous.Y) * alpha));
        }
    }
}
