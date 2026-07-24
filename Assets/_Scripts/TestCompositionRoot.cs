using UnityEngine;
using SimulationCore.Logging.Contract;
using SimulationCore.Logging.Unity.Infrastructure;
using ILogger = SimulationCore.Logging.API.ILogger;

using SimulationCore;
using SimulationCore.Contracts;
using SimulationCore.Infrastructure;

using SimulationCore.CommandSystem.API;
using SimulationCore.CommandSystem.Application;

using SimulationCore.ExternalCommands.PlayerInput.Application;
using SimulationCore.ExternalCommands.PlayerInput.Infrastructure;
using SimulationCore.ExternalCommands.PlayerInput.Contract;
using SimulationCore.ExternalCommands.PlayerInput.API;
using SimulationCore.ExternalCommands;
using SimulationCore.ExternalCommands.Port;

using SimulationCore.World.Application;
using SimulationCore.World.Contract;
using SimulationCore.World.Infrastructure;
using SimulationCore.World.API;

using SimulationCore.SimulationActor;
using SimulationCore.SimulationActor.Application.Port;
using SimulationCore.SimulationActor.Infrastructure;

// Example of a simple player
#region PlayerInputs
public struct MoveHorizontal : IAxisInputKey { }
public struct MoveVertical : IAxisInputKey { }

public readonly struct PlayerMoveCommand : ICommand
{
    public readonly Float3 Direction;

    public PlayerMoveCommand(Float3 direction)
    {
        Direction = direction;
    }
    public override string ToString()
    {
        return $"PlayerMoveCommand: Direction = {Direction}";
    }
}
public class AcquirePlayerMoveCommand : IInputCommandRule
{
    public bool TryProduce(IPlayerInputSnapshot snapshot, out ICommand command)
    {
        var horizontal = snapshot.GetAxisState<MoveHorizontal>();
        var vertical = snapshot.GetAxisState<MoveVertical>();
        var direction = new Float3(horizontal.Value, vertical.Value, 0).Normalized();

        command = new PlayerMoveCommand(direction);

        return true;
    }
}
#endregion

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
        // context.AddComponent(entity, new TransformState { Position = arguments.Position });
        // context.AddComponent(entity, new PhysicsState { Velocity = arguments.Velocity });
        // context.AddComponent(entity, new SceneActorDefinition { ArchetypeId = 0 });
        context.AddComponent(entity, new PlayerTag());
    }
}

public class PlayerSystem : ISystem
{
    private IEcsWorld world;
    // IEntityFilter filter;

    PlayerMoveCommandHandler movement;

    public PlayerSystem()
    {
        movement = new PlayerMoveCommandHandler();
    }

    public void Initialize(IEcsWorld world, ICommandHandleRegisterPort commandSubscriber)
    {
        this.world = world;
        // filter = world.CreateFilter()
        //     .With<PlayerTag>()
        //     .With<TransformState>()
        //     .With<PhysicsState>()
        //     .Build();

        commandSubscriber.Register<PlayerMoveCommand>(movement);
    }

    public void PrePhysicsTick(ulong tick, float deltaTime)
    {
        // for (int i = 0; i < filter.EntityCount; i++)
        // {
        //     EntityHandle entity = filter.GetEntity(i);

        //     if (!world.TryGetComponent<TransformState>(entity, out TransformState transformState))
        //         continue;

        //     transformState.Position = transformState.Position + movement.Direction * deltaTime;
        //     world.SetComponent(entity, transformState);
        // }
    }

    public void PostPhysicsTick(ulong tick, float deltaTime)
    {
        // for (int i = 0; i < filter.EntityCount; i++)
        // {
        //     EntityHandle entity = filter.GetEntity(i);

        //     if (!world.TryGetComponent<TransformState>(entity, out TransformState transformState))
        //         continue;

        //     Debug.Log($"Player Position: {transformState.Position}");
        // }
    }

    private sealed class PlayerMoveCommandHandler : ICommandHandler<PlayerMoveCommand>
    {
        public Float3 Direction { get; private set; }

        public void Handle(PlayerMoveCommand command)
        {
            Direction = command.Direction;
        }
    }
}

// // Actor Bridge 
// // namespace SimulationCore.World.API
// // {
// //     public interface IEntityActorBridge
// //     {
// //         void ReconcileBeforePhysics(ulong tick, float deltaTime);
// //         void ReconcileAfterStructuralCommit(ulong tick, float deltaTime);
// //     }
// // }
// // namespace Presentation.API
// // {
// //     interface ISimulationPresentation
// //     {
// //         void UpdatePresentation(ulong tick);
// //     }
// // }

// // namespace SimulationCore.World.Unity
// // {
// //     public struct SimulationTransform : IComponent
// //     {
// //         public Float3 Position;
// //         public FloatQuaternion Rotation;
// //     }

// //     public struct SceneActorDefinition : IComponent
// //     {
// //         public int ArchetypeId;
// //     }

// //     public struct PhysicsBodyDefinition : IComponent
// //     {
// //         public int ProfileId;
// //         public PhysicsAuthority Authority;
// //     }

// //     public enum PhysicsAuthority : byte
// //     {
// //         SimulationKinematic,
// //         UnityDynamic,
// //         TriggerOnly
// //     }

// //     public class UnityActorBridge : IEntityActorBridge, ISimulationPresentation
// //     {
// //         IEcsWorld world;
// //         UnityActorPoolService actorPoolService;
// //         Dictionary<EntityHandle, ActorLease<IUnityEntityActor>> entityActorMap = new Dictionary<EntityHandle, ActorLease<IUnityEntityActor>>();

// //         private readonly IEntityFilter filter;

// //         public UnityActorBridge(IEcsWorld world, UnityActorPoolService actorPoolService)
// //         {
// //             filter = world.CreateFilter()
// //                 .With<PlayerTag>()
// //                 .With<TransformState>()
// //                 .With<PhysicsState>()
// //                 .Build();

// //             this.world = world;
// //             this.actorPoolService = actorPoolService;
// //         }

// //         public void ReconcileBeforePhysics(ulong tick, float deltaTime)
// //         {
// //             List<EntityHandle> toDestroy = new List<EntityHandle>();
// //             foreach (EntityHandle entity in entityActorMap.Keys)
// //             {
// //                 if (filter.Contains(entity))
// //                 {

// //                 }
// //                 else
// //                 {
// //                     toDestroy.Add(entity);
// //                 }
// //             }

// //             foreach (EntityHandle entity in toDestroy)
// //             {
// //                 entityActorMap[entity].Actor.GameObject.SetActive(false);
// //             }
// //         }
// //         public void ReconcileAfterStructuralCommit(ulong tick, float deltaTime)
// //         {
// //             List<EntityHandle> toDestroy = new List<EntityHandle>();
// //             foreach (EntityHandle entity in entityActorMap.Keys)
// //             {
// //                 if (!filter.Contains(entity))
// //                 {
// //                     toDestroy.Add(entity);
// //                 }
// //             }
// //             foreach (EntityHandle entity in toDestroy)
// //             {
// //                 actorPoolService.ReleaseActor(new ReleaseActorCommand(entityActorMap[entity].Handle));
// //                 entityActorMap.Remove(entity);
// //             }

// //             List<EntityHandle> toSpawn = new List<EntityHandle>();
// //             for (int i = 0; i < filter.EntityCount; i++)
// //             {
// //                 EntityHandle entity = filter.GetEntity(i);
// //                 if (!entityActorMap.ContainsKey(entity))
// //                 {
// //                     toSpawn.Add(entity);
// //                 }
// //             }
// //             foreach (EntityHandle entity in toSpawn)
// //             {
// //                 if (world.TryGetComponent<SceneActorDefinition>(entity, out SceneActorDefinition actorDef))
// //                 {
// //                     int archetypeId = actorDef.ArchetypeId;
// //                     ActorLease<IUnityEntityActor> lease = actorPoolService.AcquireActor(new AcquireActorCommand(archetypeId));
// //                     UnityActor unityActor = lease.UnityActor;
// //                     unityActor.BindEntity(entity, lease.Handle);

// //                     entityActorMap.Add(entity, lease);
// //                 }
// //                 else
// //                 {
// //                     Debug.LogWarning($"Entity {entity} does not have a SceneActorDefinition component.");
// //                     continue;
// //                 }
// //             }

// //             actorPoolService.ActivatePendingActors(new ActivatePendingActorsCommand());
// //         }

// //         public void UpdatePresentation(ulong tick)
// //         {
// //             foreach (var kvp in entityActorMap)
// //             {
// //                 EntityHandle entity = kvp.Key;
// //                 ActorLease<IUnityEntityActor> lease = kvp.Value;

// //                 if (world.TryGetComponent<TransformState>(entity, out TransformState transformState))
// //                 {
// //                     Vector3 unityPosition = new Vector3(transformState.Position.X, transformState.Position.Y, transformState.Position.Z);
// //                     lease.Actor.Transform.position = unityPosition;
// //                 }
// //             }
// //         }

// //     }
// // }

// // Tick Physics System
// namespace TickPhysicsSystem
// {

//     public interface IPhysicsRuntime
//     {
//         void Simulate(float deltaTime);
//     }
//     public class UnityPhysicsRuntime : IPhysicsRuntime
//     {
//         public void Simulate(float deltaTime)
//         {
//             Physics.SyncTransforms();
//             Physics.Simulate(deltaTime);
//             // Physics.ContactEvent
//         }
//     }
// }
// namespace TickPhysicsSystem.Unity
// {
//     public interface IUnityFactSink
//     {
//         void RecordCollision(CollisionFact collisionFact);
//     }
//     public enum ContactPhase : byte
//     {
//         Enter,
//         Stay,
//         Exit
//     }
//     public readonly struct CollisionFact
//     {
//         public readonly EntityHandle EntityA;
//         public readonly EntityHandle EntityB;
//         public readonly ContactPhase Phase;

//         public CollisionFact(EntityHandle entityA, EntityHandle entityB, ContactPhase phase)
//         {
//             EntityA = entityA;
//             EntityB = entityB;
//             Phase = phase;
//         }
//     }
// }

// Composition Root
public class TestCompositionRoot : MonoBehaviour
{
    [SerializeField] UnityLogger logger;
    [SerializeField] Player playerPrefab;

    PlayerInputCommands playerInput;
    SimulationRunner runner;
    private void Awake()
    {
        Physics.simulationMode = SimulationMode.Script;
    }
    void Start()
    {
        CommandServices commandServices = new CommandServices();
        ICommandContext commandContext = commandServices;

        // SimulationExternalCommands
        RegisterableExternalCommand registerableCommands = new RegisterableExternalCommand();
        ICommandEnqueuePort commandPort = new CommandEnqueuePort(commandContext, logger);
        IRuleRegistrationPort registrationPort = new RuleRegistration();
        IButtonRegistrationPort buttonPort = new ButtonRegistration();
        IAxisRegistrationPort axisPort = new AxisRegistration();
        playerInput = new PlayerInputCommands(commandPort, buttonPort, axisPort, registrationPort);
        playerInput.RegisterAxisStatePuller<MoveHorizontal>(new UnityAxisStatePuller("Horizontal"));
        playerInput.RegisterAxisStatePuller<MoveVertical>(new UnityAxisStatePuller("Vertical"));
        playerInput.RegisterInputCommand<PlayerMoveCommand>(new AcquirePlayerMoveCommand());
        playerInput.Initialize();
        registerableCommands.RegisterExternalCommandProvider(playerInput);
        // TODO: Register UI, Debug, and other external commands here

        // SimulationWorld
        ICommandHandleRegisterPort commandSubscriberPort = new CommandSubscriberPort(commandContext);
        EcsWorld world = new EcsWorld(100, commandSubscriberPort);
        world.RegisterComponent<PlayerTag>();
        world.RegisterSystem(new PlayerSystem());

        UnityActorInstancePort unityInstancePort = new UnityActorInstancePort();
        unityInstancePort.RegisterPrefab<Player>(0, playerPrefab);
        SimulationActors simulationActors = new SimulationActors(unityInstancePort);

        ISimulationCommandSystem commandSystem = commandServices;
        ISimulationExternalCommands externalCommands = registerableCommands;
        ISimulationWorld simulationWorld = world;
        ISimulationActor simulationActor = simulationActors;
        ISimulationPhysics simulationPhysics = new NullSimulationPhysics();
        ISimulationPresentation simulationPresentation = new NullSimulationPresentation();
        runner = new SimulationRunner(
            commandSystem,
            externalCommands,
            simulationWorld,
            simulationActor,
            simulationPhysics,
            simulationPresentation,
            1 / 60f,
            logger);


        // // Entity component system setup



        // // Test spawning a player entity
        // world.SpawnRequest(new SpawnPlayerRecipe(), new SpawnPlayerArguments(new Float3(0f, 0f, 0f), new Float3(1f, 0f, 0f)));
        // world.CommitStructuralChanges();

        // UnityPhysicsRuntime physicsRuntime = new UnityPhysicsRuntime();

        // UnityActorPoolService actorPoolService = new UnityActorPoolService();
        // actorPoolService.RegisterActorPool<Player>(0, 10, new UnityActorFactory<Player>(playerPrefab, transform));
        // actorPoolService.InitializeActorPools();

        // UnityActorBridge actorBridge = new UnityActorBridge(world, actorPoolService);
    }
    void Update()
    {
        playerInput.CaptureRenderInput();

        runner.AdvanceTime(Time.unscaledDeltaTime);
    }

}
