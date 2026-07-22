using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class UnityActorFactory<T> : IActorFactory<T> where T : MonoBehaviour, IPhysicalActor
{
    private readonly T prefab;
    private readonly Transform parent;

    public UnityActorFactory(T prefab, Transform parent)
    {
        this.prefab = prefab ? prefab
            : throw new ArgumentNullException(nameof(prefab));

        this.parent = parent ? parent
            : throw new ArgumentNullException(nameof(parent));

        parent.gameObject.SetActive(false);
    }

    public T CreateActor()
    {
        return UnityEngine.Object.Instantiate(prefab, parent);
    }
}
