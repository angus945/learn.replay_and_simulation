// using UnityActorPool.Contract;

// namespace UnityActorPool.API
// {
//     public interface IUnityActorPoolService
//     {
//         ActorLease<IUnityEntityActor> AcquireActor(AcquireActorCommand command);
//         ActorLease<T> AcquireActor<T>(AcquireActorCommand command) where T : class, IUnityEntityActor;
//         ActorLease<IUnityEntityActor> AcquireActorAt(AcquireActorAtCommand command);
//         ActorLease<T> AcquireActorAt<T>(AcquireActorAtCommand command) where T : class, IUnityEntityActor;
//         void ActivatePendingActors(ActivatePendingActorsCommand command);
//         void ReleaseActor(ReleaseActorCommand command);
//     }

// }
