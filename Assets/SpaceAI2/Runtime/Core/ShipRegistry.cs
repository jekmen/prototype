using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI2
{
    public class ShipRegistry
    {
        private readonly float cellSize;
        private readonly List<SpaceShip> ships = new List<SpaceShip>();
        private readonly Dictionary<Vector3Int, List<SpaceShip>> cells = new Dictionary<Vector3Int, List<SpaceShip>>();
        private readonly Stack<List<SpaceShip>> pool = new Stack<List<SpaceShip>>();
        private readonly List<SpaceShip> scratch = new List<SpaceShip>();
        private readonly Dictionary<int, SpaceShip> shipColliders = new Dictionary<int, SpaceShip>();
        private readonly List<Collider> colliderScratch = new List<Collider>();

        public ShipRegistry(float cellSize) => this.cellSize = Mathf.Max(1f, cellSize);

        public IReadOnlyList<SpaceShip> All => ships;

        public void Add(SpaceShip ship)
        {
            if (ships.Contains(ship)) return;
            ships.Add(ship);
            ship.GetComponentsInChildren(true, colliderScratch);
            foreach (Collider collider in colliderScratch) shipColliders[collider.GetInstanceID()] = ship;
        }

        public void Remove(SpaceShip ship)
        {
            if (!ships.Remove(ship)) return;
            ship.GetComponentsInChildren(true, colliderScratch);
            foreach (Collider collider in colliderScratch) shipColliders.Remove(collider.GetInstanceID());
        }

        public bool IsShipCollider(int colliderInstanceId) => shipColliders.ContainsKey(colliderInstanceId);

        public bool TryGetShipByCollider(int colliderInstanceId, out SpaceShip ship) => shipColliders.TryGetValue(colliderInstanceId, out ship);

        public void Rebuild()
        {
            foreach (List<SpaceShip> cell in cells.Values)
            {
                cell.Clear();
                pool.Push(cell);
            }
            cells.Clear();

            foreach (SpaceShip ship in ships)
            {
                if (ship == null) continue;
                ship.TakeSnapshot();
                if (!ship.Snapshot.Active) continue;
                Vector3Int key = Cell(ship.Snapshot.Position);
                if (!cells.TryGetValue(key, out List<SpaceShip> cell))
                {
                    cell = pool.Count > 0 ? pool.Pop() : new List<SpaceShip>();
                    cells.Add(key, cell);
                }
                cell.Add(ship);
            }
        }

        public void Query(Vector3 center, float radius, List<SpaceShip> results)
        {
            results.Clear();
            float radiusSq = radius * radius;
            Vector3Int min = Cell(center - Vector3.one * radius);
            Vector3Int max = Cell(center + Vector3.one * radius);
            Vector3Int size = max - min + Vector3Int.one;

            if ((long)size.x * size.y * size.z > cells.Count)
            {
                foreach (KeyValuePair<Vector3Int, List<SpaceShip>> pair in cells)
                {
                    Vector3Int key = pair.Key;
                    if (key.x < min.x || key.y < min.y || key.z < min.z || key.x > max.x || key.y > max.y || key.z > max.z) continue;
                    Collect(pair.Value, center, radiusSq, results);
                }
                return;
            }

            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            {
                if (cells.TryGetValue(new Vector3Int(x, y, z), out List<SpaceShip> cell))
                    Collect(cell, center, radiusSq, results);
            }
        }

        public SpaceShip FindNearestEnemy(SpaceShip self, float radius)
        {
            Vector3 position = self.Position;
            Query(position, radius, scratch);
            SpaceShip best = null;
            float bestSq = float.MaxValue;
            foreach (SpaceShip ship in scratch)
            {
                if (!self.IsEnemy(ship)) continue;
                float sq = (ship.Snapshot.Position - position).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = ship;
                }
            }
            return best;
        }

        private static void Collect(List<SpaceShip> cell, Vector3 center, float radiusSq, List<SpaceShip> results)
        {
            foreach (SpaceShip ship in cell)
            {
                if ((ship.Snapshot.Position - center).sqrMagnitude <= radiusSq)
                    results.Add(ship);
            }
        }

        private Vector3Int Cell(Vector3 position) => new Vector3Int(
            Mathf.FloorToInt(position.x / cellSize),
            Mathf.FloorToInt(position.y / cellSize),
            Mathf.FloorToInt(position.z / cellSize));
    }
}
