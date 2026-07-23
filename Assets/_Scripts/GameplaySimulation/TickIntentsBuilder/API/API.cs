using SimulationInput.API;
using TickCommandSystem.API;
using TickCommandSystem.Contract;
using TickIntentsBuilder.Contract;

namespace TickIntentsBuilder.API
{
    public interface ITickIntentsBuilder
    {
        void ProduceInputCommands(IInputSnapshot snapshot);
        void CommitTick(ulong tick);
        void EnqueueCommittedCommands(
            ITickCommandQueue commandQueue,
            CommandType type = CommandType.Gameplay);
    }

    public interface IInputCommandProducer : ITickIntentsBuilder
    {
        void RegisterInputCommand<TCommand>(
            IInputCommandRule commandRule)
            where TCommand : struct, ICommand;
    }
}
