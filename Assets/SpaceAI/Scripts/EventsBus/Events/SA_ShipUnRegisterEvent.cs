using SpaceAI.Ship;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI.Events
{
    public struct SA_ShipUnRegisterEvent : SA_IEvent
    {
        public SA_IShip Ship;

        public SA_ShipUnRegisterEvent(SA_IShip ship)
        {
            Ship = ship;
        }
    }
}