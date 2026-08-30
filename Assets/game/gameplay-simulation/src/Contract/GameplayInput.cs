using System.Runtime.Serialization;

namespace GameplaySimulation
{
    /// <summary>Gameplay payload only. Session, sequence and target tick belong to the framework envelope.</summary>
    [DataContract]
    public sealed class GameplayInput
    {
        public GameplayInput(GameplayActionKind kind, ulong actor, ulong target = 0, float x = 0, float y = 0)
        { Kind = kind; Actor = actor; Target = target; X = x; Y = y; }
        [DataMember(Order = 1)] public GameplayActionKind Kind { get; private set; }
        [DataMember(Order = 2)] public ulong Actor { get; private set; }
        [DataMember(Order = 3)] public ulong Target { get; private set; }
        [DataMember(Order = 4)] public float X { get; private set; }
        [DataMember(Order = 5)] public float Y { get; private set; }
    }
}
