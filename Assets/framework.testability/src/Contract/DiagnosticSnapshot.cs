using System;
using System.Collections.Generic;
using InvariantChecks;

namespace Testability
{
    public sealed class InvariantReport
    {
        public InvariantReport(bool evaluated, ulong tick, int checkCount, IEnumerable<InvariantViolation> violations)
        {
            Evaluated = evaluated; Tick = tick; CheckCount = checkCount;
            Violations = new List<InvariantViolation>(violations).AsReadOnly();
        }
        public bool Evaluated { get; }
        public ulong Tick { get; }
        public int CheckCount { get; }
        public IReadOnlyList<InvariantViolation> Violations { get; }
    }

    public sealed class DiagnosticSnapshot<TObservation>
    {
        public DiagnosticSnapshot(string sessionId, SessionState state, ulong tick, TObservation observation,
            InvariantReport invariants, string faultCode)
        { SessionId = sessionId; State = state; Tick = tick; Observation = observation; Invariants = invariants; FaultCode = faultCode; }
        public string SessionId { get; }
        public SessionState State { get; }
        public ulong Tick { get; }
        public TObservation Observation { get; }
        public InvariantReport Invariants { get; }
        public string FaultCode { get; }
    }
}
