namespace PhysicsActor
{
    public interface IPhysicalActor
    {
        int ActorId { get; }

        void InitializeActor(int actorId);
        void PrepareSpawn();
        void ActivateActor();
        void DeactivateActor();
    }
}
