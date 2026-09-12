namespace InvariantChecks
{
    /// <summary>Evaluate a read model without modifying the target. Scheduling and failure policy belong to the caller.</summary>
    public interface IInvariant<in T>
    {
        string Code { get; }
        InvariantViolation Evaluate(T observation);
    }
}
