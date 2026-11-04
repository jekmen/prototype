using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI2.Demo
{
    /// <summary>
    /// Launches missiles from random directions at targets of different skill and counts hits per lane.
    /// Lanes with a torpedo take the torpedo profile instead of the selected missile.
    /// </summary>
    public class MissileDemo : MonoBehaviour
    {
        [Serializable]
        private class Lane
        {
            public string Label;
            public SpaceShip Target;
            public MissileProfile Override;
            [NonSerialized] public int Launches;
            [NonSerialized] public int Hits;
            [NonSerialized] public float Cooldown;
            [NonSerialized] public CombatBrain Brain;
            [NonSerialized] public AIPilot Pilot;
        }

        [SerializeField] private Lane[] lanes;
        [SerializeField] private MissileProfile[] missiles;
        [SerializeField, Min(100f)] private float launchDistance = 1200f;
        [SerializeField, Min(0f)] private float launchInterval = 2f;
        [SerializeField] private ChaseCamera chaseCamera;
        [SerializeField] private FlightHud hud;

        private readonly Dictionary<int, Lane> inFlight = new Dictionary<int, Lane>();
        private int missile;
        private int view;
        private float elapsed;
        private GUIStyle style;

        public void Setup(string[] labels, SpaceShip[] targets, MissileProfile[] overrides, MissileProfile[] missileSet, ChaseCamera cameraRig, FlightHud flightHud)
        {
            lanes = new Lane[targets.Length];
            for (int i = 0; i < lanes.Length; i++) lanes[i] = new Lane { Label = labels[i], Target = targets[i], Override = overrides[i] };
            missiles = missileSet;
            chaseCamera = cameraRig;
            hud = flightHud;
        }

        private void Start()
        {
            MissileSystem system = MissileSystem.Instance;
            system.Detonated += OnDetonated;
            system.Expired += track => inFlight.Remove(track.Id);
            foreach (Lane lane in lanes)
            {
                lane.Brain = lane.Target.GetComponent<CombatBrain>();
                lane.Pilot = lane.Target.GetComponent<AIPilot>();
                lane.Cooldown = UnityEngine.Random.Range(0.5f, 1.5f);
            }
            SetMissile(0);
            ShowView(0);
        }

        private void OnDestroy()
        {
            if (!MissileSystem.TryGet(out MissileSystem system)) return;
            system.Detonated -= OnDetonated;
        }

        private void OnDetonated(MissileTrack track, Vector3 point)
        {
            if (!inFlight.TryGetValue(track.Id, out Lane lane)) return;
            inFlight.Remove(track.Id);
            float distance = Vector3.Distance(point, lane.Target.Position) - lane.Target.Radius;
            if (distance <= track.Profile.BlastRadius) lane.Hits++;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab)) ShowView((view + 1) % lanes.Length);
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) ResetStats();
            for (int i = 0; i < missiles.Length && i < 9; i++)
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1 + i)) SetMissile(i);

            foreach (Lane lane in lanes)
            {
                // Lanes without a brain fly straight: the baseline every evader is measured against.
                if (lane.Brain == null && lane.Pilot != null) lane.Pilot.Direct(lane.Target.Forward, 1f);
                if (InFlight(lane)) continue;
                lane.Cooldown -= Time.deltaTime;
                if (lane.Cooldown <= 0f) LaunchAt(lane);
            }
        }

        private bool InFlight(Lane lane)
        {
            foreach (Lane flying in inFlight.Values)
                if (flying == lane) return true;
            return false;
        }

        private void LaunchAt(Lane lane)
        {
            MissileProfile profile = lane.Override != null ? lane.Override : missiles[missile];
            // Short-range missiles are launched where a pilot would launch them, well inside their lock range.
            float distance = Mathf.Min(launchDistance, profile.LockRange * 0.75f);
            Vector3 origin = lane.Target.Position + UnityEngine.Random.onUnitSphere * distance;
            Vector3 direction = (lane.Target.Position - origin).normalized;
            int fired = profile.Salvo;
            for (int i = 0; i < fired; i++)
            {
                Vector3 spread = Quaternion.AngleAxis(profile.SalvoSpread, UnityEngine.Random.onUnitSphere) * direction;
                int id = MissileSystem.Instance.Launch(profile, null, origin + UnityEngine.Random.insideUnitSphere * 5f, fired > 1 ? spread : direction, lane.Target);
                inFlight[id] = lane;
            }
            lane.Launches += fired;
            lane.Cooldown = launchInterval;
        }

        private void SetMissile(int index)
        {
            missile = index;
            MissileSystem.Instance.Clear();
            inFlight.Clear();
            ResetStats();
        }

        private void ShowView(int index)
        {
            view = index;
            chaseCamera.Target = lanes[view].Target.transform;
            hud.Bind(lanes[view].Target.Motor, null);
        }

        private void ResetStats()
        {
            elapsed = 0f;
            foreach (Lane lane in lanes)
            {
                lane.Launches = 0;
                lane.Hits = 0;
            }
        }

        private void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };
            MissileProfile current = missiles[missile];
            string text = $"Missile {current.name} ({current.Guidance}, salvo {current.Salvo}) · {elapsed:0} s\n";
            foreach (Lane lane in lanes)
            {
                float rate = lane.Launches > 0 ? 100f * lane.Hits / lane.Launches : 0f;
                string missileName = lane.Override != null ? lane.Override.name : current.name;
                string behavior = lane.Brain != null && lane.Brain.Current != null ? lane.Brain.Current.Name : "straight";
                text += $"{lane.Label}: {missileName}, launched {lane.Launches}, hits {lane.Hits}, hit rate {rate:0}% · {behavior}\n";
            }
            text += $"View: {lanes[view].Label}\n";
            text += "1-" + missiles.Length + " missile · Tab switch view · R reset stats";
            GUI.Label(new Rect(16, Screen.height - 40 - 22 * (lanes.Length + 3), 1100, 22 * (lanes.Length + 3) + 20), text, style);
        }
    }
}
