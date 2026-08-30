using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GameplayProtocol
{
    [Flags]
    public enum ProtocolPermission { None = 0, Observe = 1, Act = 2, Drive = 4, Admin = 8 }

    /// <summary>Issued by trusted server composition, never accepted from a wire request.</summary>
    public sealed class ProtocolClient
    {
        public ProtocolClient(string id, ProtocolPermission permissions)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Client identity required.", nameof(id));
            Id = id; Permissions = permissions;
        }
        public string Id { get; }
        public ProtocolPermission Permissions { get; }
    }

    [DataContract]
    public sealed class ProtocolRequest
    {
        public ProtocolRequest(int version, string requestId, string sessionId, string operation, string payloadJson = "{}")
        { Version = version; RequestId = requestId; SessionId = sessionId; Operation = operation; PayloadJson = payloadJson; }
        [DataMember(IsRequired = true)] public int Version { get; private set; }
        [DataMember(IsRequired = true)] public string RequestId { get; private set; }
        [DataMember(IsRequired = true)] public string SessionId { get; private set; }
        [DataMember(IsRequired = true)] public string Operation { get; private set; }
        [DataMember(IsRequired = true)] public string PayloadJson { get; private set; }
    }

    [DataContract]
    public sealed class ProtocolResponse
    {
        public ProtocolResponse(string requestId, string sessionId, string code, string payloadJson = null)
        { Version = 1; RequestId = requestId; SessionId = sessionId; Code = code; PayloadJson = payloadJson; }
        [DataMember] public int Version { get; private set; }
        [DataMember] public string RequestId { get; private set; }
        [DataMember] public string SessionId { get; private set; }
        [DataMember] public string Code { get; private set; }
        public bool Success => Code == "ok";
        [DataMember] public string PayloadJson { get; private set; }
    }

    public sealed class ProtocolOperation
    {
        public ProtocolOperation(string name, ProtocolPermission permission, bool requiresSession, bool requiresControl)
        { Name = name; Permission = permission; RequiresSession = requiresSession; RequiresControl = requiresControl; }
        public string Name { get; }
        public ProtocolPermission Permission { get; }
        public bool RequiresSession { get; }
        public bool RequiresControl { get; }
    }

    public sealed class ProtocolLimits
    {
        public ProtocolLimits(int pending = 128, int remembered = 4096, int requestBytes = 65536, int responseBytes = 1048576, long historyBytes = 16777216)
        {
            if (pending < 1 || remembered < 1 || requestBytes < 1 || responseBytes < 1) throw new ArgumentOutOfRangeException(nameof(pending));
            if (historyBytes < (long)requestBytes + responseBytes + 2048) throw new ArgumentOutOfRangeException(nameof(historyBytes));
            Pending = pending; Remembered = remembered; RequestBytes = requestBytes; ResponseBytes = responseBytes;
            HistoryBytes = historyBytes;
        }
        public int Pending { get; }
        public int Remembered { get; }
        public int RequestBytes { get; }
        public int ResponseBytes { get; }
        public long HistoryBytes { get; }
    }

    public sealed class ProtocolFault : Exception
    {
        public ProtocolFault(string code) : base(code) { Code = code; }
        public string Code { get; }
    }
}
