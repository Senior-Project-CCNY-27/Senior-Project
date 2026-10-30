using NaughtyAttributes;
using UnityEngine;

public class KeyHolder : MonoBehaviour
{
    [field: SerializeField, ReadOnly] public bool KeyPickedUp { get; private set; }

    public void PickupKey()
    {
        KeyPickedUp = true; 
    }
}