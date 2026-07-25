using System;
using SimulationCore.SimulationPhysics.Contract;
using SimulationCore.Unity.PhysicsRuntime.Application;
using UnityEngine;

namespace SimulationCore.Unity.PhysicsRuntime.Presentation
{
    public class OnTriggerEnterEvent : MonoBehaviour, IUnityCollisionRecordPort
    {
        Action<GameObject, GameObject, ContactPhase> collisionRecordCallback;
        public void Initial(Action<GameObject, GameObject, ContactPhase> collisionRecordCallback)
        {
            this.collisionRecordCallback = collisionRecordCallback;
        }
        public void OnTriggerEnter(Collider other)
        {
            collisionRecordCallback?.Invoke(this.gameObject, other.gameObject, ContactPhase.Enter);
        }
    }
}
