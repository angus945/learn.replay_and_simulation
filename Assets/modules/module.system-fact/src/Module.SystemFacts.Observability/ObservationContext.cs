using System;

public sealed class ObservationContext
{
    public ObservationContext(long sequence, DateTimeOffset timestamp)
    {
        Sequence = sequence;
        Timestamp = timestamp;
    }

    public long Sequence { get; }

    public DateTimeOffset Timestamp { get; }

    public long? Tick { get; }

    public string TraceId { get; }

    public string Source { get; }
}