namespace Module.Verification.TraceBuffer
{
    public interface ITraceWriter<in T>
    {
        void Record(T entry);
    }

    public interface ITraceReader<T>
    {
        TraceBatch<T> Read(TraceCursor cursor, int maxItems);
    }

    public interface ITraceBuffer<T> : ITraceWriter<T>, ITraceReader<T>
    {
    }
}
