namespace SpaceAI.Events
{
	internal class PublishDeferredAction : IDeferredAction
	{
		public IEvent Event;

		public PublishDeferredAction(IEvent @event)
		{
			Event = @event;
		}

		public DeferredActions ActionType()
		{
			return DeferredActions.Publish;
		}
	}
}