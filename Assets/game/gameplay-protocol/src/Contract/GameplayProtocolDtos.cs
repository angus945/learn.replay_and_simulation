using System.Runtime.Serialization;

namespace GameplayProtocol.Game
{
    [DataContract] public sealed class VersionDto
    {
        [DataMember(IsRequired = true)] public int Version;
    }
    [DataContract] public sealed class ActionDto
    {
        [DataMember(IsRequired = true)] public int Version;
        [DataMember(IsRequired = true)] public string Sequence;
        [DataMember(IsRequired = true)] public string TargetTick;
        [DataMember(IsRequired = true)] public string Kind;
        [DataMember(IsRequired = true)] public string Actor;
        [DataMember] public string Target = "0";
        [DataMember] public float X;
        [DataMember] public float Y;
    }
    [DataContract] public sealed class PageDto
    {
        [DataMember(IsRequired = true)] public int Version;
        [DataMember(IsRequired = true)] public int AfterIndex;
        [DataMember(IsRequired = true)] public int MaxItems;
    }
    [DataContract] public sealed class TraceQueryDto
    {
        [DataMember(IsRequired = true)] public int Version;
        [DataMember(IsRequired = true)] public string StreamId;
        [DataMember(IsRequired = true)] public string AfterSequence;
        [DataMember(IsRequired = true)] public int MaxItems;
    }
    [DataContract] public sealed class ResultDto
    {
        [DataMember] public string Sequence;
        [DataMember] public string Tick;
        [DataMember] public string Status;
        [DataMember] public string Code;
    }
    [DataContract] public sealed class ResultPageDto
    {
        [DataMember] public int Version = GameplayProtocolAdapter.PayloadVersion;
        [DataMember] public ResultDto[] Items;
        [DataMember] public int NextIndex;
        [DataMember] public bool HasMore;
    }
    [DataContract] public sealed class ActorDto
    {
        [DataMember] public string Id;
        [DataMember] public float X;
        [DataMember] public float Y;
        [DataMember] public float DirectionX;
        [DataMember] public float DirectionY;
        [DataMember] public float Speed;
        [DataMember] public int Health;
        [DataMember] public int MaxHealth;
        [DataMember] public bool Active;
    }
    [DataContract] public sealed class ObservationDto
    {
        [DataMember] public int Version = GameplayProtocolAdapter.PayloadVersion;
        [DataMember] public string Tick;
        [DataMember] public string PlayerId;
        [DataMember] public string EnemyRandomState;
        [DataMember] public string RespawnRandomState;
        [DataMember] public int EnemiesSpawned;
        [DataMember] public string[] PendingRespawnTicks;
        [DataMember] public ActorDto[] Actors;
    }
    [DataContract] public sealed class AdmissionDto
    {
        [DataMember] public int Version = GameplayProtocolAdapter.PayloadVersion;
        [DataMember] public bool Queued;
        [DataMember] public string Code;
    }
    [DataContract] public sealed class StepDto
    {
        [DataMember] public int Version = GameplayProtocolAdapter.PayloadVersion;
        [DataMember] public string Policy;
        [DataMember] public string Tick;
        [DataMember] public string ModernHash;
        [DataMember] public ResultDto[] Results;
    }
    [DataContract] public sealed class OperationDto
    {
        [DataMember] public string Name;
        [DataMember] public string Permission;
        [DataMember] public bool RequiresSession;
        [DataMember] public bool RequiresControl;
    }
    [DataContract] public sealed class ActionDescriptionDto
    {
        [DataMember] public string Kind;
        [DataMember] public bool RequiresTarget;
        [DataMember] public bool UsesAxes;
        [DataMember] public bool RequiresActor;
        [DataMember] public bool RequiresFiniteAxes;
        [DataMember] public bool NormalizesAxes;
        [DataMember] public string SuccessCode;
        [DataMember] public string[] RejectionCodes;
        [DataMember] public string[] InvalidRequestCodes;
    }
    [DataContract] public sealed class CapabilitiesDto
    {
        [DataMember] public int Version = GameplayProtocolAdapter.PayloadVersion;
        [DataMember] public string SessionId;
        [DataMember] public string State;
        [DataMember] public bool HasRealtimeDriver;
        [DataMember] public string Policy;
        [DataMember] public string HashKind;
        [DataMember] public string GrantedPermissions;
        [DataMember] public string Tick;
        [DataMember] public string LastCompletedTick;
        [DataMember] public float TickDelta;
        [DataMember] public int MaxTicks;
        [DataMember] public int MaxInputs;
        [DataMember] public int TraceCapacity;
        [DataMember] public int MaxPayloadBytes;
        [DataMember] public int MaxTotalPayloadBytes;
        [DataMember] public string ActionOrdering;
        [DataMember] public bool RequiresNonzeroUniqueSequence;
        [DataMember] public bool RequiresFutureTargetTick;
        [DataMember] public string[] AdmissionCodes;
        [DataMember] public OperationDto[] Operations;
        [DataMember] public ActionDescriptionDto[] Actions;
    }
    [DataContract] public sealed class ViolationDto
    {
        [DataMember] public string Code;
        [DataMember] public string Detail;
    }
    [DataContract] public sealed class DiagnosticsDto
    {
        [DataMember] public int Version = GameplayProtocolAdapter.PayloadVersion;
        [DataMember] public ObservationDto Observation;
        [DataMember] public string State;
        [DataMember] public string Tick;
        [DataMember] public string ObservationTick;
        [DataMember] public string LastCompletedTick;
        [DataMember] public bool Evaluated;
        [DataMember] public string InvariantTick;
        [DataMember] public int CheckCount;
        [DataMember] public ViolationDto[] Violations;
        [DataMember] public string FaultCode;
    }
    [DataContract] public sealed class TraceRecordDto
    {
        [DataMember] public string RecordSequence;
        [DataMember] public string Tick;
        [DataMember] public string ActionSequence;
        [DataMember] public string Stage;
        [DataMember] public string Type;
        [DataMember] public string Code;
        [DataMember] public int Wave;
        [DataMember] public string Actor;
        [DataMember] public string Target;
    }
    [DataContract] public sealed class TracePageDto
    {
        [DataMember] public int Version = GameplayProtocolAdapter.PayloadVersion;
        [DataMember] public string StreamId;
        [DataMember] public string AfterSequence;
        [DataMember] public bool StreamChanged;
        [DataMember] public string MissedCount;
        [DataMember] public bool HasMore;
        [DataMember] public TraceRecordDto[] Items;
    }
}
