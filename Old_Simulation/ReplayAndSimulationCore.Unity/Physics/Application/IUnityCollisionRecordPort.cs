using System;
using SimulationCore.SimulationPhysics.Contract;
using UnityEngine;

namespace SimulationCore.Unity.PhysicsRuntime.Application
{
    public interface IUnityCollisionRecordPort
    {
        void Initial(Action<GameObject, GameObject, ContactPhase> collisionRecordCallback);
    }
}

