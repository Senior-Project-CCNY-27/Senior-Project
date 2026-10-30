using UnityEngine;

public class KeyPickup : MonoBehaviour
{
    public GameObject key;
    public bool keyPickedUp;

    public void PickupKey(Interactor interactor)
    {
        keyPickedUp = true; 
        Destroy(key);
    }
}
