public interface IActorHandle
{
    int PoolId { get; }
    int ActorId { get; }
    uint Generation { get; }
}
