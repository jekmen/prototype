using SpaceAI.Ship;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI.Events
{
    public struct ShipUnRegisterEvent : IEvent
    {
        public IShip Ship;

        public ShipUnRegisterEvent(IShip ship)
        {
            Ship = ship;
        }
    }
}