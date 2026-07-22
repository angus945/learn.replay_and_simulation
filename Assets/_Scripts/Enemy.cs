using UnityEngine;

public class Enemy : MonoBehaviour, IPhysicalActor
{
    public int health = 100;

    public int ActorId => throw new System.NotImplementedException();

    public void ActivateActor()
    {
        gameObject.SetActive(true);
    }

    public void DeactivateActor()
    {
        gameObject.SetActive(false);
    }

    public void InitializeActor(int actorId)
    {

    }

    public void PrepareSpawn()
    {

    }
}