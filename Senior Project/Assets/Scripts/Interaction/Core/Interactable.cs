using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour
{
    [field: Header("Display")]
    [field: SerializeField] public string DisplayName { get; private set; } = "Interactable";
    [field: SerializeField] public bool DisplayOnlyWhenValidated { get; private set; } = false;
    
    [field: Header("Override")]
    [field: SerializeField, ReadOnly] public bool IsInteractionEnabled { get; private set; } = true;

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
