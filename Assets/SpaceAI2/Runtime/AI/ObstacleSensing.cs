using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace SpaceAI2
{
    public class ObstacleSensing : IDisposable
    {
        private const int ProbesPerShip = 5;
        private const int MaxHits = 4;
        private const float SideProbeAngle = 30f;

        private readonly List<AIPilot> batch = new List<AIPilot>();
        private readonly List<ProbeSetup> setups = new List<ProbeSetup>();
        private NativeArray<SpherecastCommand> commands;
        private NativeArray<RaycastHit> results;

        public int Interval { get; set; } = 3;
        public LayerMask Mask { get; set; } = Physics.DefaultRaycastLayers;

        public void Run(List<AIPilot> pilots, ShipRegistry registry, int frame)
        {
            batch.Clear();
            setups.Clear();
            int interval = Mathf.Max(1, Interval);
            for (int i = 0; i < pilots.Count; i++)
            {
                AIPilot pilot = pilots[i];
                if ((i + frame) % interval != 0 || !pilot.TryPrepareProbes(out ProbeSetup setup)) continue;
                batch.Add(pilot);
                setups.Add(setup);
            }
            if (batch.Count == 0) return;

            int count = batch.Count * ProbesPerShip;
            EnsureCapacity(count);
            var query = new QueryParameters(Mask, false, QueryTriggerInteraction.Ignore, false);
            float sin = Mathf.Sin(SideProbeAngle * Mathf.Deg2Rad);
            float cos = Mathf.Cos(SideProbeAngle * Mathf.Deg2Rad);

            for (int i = 0; i < setups.Count; i++)
            {
                ProbeSetup s = setups[i];
                int k = i * ProbesPerShip;
                commands[k] = new SpherecastCommand(s.Origin, s.Radius, s.Forward, query, s.Range);
                for (int side = 0; side < 4; side++)
                {
                    Vector3 direction = s.Forward * cos + s.SideDirection((ProbeSide)side) * sin;
                    commands[k + 1 + side] = new SpherecastCommand(s.Origin, s.Radius, direction, query, s.Range);
                }
            }

            SpherecastCommand.ScheduleBatch(commands.GetSubArray(0, count), results.GetSubArray(0, count * MaxHits), 8, MaxHits).Complete();

            for (int i = 0; i < batch.Count; i++)
            {
                ProbeSetup s = setups[i];
                int k = i * ProbesPerShip;
                var reading = new ObstacleReading { Probe = s };
                reading.Blocked = Nearest(k, registry, s.Range, out reading.Distance, out reading.Normal);
                reading.FreeUp = FreeDistance(k + 1, registry, s.Range);
                reading.FreeDown = FreeDistance(k + 2, registry, s.Range);
                reading.FreeLeft = FreeDistance(k + 3, registry, s.Range);
                reading.FreeRight = FreeDistance(k + 4, registry, s.Range);
                batch[i].ReceiveReading(reading);
            }
        }

        public void Dispose()
        {
            if (commands.IsCreated) commands.Dispose();
            if (results.IsCreated) results.Dispose();
        }

        private float FreeDistance(int command, ShipRegistry registry, float range) =>
            Nearest(command, registry, range, out float distance, out _) ? distance : range;

        private bool Nearest(int command, ShipRegistry registry, float range, out float distance, out Vector3 normal)
        {
            distance = range;
            normal = Vector3.zero;
            bool found = false;
            for (int j = 0; j < MaxHits; j++)
            {
                RaycastHit hit = results[command * MaxHits + j];
                int id = hit.colliderInstanceID;
                if (id == 0) break;
                if (registry.IsShipCollider(id) || hit.distance >= distance) continue;
                distance = hit.distance;
                normal = hit.normal;
                found = true;
            }
            return found;
        }

        private void EnsureCapacity(int count)
        {
            if (commands.IsCreated && commands.Length >= count) return;
            Dispose();
            int capacity = Mathf.NextPowerOfTwo(count);
            commands = new NativeArray<SpherecastCommand>(capacity, Allocator.Persistent);
            results = new NativeArray<RaycastHit>(capacity * MaxHits, Allocator.Persistent);
        }
    }
}
