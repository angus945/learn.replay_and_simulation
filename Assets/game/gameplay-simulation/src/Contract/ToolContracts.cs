using System.Collections.Generic;
using Testability;

namespace GameplaySimulation
{
    public enum SimulationDriveMode { Manual, Realtime }
    public enum ActionLookupState { Unknown, Pending, Completed, Cancelled, StaleSession }

    public sealed class ActionLookup
    {
        public ActionLookup(ActionLookupState state, ActionResult result = null) { State = state; Result = result; }
        public ActionLookupState State { get; }
        public ActionResult Result { get; }
    }

    public sealed class ActionResultPage
    {
        public ActionResultPage(string sessionId, IEnumerable<ActionResult> items, int nextIndex, bool hasMore)
        { SessionId = sessionId; Items = new List<ActionResult>(items).AsReadOnly(); NextIndex = nextIndex; HasMore = hasMore; }
        public string SessionId { get; }
        public IReadOnlyList<ActionResult> Items { get; }
        public int NextIndex { get; }
        public bool HasMore { get; }
    }

    public sealed class ActionDescriptor
    {
        public ActionDescriptor(GameplayActionKind kind, bool requiresTarget, bool usesAxes)
        { Kind = kind; RequiresTarget = requiresTarget; UsesAxes = usesAxes; }
        public GameplayActionKind Kind { get; }
        public bool RequiresActor => true;
        public bool RequiresTarget { get; }
        public bool UsesAxes { get; }
        public bool RequiresFiniteAxes => true;
        public bool NormalizesAxes => UsesAxes;
        public string SuccessCode => Kind == GameplayActionKind.Move ? "move.applied" : "attack.applied";
        public IReadOnlyList<string> RejectionCodes => Kind == GameplayActionKind.Move
            ? System.Array.AsReadOnly(new[] { "actor.unknown", "actor.dead" })
            : System.Array.AsReadOnly(new[] { "actor.unknown", "actor.dead", "target.self", "target.unknown", "target.dead", "target.out_of_range" });
    }

    public sealed class GameplayCapabilities
    {
        public GameplayCapabilities(string sessionId, SessionState state, SimulationDriveMode mode, GameplayScenario scenario)
        { SessionId = sessionId; State = state; DriveMode = mode; Scenario = scenario; }
        public int ContractVersion => 1;
        public string SessionId { get; }
        public SessionState State { get; }
        public SimulationDriveMode DriveMode { get; }
        public bool CanStep => State == SessionState.Running && DriveMode == SimulationDriveMode.Manual;
        public bool CanSubmit => State == SessionState.Running;
        public bool SupportsResultQuery => true;
        public bool SupportsDiagnostics => true;
        public bool SupportsRemoteProtocol => false;
        public string ActionOrdering => "target-tick-then-sequence";
        public string Threading => "single-owner-thread-between-ticks";
        public bool RequiresNonzeroUniqueSequence => true;
        public bool RequiresFutureTargetTick => true;
        public int MaxResultPageSize => 1024;
        public GameplayScenario Scenario { get; }
        public IReadOnlyList<ActionDescriptor> Actions { get; } = System.Array.AsReadOnly(new[]
        {
            new ActionDescriptor(GameplayActionKind.Move, false, true),
            new ActionDescriptor(GameplayActionKind.Attack, true, false)
        });
    }
}
