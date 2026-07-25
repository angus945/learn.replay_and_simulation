using System;
using SimulationCore.SimulationPhysics.Application;
using SimulationCore.SimulationPhysics.Contract;
using SimulationCore.Unity.PhysicsRuntime.Application;
using UnityEngine;

namespace SimulationCore.Unity.PhysicsRuntime.Presentation
{
    public class OnTriggerStayEvent : MonoBehaviour, IUnityCollisionRecordPort
    {
        Action<GameObject, GameObject, ContactPhase> collisionRecordCallback;
        public void Initial(Action<GameObject, GameObject, ContactPhase> collisionRecordCallback)
        {
            this.collisionRecordCallback = collisionRecordCallback;
        }
        public void OnTriggerStay(Collider other)
        {
            collisionRecordCallback?.Invoke(this.gameObject, other.gameObject, ContactPhase.Stay);
        }
    }
}
