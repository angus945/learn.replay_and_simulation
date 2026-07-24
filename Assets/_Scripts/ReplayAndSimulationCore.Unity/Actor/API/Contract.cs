// using ECSManagement.Contract;
// using UnityEngine;

// namespace SimulationCore.Unity.UnityActor.Contract
// {


//     public interface IUnityEntityActor
//     {
//         Transform Transform { get; }
//         GameObject GameObject { get; }

//         void Initial(int actorId);
//         // void Bind(EntityHandle entity, IUnityFactSink factSink);
//         // void Unbind();
//     }

//     public readonly struct ActorHandle
//     {
//         public int PoolId { get; }
//         public int ResourceId { get; }
//         public uint Generation { get; }

//         public ActorHandle(int poolId, int resourceId, uint generation)
//         {
//             PoolId = poolId;
//             ResourceId = resourceId;
//             Generation = generation;
//         }
//     }

//     public readonly struct ActorLease<T> where T : class, IUnityEntityActor
//     {
//         public readonly ActorHandle Handle;
//         public readonly T Actor;
//         public readonly UnityActor UnityActor;

//         public ActorLease(ActorHandle handle, T actor, UnityActor unityActor)
//         {
//             Handle = handle;
//             Actor = actor;
//             UnityActor = unityActor;
//         }
//     }

//     public readonly struct AcquireActorCommand
//     {
//         public readonly int PoolId;

//         public AcquireActorCommand(int poolId)
//         {
//             PoolId = poolId;
//         }
//     }

//     public readonly struct AcquireActorAtCommand
//     {
//         public readonly int PoolId;
//         public readonly int ResourceId;

//         public AcquireActorAtCommand(int poolId, int resourceId)
//         {
//             PoolId = poolId;
//             ResourceId = resourceId;
//         }
//     }

//     public readonly struct ActivatePendingActorsCommand
//     {

//     }

//     public readonly struct ReleaseActorCommand
//     {
//         public readonly ActorHandle Handle;

//         public ReleaseActorCommand(ActorHandle handle)
//         {
//             Handle = handle;
//         }
//     }
// }
