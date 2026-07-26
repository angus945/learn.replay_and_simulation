using UnityEngine;
using SimulationCore.Logging.Unity.Infrastructure;

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

using SimulationCore.SimulationActor.Infrastructure;
using SimulationCore.SimulationActor.Contract;
using SimulationCore.SimulationActor.Application;
using SimulationCore.SimulationPhysics.Application;
using SimulationCore.Unity.PhysicsRuntime.Infrastructure;
using SimulationCore.SimulationPhysics.Infrastructure;
using SimulationCore.SimulationActor.Presentation;

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

    public SpawnPlayerArguments(Float3 position)
    {
        Position = position;
    }
}
public sealed class SpawnPlayerRecipe : IEntityRecipe<SpawnPlayerArguments>
{
    public void Build(IEntityBuildContext context, in SpawnPlayerArguments arguments)
    {
        context.AddComponent(new PlayerTag());
        context.AddComponent(new ActorArchetypeComponent(0));
        context.AddComponent(new ActorTransformState(arguments.Position, FloatQuaternion.Identity));
    }
}

public class PlayerSystem : ISystem, IPrePhysicsTick
{
    private IEcsWorld world;
    IEntityFilter filter;

    PlayerMoveCommandHandler movement;

    public PlayerSystem()
    {
        movement = new PlayerMoveCommandHandler();
    }

    public void Initialize(IEcsWorld world, ICommandHandleRegistryPort commandRegistry)
    {
        this.world = world;
        filter = world.CreateFilter()
            .With<PlayerTag>()
            .With<ActorArchetypeComponent>()
            .With<ActorTransformState>()
            .Build();

        commandRegistry.Register<PlayerMoveCommand>(movement);
    }

    public void PrePhysicsTick(ulong tick, float deltaTime)
    {
        for (int i = 0; i < filter.EntityCount; i++)
        {
            EntityHandle entity = filter.GetEntity(i);

            if (!world.TryGetComponent<ActorTransformState>(entity, out ActorTransformState transformState))
                continue;

            Float3 position = transformState.Position + movement.Direction * deltaTime;
            ActorTransformState newTransformState = new ActorTransformState(position, transformState.Rotation);
            world.SetComponent(entity, newTransformState);
        }
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

// Composition Root
public class TestCompositionRoot : MonoBehaviour
{
    [SerializeField] UnityLogger logger;
    [SerializeField] Player playerPrefab;

    PlayerInputCommands playerInput;
    SimulationRunner runner;

    void Awake()
    {

    }
    void Start()
    {
        CommandServices commandServices = new CommandServices();
        ICommandContext commandContext = commandServices;

        // SimulationExternalCommands
        RegisterableExternalCommand registerableCommands = new RegisterableExternalCommand();
        ICommandPort commandPort = new CommandEnqueuePort(commandContext, logger);
        IButtonRegistrationPort buttonPort = new ButtonRegistration();
        IAxisRegistrationPort axisPort = new AxisRegistration();
        IRuleRegistrationPort registrationPort = new RuleRegistration();
        playerInput = new PlayerInputCommands(commandPort, buttonPort, axisPort, registrationPort);
        playerInput.RegisterAxisStatePuller<MoveHorizontal>(new UnityAxisStatePuller("Horizontal"));
        playerInput.RegisterAxisStatePuller<MoveVertical>(new UnityAxisStatePuller("Vertical"));
        playerInput.RegisterInputCommand<PlayerMoveCommand>(new AcquirePlayerMoveCommand());
        registerableCommands.RegisterExternalCommandProvider(playerInput);
        // TODO: Register UI, Debug, and other external commands here

        // SimulationWorld
        ICommandHandleRegistryPort commandSubscriberPort = new CommandSubscriberPort(commandContext);
        EcsWorld world = new EcsWorld(100, commandSubscriberPort);
        world.RegisterComponent<PlayerTag>();
        world.RegisterSystem(new PlayerSystem());

        // SimulationActors
        UnityActorInstancePort unityInstancePort = new UnityActorInstancePort(transform);
        unityInstancePort.RegisterPrefab<Player>(0, playerPrefab);

        // Physics Simulation / Decorator
        PhysicsEventSink collisionEventSink = new PhysicsEventSink(commandContext);
        SimulationPhysics physics = new SimulationPhysics(new UnityPhysicsRuntime(), collisionEventSink);
        PhysicsActorInstancePortDecorator physicsActorDecorator = new PhysicsActorInstancePortDecorator(unityInstancePort, collisionEventSink);

        // SimulationActors
        EntityPort entityPort = new EntityPort(world);
        SimulationActors simulationActors = new SimulationActors(entityPort, physicsActorDecorator);
        simulationActors.RegisterActorPool<Player>(0, 10);
        world.RegisterComponent<ActorArchetypeComponent>();
        world.RegisterComponent<ActorTransformState>();


        // Initialize Systems
        playerInput.Initialize();
        world.InitializeSystems();

        // Presentation
        UnitySimulationPresentation presentation = new UnitySimulationPresentation(world, physicsActorDecorator, unityInstancePort);

        ISimulationCommandSystem commandSystem = commandServices;
        ISimulationExternalCommands externalCommands = registerableCommands;
        ISimulationWorld simulationWorld = world;
        ISimulationActor simulationActor = simulationActors;
        ISimulationPhysics simulationPhysics = physics;
        ISimulationPresentation simulationPresentation = presentation;
        runner = new SimulationRunner(
            commandSystem,
            externalCommands,
            simulationWorld,
            simulationActor,
            simulationPhysics,
            simulationPresentation,
            1 / 60f,
            logger);

        // Test spawning a player entity
        IEcsWorld ecsWorld = world;
        ecsWorld.SpawnRequest(new SpawnPlayerRecipe(), new SpawnPlayerArguments(new Float3(0f, 0f, 0f)));
    }
    void Update()
    {
        playerInput.CaptureRenderInput();

        runner.AdvanceTime(Time.unscaledDeltaTime);

        runner.UpdatePresentation();
    }

}
