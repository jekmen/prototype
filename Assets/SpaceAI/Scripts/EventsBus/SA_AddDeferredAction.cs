using System;

namespace SpaceAI.Events
{
    internal class SA_AddDeferredAction : SA_IDeferredAction
    {
        public Action AddHandlerAction;

        public SA_AddDeferredAction(Action addHandlerAction)
        {
            AddHandlerAction = addHandlerAction;
        }

        public DeferredActions ActionType()
        {
            return DeferredActions.Add;
        }
    }
}