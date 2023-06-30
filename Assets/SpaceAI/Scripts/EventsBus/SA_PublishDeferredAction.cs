namespace SpaceAI.Events
{
	internal class SA_PublishDeferredAction : SA_IDeferredAction
	{
		public SA_IEvent Event;

		public SA_PublishDeferredAction(SA_IEvent @event)
		{
			Event = @event;
		}

		public DeferredActions ActionType()
		{
			return DeferredActions.Publish;
		}
	}
}