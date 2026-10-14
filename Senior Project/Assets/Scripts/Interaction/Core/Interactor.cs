using UnityEngine;
using UnityEngine.UI;

public class Interactor : MonoBehaviour
{
    public float castDistance = 5f; // how far caycast goes

    public Image crosshair = null; 
    public bool isCrosshairActive;

    private int interactableLayerMask; // Only detect objects on the Interactable layer

    private void Start()
    {
        interactableLayerMask = LayerMask.GetMask("Interactable");
    }

    private void Update()
    {
        if (doInteractionsTest(out Interactable interactable)) // check if player is looking at interactble object
        {
            CrosshairChange(true); // changes crosshiar color to red 

            if (InputManager.Instance.Actions.Player.Interact.WasPerformedThisFrame() && interactable.canInteract == true) // checks if object getting clicked is interactble
            {
                interactable.Interact(this); // tells the object to interact
            }
        }
        else
        {
            CrosshairChange(false); // changes crosshair color back to normal 
        }
    }

    //Ray cast
    public bool doInteractionsTest(out Interactable interactable) // will make cursor turn red if raycast is on interactable object
    {
        interactable = null;
        RaycastHit hit;

        Vector3 forward  = transform.TransformDirection(Vector3.forward); 
        Debug.DrawLine(transform.position, transform.position + forward * 5, Color.blue); //show the raycast

        if (Physics.Raycast(transform.position, forward, out hit, castDistance, interactableLayerMask))
        {
            interactable = hit.collider.GetComponent<Interactable>(); // Get the Interactable component from whatever the raycast hit

            isCrosshairActive = true;

            if (interactable != null) // check to see if there is anything to interact with
            {                
                return true; 
            }
                        
            return false;
        }
        else
        {
            isCrosshairActive = false;
        }
        
        return false;       
    }

    void CrosshairChange(bool on) // changes color of crosshair
    {
        if (on)
        {
            crosshair.color = Color.red; // if the player look at a interactible turn red
        }
        else
        {
            crosshair.color = Color.white; 
            isCrosshairActive = false;
        }
    }
}
