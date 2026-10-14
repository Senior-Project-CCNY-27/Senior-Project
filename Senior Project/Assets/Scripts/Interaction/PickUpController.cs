using UnityEngine;

public class PickUpController : MonoBehaviour
{
   [Header("Pickup Settings")]
    public Transform holdArea;
    public GameObject heldObject;
    public Rigidbody heldObjectRB;

    public float pickupRange = 5.0f;

    private int interactableLayerMask; // Only detect objects on the Interactable layer

    void Start()
    {
        interactableLayerMask = LayerMask.GetMask("Interactable");
    }

    // Update is called once per frame
    void Update()
    {
        if (InputManager.Instance.Actions.Player.Interact.WasPerformedThisFrame() && (heldObject == null)) // click E to pick up objects if not holding object 
        {
            RaycastHit hit;

            if (Physics.Raycast(transform.position, transform.forward, out hit, pickupRange, interactableLayerMask))
            {
                PickupObject(hit.transform.gameObject);
            }
        }

        if (InputManager.Instance.Actions.Player.Drop.WasPerformedThisFrame() && heldObject != null) // click G drop held object 
        {
            DropObject();
        }

        if (heldObject != null) // when holding an object
        {
            MoveObject();
        }

    }
    void MoveObject() // how the object moves when being held
    {
        heldObject.transform.position = holdArea.position;
        heldObject.transform.rotation = holdArea.rotation;
    }
    void PickupObject(GameObject pickObject) // how objects are being picked up, object with a rigidbody
    {
        Rigidbody rb = pickObject.GetComponent<Rigidbody>();

        if (rb != null)
        {
            heldObject = pickObject;
            heldObjectRB = rb;

            heldObjectRB.useGravity = false;
            heldObjectRB.constraints = RigidbodyConstraints.FreezeRotation;
        }
    }
    void DropObject() // how objects are beings dropped
    {
        if (heldObject == null)
        {
            return;
        }

        heldObjectRB.useGravity = true;
        heldObjectRB.constraints = RigidbodyConstraints.None;

        heldObject = null;
        heldObjectRB = null;
    }

    
}
