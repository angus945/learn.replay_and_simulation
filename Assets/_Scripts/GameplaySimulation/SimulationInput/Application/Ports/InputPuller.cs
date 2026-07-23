namespace SimulationInput.Application
{
    public interface IButtonStatePuller
    {
        bool IsPressed { get; }
    }
    public interface IAxisStatePuller
    {
        float Value { get; }
    }
}