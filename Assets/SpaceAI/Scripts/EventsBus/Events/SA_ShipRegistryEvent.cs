namespace SpaceAI.Events
{
    using SpaceAI.Ship;

    public struct SA_ShipRegistryEvent : SA_IEvent
    {
        public SA_IShip Ship;

        public SA_ShipRegistryEvent(SA_IShip ship)
        {
            Ship = ship;
        }
    }

    public struct SA_ShipSystemsInitedEvent : SA_IEvent
    {

    }
}