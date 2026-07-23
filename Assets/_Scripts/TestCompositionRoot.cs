using ECSManagement.Application;
using ExternalIntent.Application;
using ExternalIntent.Contract;
using PhysicsActor.Unity.Infrastructure;
using SimulationInput.Application;
using SimulationInput.API;
using UnityEngine;
using SimulationInput.Contract;
using SimulationInput.Unity.Infrastructure;
using ECSManagement.API;
using ECSManagement.Contract;
using System.Collections.Generic;

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
    }
}

namespace ECSManagement
{
    public sealed class EcsFilter
    {
        // Dictionary<EntityHandle,
        // List<int> matchingIndices;
    }

}

public class PlayerSystem : ISystem
{
    private readonly EntityHandle player;
    private IEcsWorld world;

    public PlayerSystem(EntityHandle player)
    {
        this.player = player;
    }

    public void Initialize(IEcsWorld world)
    {
        this.world = world;
    }

    public void RegisterIntentHandlers(ISystemIntentHandlerRegistry registry)
    {
        registry.RegisterIntentHandler<PlayerMoveIntent>(new PlayerMoveIntentHandler(world, player));
    }

    public void PrePhysicsTick(ulong tick, float deltaTime)
    {
        // if (!world.TryGetComponent<TransformState>(player, out TransformState transformState))
        //     return;

        // if (!world.TryGetComponent<PhysicsState>(player, out PhysicsState physicsState))
        //     return;

        // transformState.Position = transformState.Position + physicsState.Velocity * deltaTime;
        // world.SetComponent(player, transformState);
    }

    public void PostPhysicsTick(ulong tick, float deltaTime)
    {
    }

    private sealed class PlayerMoveIntentHandler : ISystemIntentHandler<PlayerMoveIntent>
    {
        private readonly IEcsWorld world;
        private readonly EntityHandle player;

        public PlayerMoveIntentHandler(IEcsWorld world, EntityHandle player)
        {
            this.world = world;
            this.player = player;
        }

        public void HandleIntent(PlayerMoveIntent intent)
        {
            // if (world.TryGetComponent<PhysicsState>(player, out PhysicsState physicsState))
            // {
            //     physicsState.Velocity = intent.Direction;
            //     world.SetComponent(player, physicsState);
            // }

            // Debug.Log($"Handle PlayerMoveIntent: Direction={intent.Direction}");
        }
    }
}

// Composition Root
public class TestCompositionRoot : MonoBehaviour
{
    [SerializeField] private Enemy enemyPrefab;

    SimulationRunner runner;
    private void Awake()
    {
        Physics.simulationMode = SimulationMode.Script;
    }
    void Start()
    {
        UnityActorFactory<Enemy> enemyFactory = new UnityActorFactory<Enemy>(enemyPrefab, transform);

        // PhysicsActorService physicsActor = new PhysicsActorService();
        // physicsActor.RegisterActorPool<Enemy>(0, 10, enemyFactory);
        // physicsActor.InitializeActorPools();


        SimulationInputs simulationInput = new SimulationInputs();
        simulationInput.RegisterAxisStatePuller<MoveHorizontal>(new UnityAxisStatePuller("Horizontal"));
        simulationInput.RegisterAxisStatePuller<MoveVertical>(new UnityAxisStatePuller("Vertical"));
        simulationInput.Initialize();

        IntentAcquirer intentAcquirer = new IntentAcquirer();
        intentAcquirer.RegisterInputIntent<PlayerMoveIntent>(new AcquirePlayerMoveIntent());

        // Entity component system setup
        EcsWorld world = new EcsWorld(100);
        world.RegisterComponent<TransformState>();
        world.RegisterComponent<PhysicsState>();

        // Test spawning a player entity
        EntityHandle playerHandle = world.Spawn(new SpawnPlayerRecipe(),
            new SpawnPlayerArguments(new Float3(0f, 0f, 0f),
            new Float3(1f, 0f, 0f)));

        world.RegisterSystem(new PlayerSystem(playerHandle));

        // Assemble the SimulationRunner with the necessary components
        runner = new SimulationRunner(simulationInput, intentAcquirer, world);

    }
    void Update()
    {
        runner.Update(Time.unscaledDeltaTime);
    }

}
