using System;
using SimulationCore.SimulationPhysics.Contract;
using SimulationCore.Unity.PhysicsRuntime.Application;
using UnityEngine;

namespace SimulationCore.Unity.PhysicsRuntime.Presentation
{
    public class OnCollisionExitEvent : MonoBehaviour, IUnityCollisionRecordPort
    {
        Action<GameObject, GameObject, ContactPhase> collisionRecordCallback;
        public void Initial(Action<GameObject, GameObject, ContactPhase> collisionRecordCallback)
        {
            this.collisionRecordCallback = collisionRecordCallback;
        }
        public void OnCollisionExit(Collision collision)
        {
            collisionRecordCallback?.Invoke(this.gameObject, collision.gameObject, ContactPhase.Exit);
        }
    }
}
