using NUnit.Framework;
using UnityEngine;

public class Lever : MonoBehaviour, IInteractable
{
    public bool isOpen = false;
    public bool canInteract()
    {
        return true;
    }

    public void Interact(Interactor interactor)
    {
        if (isOpen)
        {
            transform.localRotation = Quaternion.Euler(35, 0, 0); // closes the door
        }
        else
        {
            transform.localRotation = Quaternion.Euler(-35, 0, 0); // opens the door
        }

        isOpen = !isOpen; 
    }

    
}
