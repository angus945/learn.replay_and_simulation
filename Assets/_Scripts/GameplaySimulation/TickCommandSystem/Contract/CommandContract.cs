namespace TickCommandSystem.Contract
{
    public interface ICommand
    {
    }

    public interface ICommandHandler<TCommand> where TCommand : ICommand
    {
        void Handle(CommandData data, TCommand command);
    }
}
