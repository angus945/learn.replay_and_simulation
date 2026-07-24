// using SimulationCore.World.Contract;
// using SimulationCore.Unity.Actor.Contract;
// using UnityEngine;

// public class Enemy : MonoBehaviour, IUnityEntityActor
// {
//     [SerializeField] private Rigidbody body;

//     private int resourceId = -1;
//     private EntityHandle entity;
//     private IUnityFactSink factSink;
//     private bool bound;

//     public int ResourceId => resourceId;
//     public EntityHandle Entity => entity;
//     public IUnityFactSink FactSink => factSink;
//     public bool IsBound => bound;
//     public Rigidbody Rigidbody
//     {
//         get
//         {
//             if (!body)
//                 body = GetComponent<Rigidbody>();

//             return body;
//         }
//     }

//     private void Awake()
//     {
//         if (!body)
//             body = GetComponent<Rigidbody>();
//     }

//     public void Initial(int resourceId)
//     {
//         this.resourceId = resourceId;
//     }

//     public void Bind(EntityHandle entity, IUnityFactSink factSink)
//     {
//         this.entity = entity;
//         this.factSink = factSink;
//         bound = true;
//     }

//     public void PrepareActivate()
//     {
//     }

//     public void Activate()
//     {
//         gameObject.SetActive(true);
//     }

//     public void Deactivate()
//     {
//         gameObject.SetActive(false);
//     }

//     public void Unbind()
//     {
//         entity = default;
//         factSink = null;
//         bound = false;
//     }
// }
