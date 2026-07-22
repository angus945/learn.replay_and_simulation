using System;
using PhysicsActor;
using PhysicsActor.Application.Port;
using UnityEngine;

namespace PhysicsActor.Unity.Infrastructure
{
    public sealed class UnityActorFactory<T> : IActorFactory<T> where T : MonoBehaviour, IPhysicalActor
    {
        private readonly T prefab;
        private readonly Transform prefabsParent;

        public UnityActorFactory(T prefab, Transform parent)
        {
            this.prefab = prefab ? prefab
                : throw new ArgumentNullException(nameof(prefab));

            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            prefabsParent = new GameObject($"{typeof(T).Name} Prefabs").transform;
            prefabsParent.gameObject.SetActive(false);
            prefabsParent.SetParent(parent, false);
        }

        public T CreateActor()
        {
            return UnityEngine.Object.Instantiate(prefab, prefabsParent);
        }
    }
}
