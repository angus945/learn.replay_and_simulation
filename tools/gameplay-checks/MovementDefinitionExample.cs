using System;
using CharacterMovement.Application;
using CharacterMovement.Domain;
using CharacterMovement.Integration;
using DeterministicSimulation.Framework;
using MovementAggregate = CharacterMovement.Domain.CharacterMovement;

// Copy the integration pattern, not the example's domain model, into your game project.
internal sealed class MovementWorld
{
    internal MovementWorld()
    {
        Player = new MovementAggregate(new CharacterId(1), new MovementPosition(0, 0), 4);
        CharacterMovementRepository repository = new CharacterMovementRepository();
        repository.Add(Player);
        Application = new MovementApplication(repository);
    }
    internal MovementAggregate Player { get; }
    internal MovementApplication Application { get; }
}

internal sealed class MovementDefinitionExample : SimulationDefinition<MovementWorld, float>,
    ISimulationObserver<MovementWorld, MovementPosition>
{
    protected override void ValidateScenario(float tickDelta)
    {
        // Project constraints; the framework separately validates finite positive tick delta.
        if (tickDelta > 1) throw new ArgumentOutOfRangeException(nameof(tickDelta));
    }
    protected override float GetTickDelta(float tickDelta) => tickDelta;
    protected override MovementWorld CreateWorld(float tickDelta) => new MovementWorld();
    protected override void Configure(SimulationBuilder builder, MovementWorld world, float tickDelta)
    {
        builder.RequireIntent<PlayerMoveIntent>();
        builder.RegisterIntentHandler(new PlayerMoveIntentHandler(world.Application));
        builder.RegisterPrePhysicsParticipant(new MovementPrePhysicsParticipant(world.Application));
    }
    protected override void DestroyWorld(MovementWorld world)
    {
        // No unmanaged resources or subscriptions in this world; an explicit no-op is intentional.
    }
    public MovementPosition Observe(MovementWorld world) => world.Player.Position;

    internal static void Verify()
    {
        MovementDefinitionExample definition = new MovementDefinitionExample();
        using (SimulationSession<MovementWorld, float> session = definition.CreateSession(.25f))
        {
            session.EnqueueIntent(new PlayerMoveIntent(new CharacterId(1), MovementDirection.FromAxes(1, 0)));
            session.Step();
            if (session.Observe(definition).X != 1) throw new Exception("Definition movement example failed.");
            session.Reset(.25f);
            if (session.Observe(definition).X != 0) throw new Exception("Definition reset example failed.");
        }
    }
}
