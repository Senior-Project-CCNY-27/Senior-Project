public interface IInteractable
{
    public bool canInteract(); // checks if the player can interact with the object
    public void Interact(Interactor interactor); // what happens when the player interacts with the object
}
