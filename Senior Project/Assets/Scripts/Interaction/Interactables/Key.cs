using UnityEngine;

public class Key : MonoBehaviour
{
    public void PickupKey(Interactor interactor)
    {
        KeyHolder keyHolder = interactor.GetComponent<KeyHolder>();
        keyHolder.PickupKey();
        
        Destroy(gameObject);
    }
}