using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public class KeyHolder : MonoBehaviour
{
    [field: SerializeField, ReadOnly] public bool KeyPickedUp { get; private set; }

    public void PickupKey()
    {
        KeyPickedUp = true; 
    }

    public void DropKey()
    {
        KeyPickedUp = false; 
    }
}