public interface IInteractable
{
	bool CanInteract(PropInteractionContext context);
	PropInteractionResult Interact(PropInteractionContext context);
}
