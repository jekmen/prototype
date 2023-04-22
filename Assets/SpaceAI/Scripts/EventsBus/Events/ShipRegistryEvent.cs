using SpaceAI.Ship;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI.Events
{
    public struct ShipRegistryEvent : IEvent
    {
        public IShip Ship;

        public ShipRegistryEvent(IShip ship)
        {
            Ship = ship;
        }
    }

    public struct ShipSystemsInitedEvent : IEvent
    {

    }
}
