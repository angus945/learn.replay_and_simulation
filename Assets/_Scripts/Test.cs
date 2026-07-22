using PhysicsActor.Application;
using PhysicsActor.Unity.Infrastructure;
using UnityEngine;

public class TestCompositionRoot : MonoBehaviour
{
    [SerializeField] private Enemy enemyPrefab;

    void Start()
    {
        UnityActorFactory<Enemy> enemyFactory = new UnityActorFactory<Enemy>(enemyPrefab, transform);

        ApplicationService application = new ApplicationService();

        application.RegisterActorPool<Enemy>(0, 10, enemyFactory);
        application.InitializeActorPools();
    }

}
