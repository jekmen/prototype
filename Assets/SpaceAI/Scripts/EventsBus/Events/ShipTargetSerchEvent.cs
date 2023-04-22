using SpaceAI.Ship;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI.Events
{
    public struct ShipTargetRequesEvent : IEvent
    {
        public IShip Ship;
        public int ScanRange;

        public ShipTargetRequesEvent(IShip owner, int scanRange)
        {
            Ship = owner;
            ScanRange = scanRange;
        }
    }
}