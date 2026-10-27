using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour
{
    [field: SerializeField] public string DisplayName { get; private set; } = "Interactable";
    [field: SerializeField] public bool IsInteractionEnabled { get; private set; } = true;

    [field: Header("Event")]
    [field: SerializeField] public UnityEvent<Interactor> OnInteract { get; private set; } = new();
    
    public void EnableInteractability(bool canInteract) => IsInteractionEnabled = canInteract;
    
    public virtual void Interact(Interactor interactor)
    {
        OnInteract.Invoke(interactor);
    }

    public bool CanInteractAfterValidation(Interactor interactor)
    {
        IInteractionValidator[] validators = GetComponents<IInteractionValidator>();
        foreach (IInteractionValidator validator in validators)
        {
            if (!validator.CanInteract(interactor))
                return false;
        }

        return true;
    }
}
