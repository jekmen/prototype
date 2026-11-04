using System;
using UnityEngine;

namespace SpaceAI2.Demo
{
    public class ObstacleDemo : MonoBehaviour
    {
        [SerializeField] private AIPilot[] pilots;
        [SerializeField] private ChaseCamera chaseCamera;
        [SerializeField] private FlightHud hud;

        private readonly int[] maneuvers = new int[Enum.GetValues(typeof(ManeuverKind)).Length];
        private readonly int[] sides = new int[4];
        private int rockHits;
        private int shipHits;
        private float elapsed;
        private int view;
        private GUIStyle style;

        public void Setup(AIPilot[] shipPilots, ChaseCamera cameraRig, FlightHud flightHud)
        {
            pilots = shipPilots;
            chaseCamera = cameraRig;
            hud = flightHud;
        }

        private void Start()
        {
            foreach (AIPilot pilot in pilots)
            {
                pilot.Ship.Collided += OnCollided;
                pilot.ManeuverStarted += OnManeuverStarted;
            }
            ShowView(0);
        }

        private void OnDestroy()
        {
            foreach (AIPilot pilot in pilots)
            {
                if (pilot == null) continue;
                pilot.Ship.Collided -= OnCollided;
                pilot.ManeuverStarted -= OnManeuverStarted;
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab)) ShowView((view + 1) % pilots.Length);
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) ResetStats();
        }

        private void OnCollided(SpaceShip ship, Collision collision)
        {
            var other = collision.rigidbody != null ? collision.rigidbody.GetComponent<SpaceShip>() : null;
            if (other == null) rockHits++;
            else if (ship.GetInstanceID() < other.GetInstanceID()) shipHits++;
        }

        private void OnManeuverStarted(AIPilot pilot, ManeuverKind kind, ProbeSide side)
        {
            maneuvers[(int)kind]++;
            sides[(int)side]++;
        }

        private void ShowView(int index)
        {
            view = index;
            chaseCamera.Target = pilots[view].transform;
            hud.Bind(pilots[view].Ship.Motor, null);
        }

        private void ResetStats()
        {
            elapsed = 0f;
            rockHits = 0;
            shipHits = 0;
            Array.Clear(maneuvers, 0, maneuvers.Length);
            Array.Clear(sides, 0, sides.Length);
        }

        private void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };
            float minutes = Mathf.Max(elapsed / 60f, 0.001f);
            AIPilot current = pilots[view];
            SpaceAIWorld.TryGet(out SpaceAIWorld world);

            string text =
                $"{pilots.Length} ships · {elapsed:0} s · AI update {(world != null ? world.LastUpdateMs : 0):0.00} ms\n" +
                $"Rock hits {rockHits} ({rockHits / minutes:0.0}/min) · ship-ship hits {shipHits}\n" +
                $"Maneuvers: turn {maneuvers[(int)ManeuverKind.TurnAway]}, boost {maneuvers[(int)ManeuverKind.BoostTurn]}, " +
                $"roll {maneuvers[(int)ManeuverKind.RollAround]}, brake {maneuvers[(int)ManeuverKind.EmergencyBrake]}, " +
                $"reverse {maneuvers[(int)ManeuverKind.Reverse]}\n" +
                $"Sides: up {sides[0]}, down {sides[1]}, left {sides[2]}, right {sides[3]}\n" +
                $"View {current.name}: {(current.Maneuver == ManeuverKind.None ? "cruising" : $"{current.Maneuver} {current.ManeuverSide}")}\n" +
                "Tab switch view · R reset stats";

            GUI.Label(new Rect(16, Screen.height - 160, 900, 150), text, style);
        }
    }
}
