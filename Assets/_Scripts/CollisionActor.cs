using UnityEngine;

public class CollisionActor : MonoBehaviour
{
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"Collision detected between {gameObject.name} and {collision.gameObject.name}");
    }
    void OnCollisionStay(Collision collision)
    {
        Debug.Log($"Collision ongoing between {gameObject.name} and {collision.gameObject.name}");
    }
    void OnCollisionExit(Collision collision)
    {
        Debug.Log($"Collision ended between {gameObject.name} and {collision.gameObject.name}");
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Trigger entered by {other.gameObject.name} on {gameObject.name}");
    }
    void OnTriggerStay(Collider other)
    {
        Debug.Log($"Trigger ongoing by {other.gameObject.name} on {gameObject.name}");
    }
    void OnTriggerExit(Collider other)
    {
        Debug.Log($"Trigger exited by {other.gameObject.name} on {gameObject.name}");
    }

}