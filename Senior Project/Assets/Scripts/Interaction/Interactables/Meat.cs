using UnityEngine;

public class Meat : Interactable
{
    public override void Interact(Interactor interactor) // what happens when the player interacts with the object
    {
        Debug.Log("Interacted with meat");
    }
}
