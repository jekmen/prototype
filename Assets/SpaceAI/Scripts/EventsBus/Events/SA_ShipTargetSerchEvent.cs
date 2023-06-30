using SpaceAI.Ship;

namespace SpaceAI.Events
{
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