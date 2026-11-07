using UnityEngine;

namespace SpaceAI2.Demo
{
    public class PursuitDemo : MonoBehaviour
    {
        [SerializeField] private SpaceShip drone;
        [SerializeField] private SpaceShip player;
        [SerializeField] private AIPilot[] pursuers;
        [SerializeField] private ChaseCamera chaseCamera;
        [SerializeField] private FlightHud hud;

        [Header("On-tail metric")]
        [SerializeField, Range(1f, 45f)] private float coneAngle = 10f;
        [SerializeField, Min(1f)] private float maxRange = 500f;

        private float[] onTail;
        private float[] rangeSum;
        private float elapsed;
        private SpaceShip[] views;
        private int view;
        private bool chasePlayer;
        private GUIStyle style;

        public void Setup(SpaceShip droneShip, SpaceShip playerShip, AIPilot[] pursuerPilots, ChaseCamera cameraRig, FlightHud flightHud)
        {
            drone = droneShip;
            player = playerShip;
            pursuers = pursuerPilots;
            chaseCamera = cameraRig;
            hud = flightHud;
        }

        private void Start()
        {
            onTail = new float[pursuers.Length];
            rangeSum = new float[pursuers.Length];
            views = new SpaceShip[pursuers.Length + 2];
            views[0] = pursuers[0].Ship;
            views[1] = drone;
            views[2] = player;
            for (int i = 1; i < pursuers.Length; i++) views[i + 2] = pursuers[i].Ship;

            AssignTargets();
            ShowView(0);
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab)) ShowView((view + 1) % views.Length);
            if (UnityEngine.Input.GetKeyDown(KeyCode.T))
            {
                chasePlayer = !chasePlayer;
                AssignTargets();
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) ResetStats();

            Measure(Time.deltaTime);
        }

        private void AssignTargets()
        {
            SpaceShip target = chasePlayer ? player : drone;
            PursuitMode[] modes = { PursuitMode.Lead, PursuitMode.Pure, PursuitMode.Lag };
            for (int i = 0; i < pursuers.Length; i++) pursuers[i].Pursue(target, modes[i % modes.Length]);
            ResetStats();
        }

        private void ShowView(int index)
        {
            view = index;
            SpaceShip ship = views[view];
            chaseCamera.Target = ship.transform;

            PlayerPilot pilot = player.GetComponent<PlayerPilot>();
            pilot.enabled = ship == player;
            hud.Bind(ship.Motor, pilot.enabled ? pilot : null);
        }

        private void Measure(float dt)
        {
            elapsed += dt;
            for (int i = 0; i < pursuers.Length; i++)
            {
                AIPilot pursuer = pursuers[i];
                if (pursuer.Target == null) continue;
                Vector3 toTarget = pursuer.Target.Position - pursuer.Ship.Position;
                float range = toTarget.magnitude;
                rangeSum[i] += range * dt;
                if (range <= maxRange && Vector3.Angle(pursuer.Ship.Forward, toTarget) <= coneAngle) onTail[i] += dt;
            }
        }

        private void ResetStats()
        {
            elapsed = 0f;
            if (onTail == null) return;
            System.Array.Clear(onTail, 0, onTail.Length);
            System.Array.Clear(rangeSum, 0, rangeSum.Length);
        }

        private void OnGUI()
        {
            if (onTail == null) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };

            string text = $"Target: {(chasePlayer ? "player" : "drone")} · view: {views[view].name} · {elapsed:0} s\n" +
                          $"On tail = within {coneAngle:0}° of the nose and {maxRange:0} m\n";
            for (int i = 0; i < pursuers.Length; i++)
            {
                float share = elapsed > 0f ? onTail[i] / elapsed : 0f;
                float range = elapsed > 0f ? rangeSum[i] / elapsed : 0f;
                text += $"{pursuers[i].name} ({pursuers[i].PursuitMode}): on tail {share:P0}, avg range {range:0} m\n";
            }
            text += "Tab switch view · T chase drone/player · R reset stats";

            GUI.Label(new Rect(16, Screen.height - 140, 800, 130), text, style);
        }
    }
}
