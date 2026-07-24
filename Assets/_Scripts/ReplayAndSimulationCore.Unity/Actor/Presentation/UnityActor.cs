
// using System;
// using ECSManagement.Contract;
// using TickPhysicsSystem.Unity;
// using UnityActorPool.Contract;
// using UnityEngine;

// namespace UnityActorPool
// {
//     public interface IUnityActor
//     {
//         EntityHandle EntityHandle { get; }
//         ActorHandle ActorHandle { get; }
//     }
//     public class UnityActor : MonoBehaviour
//     {
//         public EntityHandle EntityHandle { get; private set; }
//         public ActorHandle ActorHandle { get; private set; }
//         IUnityFactSink factSink;

//         internal void Initial(int slotId, IUnityFactSink factSink)
//         {
//             this.factSink = factSink;
//         }
//         public void BindEntity(EntityHandle entityHandle, ActorHandle actorHandle)
//         {
//             EntityHandle = entityHandle;
//             ActorHandle = actorHandle;
//         }
//         public void UnbindEntity()
//         {
//             EntityHandle = new EntityHandle(-1, 0, 0); // Reset to an invalid entity handle
//             ActorHandle = new ActorHandle(-1, 0, 0); // Reset to an invalid actor handle
//             factSink = null;
//         }

//         // !! 即使裡面完全是空的，只要 Trigger 持續重疊，Unity 仍需要反覆呼叫它。因此大量物件時，尤其應避免不必要的
//         // TODO 之後改成可配置的 Collision Fact Sink
//         void OnCollisionEnter(Collision collision)
//         {
//             EntityHandle otherEntity = GetOtherEntityHandle(collision.gameObject);
//             CollisionFact collisionFact = new CollisionFact(EntityHandle, otherEntity, ContactPhase.Enter);

//             factSink.RecordCollision(collisionFact);
//         }
//         void OnCollisionStay(Collision collision)
//         {
//             EntityHandle otherEntity = GetOtherEntityHandle(collision.gameObject);
//             CollisionFact collisionFact = new CollisionFact(EntityHandle, otherEntity, ContactPhase.Stay);

//             factSink.RecordCollision(collisionFact);
//         }
//         void OnCollisionExit(Collision collision)
//         {
//             EntityHandle otherEntity = GetOtherEntityHandle(collision.gameObject);
//             CollisionFact collisionFact = new CollisionFact(EntityHandle, otherEntity, ContactPhase.Exit);

//             factSink.RecordCollision(collisionFact);
//         }
//         void OnTriggerEnter(Collider other)
//         {
//             EntityHandle otherEntity = GetOtherEntityHandle(other.gameObject);
//             CollisionFact collisionFact = new CollisionFact(EntityHandle, otherEntity, ContactPhase.Enter);

//             factSink.RecordCollision(collisionFact);
//         }
//         void OnTriggerStay(Collider other)
//         {
//             EntityHandle otherEntity = GetOtherEntityHandle(other.gameObject);
//             CollisionFact collisionFact = new CollisionFact(EntityHandle, otherEntity, ContactPhase.Stay);

//             factSink.RecordCollision(collisionFact);
//         }
//         void OnTriggerExit(Collider other)
//         {
//             EntityHandle otherEntity = GetOtherEntityHandle(other.gameObject);
//             CollisionFact collisionFact = new CollisionFact(EntityHandle, otherEntity, ContactPhase.Exit);

//             factSink.RecordCollision(collisionFact);
//         }
//         EntityHandle GetOtherEntityHandle(GameObject other)
//         {
//             if (other.TryGetComponent<IUnityActor>(out IUnityActor otherTag))
//             {
//                 return otherTag.EntityHandle;
//             }
//             else
//             {
//                 return new EntityHandle(-1, 0, 0); // Placeholder for non-entity objects
//             }
//         }


//     }
// }