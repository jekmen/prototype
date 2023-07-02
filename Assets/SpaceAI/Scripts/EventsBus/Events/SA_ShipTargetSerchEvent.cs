namespace SpaceAI.Events
{
    using SpaceAI.Ship;

    public struct SA_ShipTargetRequesEvent : SA_IEvent
    {
        public SA_IShip Ship;
        public int ScanRange;

        public SA_ShipTargetRequesEvent(SA_IShip owner, int scanRange)
        {
            Ship = owner;
            ScanRange = scanRange;
        }
    }
}