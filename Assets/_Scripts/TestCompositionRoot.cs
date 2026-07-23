using ECSManagement.Application;
using ExternalIntent.Application;
using ExternalIntent.Contract;
using SimulationInput.Application;
using SimulationInput.API;
using UnityEngine;
using SimulationInput.Contract;
using SimulationInput.Unity.Infrastructure;
using ECSManagement.API;
using ECSManagement.Contract;
using Logging.Contract;
using System.Collections.Generic;
using Logging.Unity.Infrastructure;

// Example of a simple player
public struct MoveHorizontal : IAxisInputKey { }
public struct MoveVertical : IAxisInputKey { }

public readonly struct PlayerMoveIntent : IExternalIntent
{
    public readonly Float3 Direction;

    public PlayerMoveIntent(Float3 direction)
    {
        Direction = direction;
    }
}
public class AcquirePlayerMoveIntent : IInputIntentRule
{
    public bool TryProduce(IInputSnapshot snapshot, out IExternalIntent intent)
    {
        var horizontal = snapshot.GetAxisState<MoveHorizontal>();
        var vertical = snapshot.GetAxisState<MoveVertical>();
        var direction = new Float3(horizontal.Value, 0f, vertical.Value).Normalized();

        intent = new PlayerMoveIntent(direction);

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

    PlayerMoveIntentHandler movement;

    public PlayerSystem()
    {
        movement = new PlayerMoveIntentHandler();
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

    public void RegisterIntentHandlers(ISystemIntentHandlerRegistry registry)
    {
        registry.RegisterIntentHandler<PlayerMoveIntent>(movement);
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

    private sealed class PlayerMoveIntentHandler : ISystemIntentHandler<PlayerMoveIntent>
    {
        public Float3 Direction { get; private set; }

        public void HandleIntent(PlayerMoveIntent intent)
        {
            Direction = intent.Direction;
        }
    }
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

        TickIntentsBuilder tickIntentsBuilder = new TickIntentsBuilder();
        tickIntentsBuilder.RegisterInputIntent<PlayerMoveIntent>(new AcquirePlayerMoveIntent());

        // Entity component system setup
        EcsWorld world = new EcsWorld(100);
        world.RegisterComponent<TransformState>();
        world.RegisterComponent<PhysicsState>();
        world.RegisterComponent<PlayerTag>();

        world.RegisterSystem(new PlayerSystem());

        // Test spawning a player entity
        world.Spawn(new SpawnPlayerRecipe(),
            new SpawnPlayerArguments(new Float3(0f, 0f, 0f),
            new Float3(1f, 0f, 0f)));


        // Assemble the SimulationRunner with the necessary components
        runner = new SimulationRunner(simulationInput, tickIntentsBuilder, world);

    }
    void Update()
    {
        runner.Update(Time.unscaledDeltaTime);
    }

}
