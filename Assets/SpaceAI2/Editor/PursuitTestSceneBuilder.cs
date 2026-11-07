using SpaceAI2.Demo;
using UnityEditor;
using UnityEngine;
using static SpaceAI2.Editor.SceneBuilderUtils;

namespace SpaceAI2.Editor
{
    public static class PursuitTestSceneBuilder
    {
        private const float ArenaRadius = 1200f;
        private static readonly Color DroneColor = new Color(1f, 0.35f, 0.3f);
        private static readonly Color AllyColor = new Color(0.35f, 0.75f, 1f);

        [MenuItem("Tools/SpaceAI2/Build Pursuit Test Scene")]
        public static void Build()
        {
            if (!NewScene()) return;
            ShipProfile profile = LoadOrCreateProfile();
            var random = new System.Random(2);

            Transform[] waypoints = CreateWaypoints(random, 10);

            GameObject drone = CreateShip("Drone", profile, waypoints[0].position, Quaternion.identity);
            drone.AddComponent<SpaceShip>().Team = 1;
            drone.AddComponent<AIPilot>();
            drone.AddComponent<WaypointRoute>().Points = waypoints;
            AddTrail(drone, DroneColor);

            GameObject player = CreateShip("Player", profile, new Vector3(0f, 200f, -600f), Quaternion.identity);
            player.AddComponent<SpaceShip>().Team = 0;
            player.AddComponent<PlayerPilot>();
            AddTrail(player, Color.white);

            string[] names = { "Lead", "Pure", "Lag" };
            var pursuers = new AIPilot[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                Vector3 position = drone.transform.position + new Vector3((i - 1) * 60f, 0f, -400f);
                GameObject ship = CreateShip(names[i], profile, position, Quaternion.identity);
                ship.AddComponent<SpaceShip>().Team = 0;
                pursuers[i] = ship.AddComponent<AIPilot>();
                AddTrail(ship, AllyColor);
            }

            SetupCamera(pursuers[0].transform, out ChaseCamera chase, out FlightHud hud);
            new GameObject("PursuitDemo").AddComponent<PursuitDemo>()
                .Setup(drone.GetComponent<SpaceShip>(), player.GetComponent<SpaceShip>(), pursuers, chase, hud);
            new GameObject(nameof(SpaceAIWorld)).AddComponent<SpaceAIWorld>();

            var backdrop = new GameObject("Backdrop").transform;
            ScatterRocks(backdrop, random, 150, 3000f, 5000f, 80f, 400f);

            SaveScene("PursuitTest");
            Selection.activeObject = pursuers[0].gameObject;
        }

        private static Transform[] CreateWaypoints(System.Random random, int count)
        {
            var root = new GameObject("Waypoints").transform;
            var points = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.name = $"Waypoint {i}";
                marker.transform.SetParent(root);
                marker.transform.position = RandomVector(random) * ArenaRadius;
                marker.transform.localScale = Vector3.one * 6f;
                points[i] = marker.transform;
            }
            return points;
        }
    }
}
