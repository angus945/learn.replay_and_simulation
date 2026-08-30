using System;
using CharacterMovement.Domain;
using CharacterMovement.Integration;
using GameplaySimulation;
using Testability;
using Testability.Templates;
using DeterministicSimulation.Framework;
using TickInputBuffering;
using TickInputBuffering.Contract;

namespace MovementDemo
{
    /// <summary>Realtime input/presentation adapter around the same control plane used by tests.</summary>
    public sealed class MovementDemoSession : IDisposable, IRealtimeInputSource, IRealtimePresentation
    {
        private readonly TickInputBuffer input = new TickInputBuffer();
        private readonly TestableSimulationSession<GameplayWorld, GameplayScenario, GameplayInput, GameplayObservation> gameplay;
        private readonly ICharacterMovementView view;
        private readonly GameplayScenario scenario;
        private readonly Action<GameplayObservation> captureObservation;
        private MovementPosition previous;
        private MovementPosition current;
        private readonly RealtimeSimulationRunner runner;
        private ulong sequence;
        private bool attackPending;
        private bool attackDown;

        public MovementDemoSession(ICharacterMovementView view, float speed = 4f, float tickDeltaTime = 1f / 60f, bool includeEnemy = false,
            bool respawnEnemies = false, int enemyHealthMin = 0, int enemyHealthMax = 0, bool randomRespawnDelay = false,
            Action<GameplayObservation> captureObservation = null)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.captureObservation = captureObservation;
            scenario = new GameplayScenario(tickDelta: tickDeltaTime, speed: speed, includeEnemy: includeEnemy,
                build: Environment.GetEnvironmentVariable("GAMEPLAY_BUILD") ?? "unspecified", respawnEnemies: respawnEnemies,
                enemyHealthMin: enemyHealthMin, enemyHealthMax: enemyHealthMax, randomRespawnDelay: randomRespawnDelay);
            input.RegisterAxis(0); input.RegisterAxis(1); input.Seal();
            gameplay = new GameplayDefinition().CreateTestSession(scenario);
            runner = gameplay.CreateRealtimeRunner(input: this, presentation: this);
            view.SetPosition(default);
        }
        public MovementPosition CurrentPosition => current;
        public ulong TickNumber => gameplay.CurrentTick;
        public float PresentationAlpha => runner.PresentationAlpha;
        public SessionState State => gameplay.State;
        public GameplayObservation Observe() => gameplay.Observe();
        public TemplateFailure Failure => gameplay.Failure;
        public Exception DriverFailure => runner.Failure;
        public TemplateRecording CaptureReplay() => gameplay.CaptureRecording();
        public void Dispose() { runner.Dispose(); gameplay.Dispose(); }
        public void ClearInput()
        {
            CaptureAxes(0, 0); attackPending = false; attackDown = false;
        }
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
            => runner.AdvanceTime(seconds);

        void IRealtimeInputSource.AcquireInput(DeterministicSimulation.SimulationTick context)
        {
            ulong tick = context.Number;
            TickInputFrame frame = input.ConsumeTick(tick);
            GameplayObservation observation = gameplay.Observe();
            SubmissionResult move = gameplay.Submit(gameplay.Id, ++sequence, tick, new GameplayInput(
                GameplayActionKind.Move, observation.PlayerId, x: frame.GetAxis(0).Value, y: frame.GetAxis(1).Value));
            if (!move.Queued) { gameplay.Stop(); return; }
            if (attackPending)
            {
                ulong target = 0;
                foreach (ActorObservation actor in observation.Actors)
                    if (actor.Id != observation.PlayerId && actor.Active) { target = actor.Id; break; }
                SubmissionResult attack = gameplay.Submit(gameplay.Id, ++sequence, tick, new GameplayInput(GameplayActionKind.Attack, observation.PlayerId, target));
                attackPending = false;
                if (!attack.Queued) { gameplay.Stop(); return; }
            }
        }
        void IRealtimePresentation.CaptureTickState(ulong tick)
        {
            previous = current;
            GameplayObservation observation = gameplay.Observe();
            ActorObservation player = observation.FindActor(observation.PlayerId);
            current = new MovementPosition(player.X, player.Y);
            captureObservation?.Invoke(observation);
        }
        public void UpdatePresentation() => runner.UpdatePresentation();
        void IRealtimePresentation.Render(float alpha)
        {
            view.SetPosition(new MovementPosition(previous.X + (current.X - previous.X) * alpha,
                previous.Y + (current.Y - previous.Y) * alpha));
        }
    }
}
