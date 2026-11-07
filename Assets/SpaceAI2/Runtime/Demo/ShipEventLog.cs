using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SpaceAI2.Demo
{
    /// <summary>
    /// Receiver for the ships' UnityEvents in the quickstart scene: prints a feed of what happened and brings
    /// destroyed ships back at their spawn point after a delay. Tab moves the chase camera to the next ship.
    /// </summary>
    public class ShipEventLog : MonoBehaviour
    {
        [SerializeField, Min(1)] private int lines = 8;
        [SerializeField, Min(0f)] private float respawnDelay = 4f;
        [SerializeField] private Rect area = new Rect(16, 16, 560, 220);
        [SerializeField] private ChaseCamera chase;

        private readonly Queue<string> feed = new Queue<string>();
        private readonly Dictionary<SpaceShip, Pose> spawns = new Dictionary<SpaceShip, Pose>();
        private readonly List<(SpaceShip ship, float time)> pending = new List<(SpaceShip, float)>();
        private readonly StringBuilder text = new StringBuilder();
        private GUIStyle style;

        public ChaseCamera Chase
        {
            get => chase;
            set => chase = value;
        }

        public int Destroyed { get; private set; }
        public int TargetsAcquired { get; private set; }
        public int MissilesLaunched { get; private set; }

        public void ShipDestroyed(SpaceShip ship)
        {
            Destroyed++;
            Add($"{ship.name} destroyed");
            if (respawnDelay > 0f) pending.Add((ship, Time.time + respawnDelay));
        }

        public void TargetAcquired(SpaceShip ship, SpaceShip target)
        {
            TargetsAcquired++;
            Add($"{ship.name} engages {target.name}");
        }

        public void MissileLaunched(SpaceShip ship, SpaceShip target)
        {
            MissilesLaunched++;
            Add(target != null ? $"{ship.name} fires a missile at {target.name}" : $"{ship.name} fires a missile");
        }

        private void Add(string line)
        {
            feed.Enqueue($"{Time.time:0.0}  {line}");
            while (feed.Count > lines) feed.Dequeue();
        }

        private void Start()
        {
            foreach (SpaceShip ship in FindObjectsOfType<SpaceShip>()) spawns[ship] = new Pose(ship.transform.position, ship.transform.rotation);
            SpaceAI.OriginShifted += OnOriginShifted;
        }

        private void OnDestroy() => SpaceAI.OriginShifted -= OnOriginShifted;

        private void OnOriginShifted(Vector3 offset)
        {
            var keys = new List<SpaceShip>(spawns.Keys);
            foreach (SpaceShip ship in keys) spawns[ship] = new Pose(spawns[ship].position - offset, spawns[ship].rotation);
        }

        private void Update()
        {
            if (chase != null && (Input.GetKeyDown(KeyCode.Tab) || chase.Target == null || !chase.Target.gameObject.activeInHierarchy)) FollowNext();
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                (SpaceShip ship, float time) = pending[i];
                if (Time.time < time) continue;
                pending.RemoveAt(i);
                if (ship == null) continue;
                if (spawns.TryGetValue(ship, out Pose pose)) ship.transform.SetPositionAndRotation(pose.position, pose.rotation);
                if (ship.Health != null) ship.Health.Restore();
                ship.RestoreSubsystems();
                ship.gameObject.SetActive(true);
            }
        }

        private void FollowNext()
        {
            var ships = new List<SpaceShip>(spawns.Keys);
            if (ships.Count == 0) return;
            int start = chase.Target != null ? ships.FindIndex(s => s.transform == chase.Target) : -1;
            for (int step = 1; step <= ships.Count; step++)
            {
                SpaceShip next = ships[(start + step + ships.Count) % ships.Count];
                if (next == null || !next.gameObject.activeInHierarchy) continue;
                chase.Target = next.transform;
                return;
            }
        }

        private void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 14 };
            text.Clear();
            text.Append($"Destroyed {Destroyed} · targets acquired {TargetsAcquired} · missiles {MissilesLaunched}   (Tab: next ship, F3: AI overlay)\n");
            foreach (string line in feed) text.Append(line).Append('\n');
            GUI.Label(area, text.ToString(), style);
        }
    }
}
