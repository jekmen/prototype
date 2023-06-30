namespace SpaceAI.Events
{
	internal interface SA_IDeferredAction
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