using UnityEngine;

public class Interactable : MonoBehaviour
{
    public bool canInteract = true;

    public virtual void Interact(Interactor interactor)
    {
        Debug.Log("I Interacted");
    }
}
