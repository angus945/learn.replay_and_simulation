using DeterministicSimulation;

namespace Testability.Templates
{
    /// <summary>Input envelope available to the integration layer; domain entities need not reference it.</summary>
    public sealed class InputExecutionContext
    {
        internal InputExecutionContext(string sessionId, ulong sequence, ulong targetTick, IDomainEventSink events)
        { SessionId = sessionId; Sequence = sequence; TargetTick = targetTick; Events = events; }
        public string SessionId { get; }
        public ulong Sequence { get; }
        public ulong TargetTick { get; }
        public IDomainEventSink Events { get; }
        internal InputExecutionContext WithEvents(IDomainEventSink events) => new InputExecutionContext(SessionId, Sequence, TargetTick, events);
    }

    public interface ITemplateGameplay<TInput, TObservation>
    {
        string Id { get; }
        ulong CurrentTick { get; }
        SubmissionResult Submit(string sessionId, ulong sequence, ulong targetTick, TInput input);
        TObservation Observe();
    }
    public interface ITemplateSimulation { TemplateTick Step(); }
    public interface ITemplateAdmin<TScenario> { void Reset(TScenario scenario); void Stop(); }
    public interface ITemplateResults
    {
        TemplateActionLookup Find(string sessionId, ulong sequence);
        TemplateActionResultPage Read(string sessionId, int afterIndex, int maxItems);
    }
}
