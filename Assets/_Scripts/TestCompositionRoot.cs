using ECSManagement.Application;
using SimulationInput.Application;
using SimulationInput.API;
using UnityEngine;
using SimulationInput.Contract;
using SimulationInput.Unity.Infrastructure;
using ECSManagement.API;
using ECSManagement.Contract;
using Logging.Contract;
using Logging.Unity.Infrastructure;
using TickCommandSystem.API;
using TickCommandSystem.Contract;
using TickIntentsBuilder.Contract;
using TickCommandRuntime = TickCommandSystem.Application.TickCommandSystem;
using TickIntentsBuilderRuntime = TickIntentsBuilder.Application.TickIntentsBuilder;

// Example of a simple player
public struct MoveHorizontal : IAxisInputKey { }
public struct MoveVertical : IAxisInputKey { }

public readonly struct PlayerMoveCommand : ICommand
{
    public readonly Float3 Direction;

    public PlayerMoveCommand(Float3 direction)
    {
        Direction = direction;
    }
}

public class AcquirePlayerMoveCommand : IInputCommandRule
{
    public bool TryProduce(IInputSnapshot snapshot, out ICommand command)
    {
        var horizontal = snapshot.GetAxisState<MoveHorizontal>();
        var vertical = snapshot.GetAxisState<MoveVertical>();
        var direction = new Float3(horizontal.Value, 0f, vertical.Value).Normalized();

        command = new PlayerMoveCommand(direction);

        return true;
    }

}

public struct TransformState : IComponent
{
    public Float3 Position;
}
public struct PhysicsState : IComponent
{
    public Float3 Velocity;
}
public struct PlayerTag : IComponent { }

public struct SpawnPlayerArguments : IEntityArguments
{
    public readonly Float3 Position;
    public readonly Float3 Velocity;

    public SpawnPlayerArguments(Float3 position, Float3 velocity)
    {
        Position = position;
        Velocity = velocity;
    }
}
public sealed class SpawnPlayerRecipe : IEntityRecipe<SpawnPlayerArguments>
{
    public void Build(IEntityBuildContext context, EntityHandle entity, in SpawnPlayerArguments arguments)
    {
        context.AddComponent(entity, new TransformState { Position = arguments.Position });
        context.AddComponent(entity, new PhysicsState { Velocity = arguments.Velocity });
        context.AddComponent(entity, new PlayerTag());
    }
}

public class PlayerSystem : ISystem
{
    private IEcsWorld world;
    IEntityFilter filter;

    PlayerMoveCommandHandler movement;

    public PlayerSystem()
    {
        movement = new PlayerMoveCommandHandler();
    }

    public void Initialize(IEcsWorld world)
    {
        this.world = world;
        filter = world.CreateFilter()
            .With<PlayerTag>()
            .With<TransformState>()
            .With<PhysicsState>()
            .Build();
    }

    public void RegisterCommandHandlers(ITickCommandDispatcher registry)
    {
        registry.RegisterCommandHandler<PlayerMoveCommand>(movement);
    }

    public void PrePhysicsTick(ulong tick, float deltaTime)
    {
        for (int i = 0; i < filter.EntityCount; i++)
        {
            EntityHandle entity = filter.GetEntity(i);

            if (!world.TryGetComponent<TransformState>(entity, out TransformState transformState))
                continue;

            transformState.Position = transformState.Position + movement.Direction * deltaTime;
            world.SetComponent(entity, transformState);
        }
    }

    public void PostPhysicsTick(ulong tick, float deltaTime)
    {
        for (int i = 0; i < filter.EntityCount; i++)
        {
            EntityHandle entity = filter.GetEntity(i);

            if (!world.TryGetComponent<TransformState>(entity, out TransformState transformState))
                continue;

            Debug.Log($"Player Position: {transformState.Position}");
        }
    }

    private sealed class PlayerMoveCommandHandler : ICommandHandler<PlayerMoveCommand>
    {
        public Float3 Direction { get; private set; }

        public void Handle(CommandData data, PlayerMoveCommand command)
        {
            Direction = command.Direction;
        }
    }
}

// Tick Physics System

public interface IPhysicsRuntime
{
    void Simulate(float deltaTime);
}

// Composition Root
public class TestCompositionRoot : MonoBehaviour
{
    [SerializeField] LogLevel inputLogLevel = LogLevel.Debug;

    SimulationRunner runner;
    private void Awake()
    {
        Physics.simulationMode = SimulationMode.Script;
    }
    void Start()
    {
        // UnityActorFactory<Enemy> enemyFactory = new UnityActorFactory<Enemy>(enemyPrefab, transform);

        // PhysicsActorService physicsActor = new PhysicsActorService();
        // physicsActor.RegisterActorPool<Enemy>(0, 10, enemyFactory);
        // physicsActor.InitializeActorPools();

        SimulationInputs simulationInput = new SimulationInputs(new UnityLogger(inputLogLevel, this));
        simulationInput.RegisterAxisStatePuller<MoveHorizontal>(new UnityAxisStatePuller("Horizontal"));
        simulationInput.RegisterAxisStatePuller<MoveVertical>(new UnityAxisStatePuller("Vertical"));
        simulationInput.Initialize();

        TickIntentsBuilderRuntime tickIntentsBuilder = new TickIntentsBuilderRuntime();
        tickIntentsBuilder.RegisterInputCommand<PlayerMoveCommand>(new AcquirePlayerMoveCommand());

        // Entity component system setup
        EcsWorld world = new EcsWorld(100);
        world.RegisterComponent<TransformState>();
        world.RegisterComponent<PhysicsState>();
        world.RegisterComponent<PlayerTag>();

        TickCommandRuntime tickCommandSystem = new TickCommandRuntime();
        PlayerSystem playerSystem = new PlayerSystem();
        playerSystem.RegisterCommandHandlers(tickCommandSystem);
        world.RegisterSystem(playerSystem);

        // Test spawning a player entity
        world.SpawnRequest(new SpawnPlayerRecipe(),
            new SpawnPlayerArguments(new Float3(0f, 0f, 0f),
            new Float3(1f, 0f, 0f)));
        world.CommitStructuralChanges();


        // Assemble the SimulationRunner with the necessary components
        runner = new SimulationRunner(
            simulationInput,
            tickIntentsBuilder,
            world,
            tickCommandSystem);

    }
    void Update()
    {
        runner.Update(Time.unscaledDeltaTime);
    }

}
