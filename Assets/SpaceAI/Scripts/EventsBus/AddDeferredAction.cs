using System;

namespace SpaceAI.Events
{
    internal class AddDeferredAction : IDeferredAction
    {
        public Action AddHandlerAction;

        public AddDeferredAction(Action addHandlerAction)
        {
            AddHandlerAction = addHandlerAction;
        }

        public DeferredActions ActionType()
        {
            return DeferredActions.Add;
        }
    }
}