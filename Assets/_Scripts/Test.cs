using UnityEngine;

public class Test : MonoBehaviour
{
    [SerializeField] private Enemy enemyPrefab;
    ActorPoolRegistry poolRegistry = new ActorPoolRegistry();

    void Start()
    {
        ActorPool<Enemy> pool = new ActorPool<Enemy>(0, new UnityActorFactory<Enemy>(enemyPrefab, transform), 10);
        poolRegistry.Register(pool);

        pool.Initialize();

    }

}