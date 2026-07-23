namespace ExternalIntent.Application
{
    internal sealed class CommitTickUseCase
    {
        readonly TickIntentsBuilderStats stats;

        internal CommitTickUseCase(TickIntentsBuilderStats stats)
        {
            this.stats = stats;
        }

        internal void Execute(ulong tick)
        {
            stats.IntentBuffer.Commit(tick);
        }
    }
}
