using SpaceAI.Ship;
using System;
using System.Threading.Tasks;
using UnityEngine;

public abstract class SA_ShipSystem : ScriptableObject
{
    protected SA_BaseShip ship;
    protected Action m_event;

    public SA_ShipSystem(SA_BaseShip ship, bool enableLoop = false)
    {
        this.ship = ship;
        if(enableLoop) RunTime();
    }

    public abstract void CollisionEvent(Collision collision);

    private async void RunTime()
    {
        while (Application.isPlaying && ship)
        {
            m_event?.Invoke();
            await Task.Yield();
        }
    }
}