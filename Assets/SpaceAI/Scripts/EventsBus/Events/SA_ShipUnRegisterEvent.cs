namespace SpaceAI.Events
{
    using SpaceAI.Ship;

    public struct SA_ShipUnRegisterEvent : SA_IEvent
    {
        public SA_IShip Ship;

        public SA_ShipUnRegisterEvent(SA_IShip ship)
        {
            Ship = ship;
        }
    }
}