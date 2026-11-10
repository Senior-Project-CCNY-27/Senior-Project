using UnityEngine;

public class Key : MonoBehaviour
{
    private KeyHolder _keyHolder;

    private void Awake()
    {
        // The KeyHolder is on the player, so we get it during pickup.
    }

    public void PickupKey(Interactor interactor)
    {
        _keyHolder = interactor.GetComponent<KeyHolder>();

        if (_keyHolder == null)
            return;

        _keyHolder.PickupKey();
    }

    public void DropKey()
    {
        if (_keyHolder == null)
            return;

        _keyHolder.DropKey();
        _keyHolder = null;
    }
}