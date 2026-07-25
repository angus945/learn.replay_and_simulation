using System;
using SimulationCore.SimulationPhysics.Contract;
using SimulationCore.Unity.PhysicsRuntime.Application;
using UnityEngine;

namespace SimulationCore.Unity.PhysicsRuntime.Presentation
{
    public class OnCollisionEnterEvent : MonoBehaviour, IUnityCollisionRecordPort
    {
        Action<GameObject, GameObject, ContactPhase> collisionRecordCallback;
        public void Initial(Action<GameObject, GameObject, ContactPhase> collisionRecordCallback)
        {
            this.collisionRecordCallback = collisionRecordCallback;
        }
        public void OnCollisionEnter(Collision collision)
        {
            collisionRecordCallback?.Invoke(this.gameObject, collision.gameObject, ContactPhase.Enter);
        }
    }
}
