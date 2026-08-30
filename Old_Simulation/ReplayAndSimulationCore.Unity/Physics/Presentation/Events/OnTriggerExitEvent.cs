using System;
using SimulationCore.SimulationPhysics.Contract;
using SimulationCore.Unity.PhysicsRuntime.Application;
using UnityEngine;

namespace SimulationCore.Unity.PhysicsRuntime.Presentation
{
    public class OnTriggerExitEvent : MonoBehaviour, IUnityCollisionRecordPort
    {
        Action<GameObject, GameObject, ContactPhase> collisionRecordCallback;
        public void Initial(Action<GameObject, GameObject, ContactPhase> collisionRecordCallback)
        {
            this.collisionRecordCallback = collisionRecordCallback;
        }
        public void OnTriggerExit(Collider other)
        {
            collisionRecordCallback?.Invoke(this.gameObject, other.gameObject, ContactPhase.Exit);
        }
    }
}
