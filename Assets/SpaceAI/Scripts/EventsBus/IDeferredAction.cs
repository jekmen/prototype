namespace SpaceAI.Events
{
	internal interface IDeferredAction
	{
		DeferredActions ActionType();
	}

	internal enum DeferredActions
	{
		Remove,
		Publish,
		Add
	}
}