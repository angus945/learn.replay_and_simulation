using TraceBuffering;

namespace Testability
{
    /// <summary>Single-threaded read-only surface. Reading must not evaluate rules or advance simulation.</summary>
    public interface IDiagnosticReader<TObservation>
    {
        DiagnosticSnapshot<TObservation> ObserveDiagnostics();
        TraceBatch<TraceEntry> ReadTrace(TraceCursor cursor, int maxItems);
    }
}
