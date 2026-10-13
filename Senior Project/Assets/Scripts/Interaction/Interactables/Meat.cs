using Unity.Mathematics;
using UnityEngine;

public class Meat : MonoBehaviour, IInteractable
{
    //public Transform playersHand;
   // public bool Holding = false;

    public bool canInteract() // checks if the player can interact with the object
    {
        return true;
    }
    
    // void Update()
    // {
    //     if(Holding == true) // pick up 
    //     {
    //         transform.position = playersHand.position;
    //         transform.rotation = playersHand.rotation;

    //         if (Input.GetMouseButtonDown(0))
    //         {
    //             Rigidbody rb = GetComponent<Rigidbody>();
    //             Holding = false;
    //             rb.useGravity = true;
    //         }
    //     }
        
    // }
    public void Interact(Interactor interactor) // what happens when the player interacts with the object
    {
        // Rigidbody rb = GetComponent<Rigidbody>();
        // rb.useGravity = false; // disables gravity on the meat object

        // Holding = true;
        Debug.Log("Interacted with meat");
    }
}
