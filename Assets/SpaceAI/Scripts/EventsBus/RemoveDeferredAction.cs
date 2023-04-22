using System;

namespace SpaceAI.Events
{
	internal class RemoveDeferredAction : IDeferredAction
	{
		public Action RemoveHandlerAction;

		public RemoveDeferredAction(Action removeHandlerAction)
		{
			RemoveHandlerAction = removeHandlerAction;
		}

		public DeferredActions ActionType()
		{
			return DeferredActions.Remove;
		}
	}
}