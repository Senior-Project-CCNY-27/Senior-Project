using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour
{
    private IInteractionValidator[] _interactionValidators;

    [field: Header("Display")]
    [field: SerializeField] public string DisplayName { get; private set; } = "Interactable";
    [field: SerializeField] public bool DisplayOnlyWhenValidated { get; private set; } = false;
    
    [field: Header("Override")]
    [field: SerializeField, ReadOnly] public bool IsInteractionEnabled { get; private set; } = true;

    [field: Header("Event")]
    [field: SerializeField] public UnityEvent<Interactor> OnInteract { get; private set; } = new();

    private void OnValidate()
    {
        if(gameObject.layer != LayerMask.NameToLayer("Interactable"))
            gameObject.layer = LayerMask.NameToLayer("Interactable");
    }

    private void Awake()
    {
        _interactionValidators = GetComponentsInChildren<IInteractionValidator>(true);
    }

    public void EnableInteractability(bool canInteract) => IsInteractionEnabled = canInteract;
    
    public virtual void Interact(Interactor interactor)
    {
        OnInteract.Invoke(interactor);
    }

    public bool CanInteractAfterValidation(Interactor interactor)
    {
        foreach (IInteractionValidator validator in _interactionValidators)
        {
            if (!validator.CanInteract(interactor))
                return false;
        }

        return true;
    }
}
