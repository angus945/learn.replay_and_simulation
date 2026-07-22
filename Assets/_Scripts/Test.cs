using PhysicsActor.Application;
using PhysicsActor.Unity.Infrastructure;
using SimulationInput;
using SimulationInput.Application;
using UnityEngine;

public class HorizontalAxis : IAxisStatePuller, IAxisKey
{
    public float Value => Input.GetAxis("Horizontal");
}
public class VerticalAxis : IAxisStatePuller, IAxisKey
{
    public float Value => Input.GetAxis("Vertical");
}


public class MoveCommand : IInputCommand
{
    public float Horizontal { get; private set; }
    public float Vertical { get; private set; }

    public void Set(float horizontal, float vertical)
    {
        Horizontal = horizontal;
        Vertical = vertical;
    }
}
public class MoveCommandBuilder : IInputCommandBuilder<MoveCommand>
{
    public MoveCommand CreateCommand()
    {
        return new MoveCommand();
    }

    public void UpdateCommand(TickInputFrame inputFrame, MoveCommand command)
    {
        AxisInputEvent horizontal = inputFrame.GetAxis<HorizontalAxis>();
        AxisInputEvent vertical = inputFrame.GetAxis<VerticalAxis>();

        command.Set(horizontal.Value, vertical.Value);
    }
}

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

        ApplicationService application = new ApplicationService();
        application.RegisterActorPool<Enemy>(0, 10, enemyFactory);
        application.InitializeActorPools();

        SimulationInputs simulationInputApplication = new SimulationInputs();
        simulationInputApplication.RegisterAxisStatePuller<HorizontalAxis>(new HorizontalAxis());
        simulationInputApplication.RegisterAxisStatePuller<VerticalAxis>(new VerticalAxis());
        simulationInputApplication.RegisterInputCommandBuilder<MoveCommand>(new MoveCommandBuilder());
        simulationInputApplication.Initialize();

        runner = new SimulationRunner(simulationInputApplication);
    }
    void Update()
    {
        runner.Update(Time.unscaledDeltaTime);
    }

}
