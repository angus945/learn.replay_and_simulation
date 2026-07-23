using GamePlay.Application;
using GamePlay.Contract;
using ExternalIntent.Application;
using ExternalIntent.Contract;
using PhysicsActor.Application;
using PhysicsActor.Unity.Infrastructure;
using SimulationInput.Application;
using SimulationInput.API;
using UnityEngine;
using SimulationInput.Contract;
using SimulationInput.Unity.Infrastructure;
using GamePlay.API;

public struct MoveHorizontal : IAxisInputKey { }
public struct MoveVertical : IAxisInputKey { }

public readonly struct PlayerMoveIntent : IExternalIntent
{
    public readonly Vector3 Direction;

    public PlayerMoveIntent(Vector3 direction)
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
        var direction = new Vector3(horizontal.Value, 0f, vertical.Value).normalized;

        intent = new PlayerMoveIntent(direction);

        return true;
    }

}
public class PlayerSystem : IGameplaySystem
{
    public void RegisterIntentHandler(IIntentHandlerRegistry registry)
    {
        registry.RegisterIntentHandler<PlayerMoveIntent>(new PlayerMoveIntentHandler());
    }
    public void PostPhysicsTick(float deltaTime)
    {
        throw new System.NotImplementedException();
    }

    public void PrePhysicsTick(float deltaTime)
    {
        throw new System.NotImplementedException();
    }


    public class PlayerMoveIntentHandler : IIntentHandler<PlayerMoveIntent>
    {
        public void HandleIntent(PlayerMoveIntent intent)
        {
            Debug.Log($"Handle PlayerMoveIntent: Direction={intent.Direction}");
        }
    }
}

public class EntityManager
{

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

        PhysicsActorService physicsActor = new PhysicsActorService();
        physicsActor.RegisterActorPool<Enemy>(0, 10, enemyFactory);
        physicsActor.InitializeActorPools();

        EntityManager entityManager = new EntityManager();

        SimulationInputs simulationInput = new SimulationInputs();
        simulationInput.RegisterAxisStatePuller<MoveHorizontal>(new UnityAxisStatePuller("Horizontal"));
        simulationInput.RegisterAxisStatePuller<MoveVertical>(new UnityAxisStatePuller("Vertical"));
        simulationInput.Initialize();

        IntentAcquirer intentAcquirer = new IntentAcquirer();
        intentAcquirer.RegisterInputIntent<PlayerMoveIntent>(new AcquirePlayerMoveIntent());

        GamePlayOrchestrator orchestrator = new GamePlayOrchestrator();
        orchestrator.RegisterSystem(new PlayerSystem());

        orchestrator.RegisterIntentHandlers();

        // Assemble the SimulationRunner with the necessary components
        runner = new SimulationRunner(simulationInput, intentAcquirer, orchestrator);

    }
    void Update()
    {
        runner.Update(Time.unscaledDeltaTime);
    }

}
