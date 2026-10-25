using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour
{
    [field: SerializeField] public string DisplayName { get; private set; } = "Interactable";
    [field: SerializeField] public bool CanInteract { get; private set; } = true;

    [field: Header("Event")]
    [field: SerializeField] public UnityEvent<Interactor> OnInteract { get; private set; } = new();
    
    public virtual void Interact(Interactor interactor)
    {
        OnInteract.Invoke(interactor);
    }
}
