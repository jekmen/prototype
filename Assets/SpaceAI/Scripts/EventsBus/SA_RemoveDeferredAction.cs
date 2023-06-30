using System;

namespace SpaceAI.Events
{
	internal class SA_RemoveDeferredAction : SA_IDeferredAction
	{
		public Action RemoveHandlerAction;

		public SA_RemoveDeferredAction(Action removeHandlerAction)
		{
			RemoveHandlerAction = removeHandlerAction;
		}

		public DeferredActions ActionType()
		{
			return DeferredActions.Remove;
		}
	}
}