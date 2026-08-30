using System;
using DeterministicSimulation;
using DeterministicSimulation.Framework;

namespace MinimalWiringExample
{
    // Domain: no framework inheritance or Unity dependency.
    public sealed class Player
    {
        public float X { get; private set; }
        public float Direction { get; private set; }

        public void SetDirection(float direction)
        {
            if (float.IsNaN(direction) || float.IsInfinity(direction)
                || direction < -1f || direction > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(direction));
            }
            Direction = direction;
        }

        public void Move(float seconds)
        {
            X += Direction * 4f * seconds;
        }
    }

    public sealed class GameWorld
    {
        public Player Player { get; } = new Player();
    }

    public readonly struct MoveInput : IIntent
    {
        public MoveInput(float direction) { Direction = direction; }
        public float Direction { get; }
    }

    public sealed class MoveInputHandler : IIntentHandler<MoveInput>
    {
        private readonly Player player;

        public MoveInputHandler(Player player) { this.player = player; }

        public void Handle(MoveInput input)
        {
            player.SetDirection(input.Direction);
        }
    }

    public sealed class MovementTick : IPrePhysicsParticipant
    {
        private readonly Player player;

        public MovementTick(Player player) { this.player = player; }

        public void Tick(SimulationContext context)
        {
            player.Move(context.Tick.DeltaTime);
        }
    }

    public sealed class GameDefinition : SimulationDefinition<GameWorld, float>
    {
        protected override void ValidateScenario(float tickDelta)
        {
            // The framework also checks that tickDelta is finite and positive.
            if (tickDelta > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(tickDelta));
            }
        }

        protected override float GetTickDelta(float tickDelta) => tickDelta;
        protected override GameWorld CreateWorld(float tickDelta) => new GameWorld();

        protected override void Configure(
            SimulationBuilder builder, GameWorld world, float tickDelta)
        {
            builder.RequireIntent<MoveInput>();
            builder.RegisterIntentHandler(new MoveInputHandler(world.Player));
            builder.RegisterPrePhysicsParticipant(new MovementTick(world.Player));
        }

        protected override void DestroyWorld(GameWorld world)
        {
            // Managed objects only; no subscriptions or external resources.
        }
    }

    public sealed class PlayerObserver : ISimulationObserver<GameWorld, float>
    {
        public float Observe(GameWorld world) => world.Player.X;
    }

    public static class Example
    {
        public static void Run()
        {
            GameDefinition definition = new GameDefinition();
            PlayerObserver observer = new PlayerObserver();

            using (SimulationSession<GameWorld, float> session =
                definition.CreateSession(0.25f))
            {
                session.EnqueueIntent(new MoveInput(1f));
                Require(session.Observe(observer) == 0f, "Input only queues.");

                session.Step();
                Require(session.Observe(observer) == 1f, "First tick: X = 1.");

                session.Step();
                Require(session.Observe(observer) == 2f, "Direction persists.");

                session.EnqueueIntent(new MoveInput(0f));
                session.Step();
                Require(session.Observe(observer) == 2f, "Zero direction stops.");

                session.EnqueueIntent(new MoveInput(1f));
                session.Reset(0.25f);
                session.Step();
                Require(session.Observe(observer) == 0f, "Reset replaces world and queue.");
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
