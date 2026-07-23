namespace ExternalIntent.Application
{
    internal sealed class CommitTickUseCase
    {
        readonly IntentAcquirerStats stats;

        internal CommitTickUseCase(IntentAcquirerStats stats)
        {
            this.stats = stats;
        }

        internal void Execute(ulong tick)
        {
            stats.IntentBuffer.Commit(tick);
        }
    }
}
