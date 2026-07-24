
using SimulationCore.SimulationActor.Domain;
using SimulationCore.Contracts;
using SimulationCore.SimulationActor.Application.Port;
using SimulationCore.SimulationActor.Contract;

namespace SimulationCore.SimulationActor
{
    public sealed class SimulationActors : ISimulationActor
    {
        ActorPools actorPools;
        IActorInstancePort actorInstancePort;

        public SimulationActors(IActorInstancePort actorInstancePort)
        {
            this.actorPools = new ActorPools();
            this.actorInstancePort = actorInstancePort;
        }
        public void RegisterActorPool<T>(int poolId, int capacity) where T : class, IActor
        {
            T[] actorInstances = actorInstancePort.CreateActorInstances<T>(poolId, capacity);

            ActorPool<T> pool = new ActorPool<T>(poolId, actorInstances);
            actorPools.AddPool<T>(pool);
        }

        public void ReconcileAfterStructuralCommit()
        {
            throw new System.NotImplementedException();
        }
    }
}

// {
//     public sealed class UnityActorPoolService
//     {
//         private readonly RegisterActorPoolUseCase registerActorPool;
//         private readonly InitializeActorPoolsUseCase initializeActorPools;
//         private readonly AcquireActorUseCase acquireActor;
//         private readonly AcquireActorAtUseCase acquireActorAt;
//         private readonly ActivatePendingActorsUseCase activatePendingActors;
//         private readonly ReleaseActorUseCase releaseActor;

//         public UnityActorPoolService()
//         {
//             UnityActorPoolState state = new UnityActorPoolState();

//             registerActorPool = new RegisterActorPoolUseCase(state);
//             initializeActorPools = new InitializeActorPoolsUseCase(state);
//             acquireActor = new AcquireActorUseCase(state);
//             acquireActorAt = new AcquireActorAtUseCase(state);
//             activatePendingActors = new ActivatePendingActorsUseCase(state);
//             releaseActor = new ReleaseActorUseCase(state);
//         }



//         public void InitializeActorPools()
//         {
//             initializeActorPools.Execute();
//         }

//         public ActorLease<IUnityEntityActor> AcquireActor(AcquireActorCommand command)
//         {
//             return acquireActor.Execute(command);
//         }

//         public ActorLease<T> AcquireActor<T>(AcquireActorCommand command) where T : class, IUnityEntityActor
//         {
//             return CastLease<T>(acquireActor.Execute(command));
//         }

//         public ActorLease<IUnityEntityActor> AcquireActorAt(AcquireActorAtCommand command)
//         {
//             return acquireActorAt.Execute(command);
//         }

//         public ActorLease<T> AcquireActorAt<T>(AcquireActorAtCommand command) where T : class, IUnityEntityActor
//         {
//             return CastLease<T>(acquireActorAt.Execute(command));
//         }

//         public void ActivatePendingActors(ActivatePendingActorsCommand command)
//         {
//             activatePendingActors.Execute(command);
//         }

//         public void ReleaseActor(ReleaseActorCommand command)
//         {
//             releaseActor.Execute(command);
//         }

//         private static ActorLease<T> CastLease<T>(ActorLease<IUnityEntityActor> lease) where T : class, IUnityEntityActor
//         {
//             if (lease.Actor is T actor)
//                 return new ActorLease<T>(lease.Handle, actor, lease.UnityActor);

//             throw new InvalidOperationException(
//                 $"Acquired actor is {lease.Actor.GetType().Name}, not {typeof(T).Name}.");
//         }
//     }


// }
