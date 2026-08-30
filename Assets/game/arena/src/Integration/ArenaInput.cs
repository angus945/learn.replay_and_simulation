using System.Runtime.Serialization;
using Arena.Application;

namespace Arena.Integration
{
    /// <summary>External payload. Session identity, sequence and target tick belong to the framework envelope.</summary>
    [DataContract]
    public sealed class ArenaInput
    {
        public ArenaInput(ArenaAction kind, ulong actor, ulong target = 0, float x = 0, float y = 0)
        { Kind = kind; Actor = actor; Target = target; X = x; Y = y; }
        [DataMember(Order = 1)] public ArenaAction Kind { get; private set; }
        [DataMember(Order = 2)] public ulong Actor { get; private set; }
        [DataMember(Order = 3)] public ulong Target { get; private set; }
        [DataMember(Order = 4)] public float X { get; private set; }
        [DataMember(Order = 5)] public float Y { get; private set; }
    }
}
