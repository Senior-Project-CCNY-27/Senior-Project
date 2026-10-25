using UnityEngine;

public class Interactable : MonoBehaviour
{
    [field: SerializeField] public string DisplayName { get; private set; } = "Interactable";
    [field: SerializeField] public bool CanInteract { get; private set; } = true;

    public virtual void Interact(Interactor interactor)
    {
        Debug.Log("I Interacted");
    }
}
