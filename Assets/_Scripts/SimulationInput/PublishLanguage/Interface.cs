namespace SimulationInput
{
    public interface IButtonStatePuller
    {
        bool IsPressed { get; }
    }
    public interface IAxisStatePuller
    {
        float Value { get; }
    }

    public interface IButtonKey { }
    public interface IAxisKey { }
    public interface IInputCommand { }

    public interface IInputCommandBuilder<TCommand> where TCommand : class, IInputCommand
    {
        TCommand CreateCommand();
        void UpdateCommand(TickInputFrame inputFrame, TCommand command);
    }
}
