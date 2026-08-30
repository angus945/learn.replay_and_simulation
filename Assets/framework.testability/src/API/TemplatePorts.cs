namespace Testability.Templates
{
    public interface ITemplateGameplay<TInput, TObservation>
    {
        string Id { get; }
        ulong CurrentTick { get; }
        SubmissionResult Submit(string sessionId, ulong sequence, ulong targetTick, TInput input);
        TObservation Observe();
    }
    public interface ITemplateSimulation { TemplateTick Step(); }
    public interface ITemplateAdmin<TScenario> { void Reset(TScenario scenario); void Stop(); }
    public interface ITemplateResults { TemplateActionLookup Find(string sessionId, ulong sequence); }
}
