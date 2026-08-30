using System;
using System.Runtime.Serialization;

namespace Testability
{
    public enum SessionState { Created, Running, Stopped, Faulted }
    public enum ActionStatus { Accepted, Rejected, InvalidRequest, Failed }

    /// <summary>Queue admission is not gameplay acceptance.</summary>
    public readonly struct SubmissionResult
    {
        public SubmissionResult(bool queued, string code) { Queued = queued; Code = code; }
        public bool Queued { get; }
        public string Code { get; }
    }

    [DataContract]
    public sealed class ActionResult
    {
        public ActionResult(ulong sequence, ulong tick, ActionStatus status, string code)
        { Sequence = sequence; Tick = tick; Status = status; Code = code; }
        [DataMember(Order = 1)] public ulong Sequence { get; private set; }
        [DataMember(Order = 2)] public ulong Tick { get; private set; }
        [DataMember(Order = 3)] public ActionStatus Status { get; private set; }
        [DataMember(Order = 4)] public string Code { get; private set; }
    }

    [DataContract]
    public sealed class TraceEntry
    {
        public TraceEntry(string session, ulong tick, ulong sequence, string stage, string type, string code,
            int wave = -1, ulong actor = 0, ulong target = 0)
        { Session = session; Tick = tick; Sequence = sequence; Stage = stage; Type = type; Code = code; Wave = wave; Actor = actor; Target = target; }
        [DataMember(Order = 1)] public string Session { get; private set; }
        [DataMember(Order = 2)] public ulong Tick { get; private set; }
        [DataMember(Order = 3)] public ulong Sequence { get; private set; }
        [DataMember(Order = 4)] public string Stage { get; private set; }
        [DataMember(Order = 5)] public string Type { get; private set; }
        [DataMember(Order = 6)] public string Code { get; private set; }
        [DataMember(Order = 7)] public int Wave { get; private set; }
        [DataMember(Order = 8)] public ulong Actor { get; private set; }
        [DataMember(Order = 9)] public ulong Target { get; private set; }
    }

}
