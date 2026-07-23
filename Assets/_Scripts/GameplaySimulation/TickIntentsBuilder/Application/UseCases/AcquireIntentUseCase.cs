using TickCommandSystem.API;
using TickCommandSystem.Contract;

namespace TickIntentsBuilder.Application
{
    internal sealed class EnqueueCommittedCommandsUseCase
    {
        readonly TickIntentsBuilderStats stats;

        internal EnqueueCommittedCommandsUseCase(TickIntentsBuilderStats stats)
        {
            this.stats = stats;
        }

        internal void Execute(
            ITickCommandQueue commandQueue,
            CommandType type)
        {
            stats.CommandBuffer.EnqueueCommitted(commandQueue, type);
        }
    }
}
