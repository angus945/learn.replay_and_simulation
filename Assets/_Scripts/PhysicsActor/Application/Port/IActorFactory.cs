public interface IActorFactory<out T> where T : class, IPhysicalActor
{
    T CreateActor();
}
