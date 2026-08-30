// using UnityEngine;
// using SimulationCore.Logging.Unity.Infrastructure;

// using SimulationCore;
// using SimulationCore.Contracts;
// using SimulationCore.CommandSystem.API;
// using SimulationCore.CommandSystem.Application;
// using SimulationCore.ExternalCommands;
// using SimulationCore.ExternalCommands.PlayerInput.Application;
// using SimulationCore.ExternalCommands.PlayerInput.Infrastructure;
// using SimulationCore.ExternalCommands.PlayerInput.Contract;
// using SimulationCore.ExternalCommands.Port;
// using SimulationCore.World.API;
// using SimulationCore.World.Application;
// using SimulationCore.World.Infrastructure;
// using SimulationCore.SimulationActor.Application;
// using SimulationCore.SimulationActor.Contract;
// using SimulationCore.SimulationActor.Infrastructure;
// using SimulationCore.SimulationActor.Presentation;
// using SimulationCore.SimulationPhysics.Application;
// using SimulationCore.SimulationPhysics.Contract;
// using SimulationCore.SimulationPhysics.Infrastructure;
// using SimulationCore.Unity.PhysicsRuntime.Infrastructure;

// public class TestCompositionRoot : MonoBehaviour
// {
//     private static readonly Vector2[] DefaultCoinSpawnPositions =
//     {
//         new Vector2(-3.2f, 2.2f),
//         new Vector2(3.1f, 2.0f),
//         new Vector2(-2.6f, -2.2f),
//         new Vector2(2.5f, -1.9f),
//         new Vector2(0f, 3.2f)
//     };

//     [SerializeField] UnityLogger logger;
//     [SerializeField] Player playerPrefab;
//     [SerializeField] Coin coinPrefab;
//     [SerializeField] float playerMoveSpeed = 4f;
//     [SerializeField] Vector2 playAreaHalfExtents = new Vector2(4.25f, 4.25f);
//     [SerializeField] Vector2[] coinSpawnPositions =
//     {
//         new Vector2(-3.2f, 2.2f),
//         new Vector2(3.1f, 2.0f),
//         new Vector2(-2.6f, -2.2f),
//         new Vector2(2.5f, -1.9f),
//         new Vector2(0f, 3.2f)
//     };

//     PlayerInputCommands playerInput;
//     SimulationRunner runner;
//     CoinCollectionSystem coinCollection;
//     GUIStyle hudStyle;
//     GUIStyle completeStyle;

//     void Start()
//     {
//         if (playerPrefab == null)
//             throw new MissingReferenceException($"{nameof(TestCompositionRoot)} requires a player prefab.");

//         if (coinPrefab == null)
//             throw new MissingReferenceException($"{nameof(TestCompositionRoot)} requires a coin prefab.");

//         Vector2[] resolvedCoinSpawnPositions = ResolveCoinSpawnPositions();

//         CommandServices commandServices = new CommandServices();
//         ICommandContext commandContext = commandServices;

//         RegisterableExternalCommand registerableCommands = BuildExternalCommands(commandContext);
//         EcsWorld world = BuildWorld(commandContext, resolvedCoinSpawnPositions.Length);
//         UnityActorInstancePort unityInstancePort = BuildUnityActorPort();

//         PhysicsEventSink collisionEventSink = new PhysicsEventSink(commandContext);
//         PhysicsEventDispatchPort eventDispatchPort = new PhysicsEventDispatchPort(collisionEventSink, commandContext);
//         SimulationPhysics physics = new SimulationPhysics(new UnityPhysicsRuntime(), eventDispatchPort);
//         PhysicsActorInstancePortDecorator physicsActorDecorator = new PhysicsActorInstancePortDecorator(unityInstancePort, collisionEventSink);

//         EntityPort entityPort = new EntityPort(world);
//         SimulationActors simulationActors = new SimulationActors(entityPort, physicsActorDecorator);
//         simulationActors.RegisterActorPool<Player>(GameActorArchetypes.Player, 1);
//         simulationActors.RegisterActorPool<Coin>(GameActorArchetypes.Coin, resolvedCoinSpawnPositions.Length);

//         playerInput.Initialize();
//         world.InitializeSystems();

//         UnitySimulationPresentation presentation = new UnitySimulationPresentation(world, physicsActorDecorator, unityInstancePort);

//         runner = new SimulationRunner(
//             commandServices,
//             registerableCommands,
//             world,
//             simulationActors,
//             physics,
//             presentation,
//             1f / 60f,
//             logger);

//         SpawnInitialEntities(world, resolvedCoinSpawnPositions);
//         PrimeSpawnedActors();
//     }

//     void Update()
//     {
//         if (playerInput == null || runner == null)
//             return;

//         playerInput.CaptureRenderInput();
//         runner.AdvanceTime(Time.unscaledDeltaTime);
//         runner.UpdatePresentation();
//     }

//     void OnGUI()
//     {
//         if (coinCollection == null)
//             return;

//         EnsureGuiStyles();

//         GUI.Label(
//             new Rect(16f, 14f, 360f, 32f),
//             $"Coins: {coinCollection.CollectedCoins}/{coinCollection.TotalCoins}",
//             hudStyle);

//         if (coinCollection.IsComplete)
//         {
//             GUI.Label(
//                 new Rect(16f, 50f, 420f, 32f),
//                 "All coins collected",
//                 completeStyle);
//         }
//     }

//     private RegisterableExternalCommand BuildExternalCommands(ICommandContext commandContext)
//     {
//         RegisterableExternalCommand registerableCommands = new RegisterableExternalCommand();
//         ICommandPort commandPort = new CommandEnqueuePort(commandContext, logger);
//         IButtonRegistrationPort buttonPort = new ButtonRegistration();
//         IAxisRegistrationPort axisPort = new AxisRegistration();
//         IRuleRegistrationPort registrationPort = new RuleRegistration();

//         playerInput = new PlayerInputCommands(commandPort, buttonPort, axisPort, registrationPort);
//         playerInput.RegisterAxisStatePuller<MoveHorizontal>(new UnityAxisStatePuller("Horizontal"));
//         playerInput.RegisterAxisStatePuller<MoveVertical>(new UnityAxisStatePuller("Vertical"));
//         playerInput.RegisterInputCommand<PlayerMoveCommand>(new AcquirePlayerMoveCommand());
//         registerableCommands.RegisterExternalCommandProvider(playerInput);

//         return registerableCommands;
//     }

//     private EcsWorld BuildWorld(ICommandContext commandContext, int coinCount)
//     {
//         ICommandHandleRegistryPort commandSubscriberPort = new CommandSubscriberPort(commandContext);
//         EcsWorld world = new EcsWorld(100, commandSubscriberPort);
//         world.RegisterComponent<PlayerTag>();
//         world.RegisterComponent<PlayerScoreComponent>();
//         world.RegisterComponent<CoinTag>();
//         world.RegisterComponent<CoinValueComponent>();
//         world.RegisterComponent<ActorArchetypeComponent>();
//         world.RegisterComponent<ActorTransformState>();

//         coinCollection = new CoinCollectionSystem(coinCount);
//         commandContext.RegisterCommandHandler(new FlushPhysicsEventsCommandHandler());
//         commandContext.RegisterEventHandler<OnCollisionEnter>(coinCollection);
//         commandContext.RegisterEventHandler<OnCollisionStay>(coinCollection);

//         world.RegisterSystem(new PlayerSystem(playerMoveSpeed, playAreaHalfExtents));
//         world.RegisterSystem(coinCollection);

//         return world;
//     }

//     private UnityActorInstancePort BuildUnityActorPort()
//     {
//         UnityActorInstancePort unityInstancePort = new UnityActorInstancePort(transform);
//         unityInstancePort.RegisterPrefab<Player>(GameActorArchetypes.Player, playerPrefab);
//         unityInstancePort.RegisterPrefab<Coin>(GameActorArchetypes.Coin, coinPrefab);
//         return unityInstancePort;
//     }

//     private void SpawnInitialEntities(IEcsWorld world, Vector2[] resolvedCoinSpawnPositions)
//     {
//         world.SpawnRequest(
//             new SpawnPlayerRecipe(),
//             new SpawnPlayerArguments(new Float3(0f, 0f, 0f), resolvedCoinSpawnPositions.Length));

//         SpawnCoinRecipe coinRecipe = new SpawnCoinRecipe();
//         for (int i = 0; i < resolvedCoinSpawnPositions.Length; i++)
//         {
//             Vector2 spawnPosition = resolvedCoinSpawnPositions[i];
//             world.SpawnRequest(
//                 coinRecipe,
//                 new SpawnCoinArguments(new Float3(spawnPosition.x, spawnPosition.y, 0f), 1));
//         }
//     }

//     private void PrimeSpawnedActors()
//     {
//         runner.AdvanceTime(runner.TickDeltaTime);
//         runner.UpdatePresentation();
//     }

//     private Vector2[] ResolveCoinSpawnPositions()
//     {
//         if (coinSpawnPositions != null && coinSpawnPositions.Length > 0)
//             return coinSpawnPositions;

//         return DefaultCoinSpawnPositions;
//     }

//     private void EnsureGuiStyles()
//     {
//         if (hudStyle != null)
//             return;

//         hudStyle = new GUIStyle(GUI.skin.label);
//         hudStyle.fontSize = 24;
//         hudStyle.fontStyle = FontStyle.Bold;
//         hudStyle.normal.textColor = Color.white;

//         completeStyle = new GUIStyle(GUI.skin.label);
//         completeStyle.fontSize = 20;
//         completeStyle.fontStyle = FontStyle.Bold;
//         completeStyle.normal.textColor = new Color(1f, 0.86f, 0.2f);
//     }
// }
