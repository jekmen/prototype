using System.Collections.Generic;
using SpaceAI2.Demo;
using UnityEditor;
using UnityEngine;
using static SpaceAI2.Editor.SceneBuilderUtils;

namespace SpaceAI2.Editor
{
    public static class ObstacleTestSceneBuilder
    {
        private const float FieldRadius = 1500f;
        private const int ShipCount = 50;
        private const int WaypointCount = 24;
        private const int RockCount = 1000;
        private const float KeepClear = 40f;

        [MenuItem("Tools/SpaceAI2/Build Obstacle Test Scene")]
        public static void Build()
        {
            if (!NewScene()) return;
            ShipProfile profile = LoadOrCreateProfile();
            var random = new System.Random(3);

            var waypoints = new Transform[WaypointCount];
            var waypointRoot = new GameObject("Waypoints").transform;
            var clearPoints = new List<Vector3>();
            for (int i = 0; i < WaypointCount; i++)
            {
                var point = new GameObject($"Waypoint {i}").transform;
                point.SetParent(waypointRoot);
                point.position = RandomVector(random) * FieldRadius * 0.8f;
                waypoints[i] = point;
                clearPoints.Add(point.position);
            }

            var pilots = new AIPilot[ShipCount];
            var shipRoot = new GameObject("Ships").transform;
            for (int i = 0; i < ShipCount; i++)
            {
                Vector3 position = RandomVector(random) * FieldRadius * 0.8f;
                Quaternion rotation = Quaternion.LookRotation(RandomVector(random).normalized + Vector3.forward * 0.01f);
                GameObject ship = CreateShip($"Ship {i:00}", profile, position, rotation);
                ship.transform.SetParent(shipRoot);
                ship.AddComponent<SpaceShip>();
                pilots[i] = ship.AddComponent<AIPilot>();
                ship.AddComponent<WaypointRoute>().Points = waypoints;
                AddTrail(ship, Color.HSVToRGB(i / (float)ShipCount, 0.6f, 1f));
                clearPoints.Add(position);
            }

            CreateRocks(random, clearPoints);

            SetupCamera(pilots[0].transform, out ChaseCamera chase, out FlightHud hud);
            new GameObject("ObstacleDemo").AddComponent<ObstacleDemo>().Setup(pilots, chase, hud);
            new GameObject(nameof(SpaceAIWorld)).AddComponent<SpaceAIWorld>();

            SaveScene("ObstacleTest");
            Selection.activeObject = pilots[0].gameObject;
        }

        private static void CreateRocks(System.Random random, List<Vector3> clearPoints)
        {
            var root = new GameObject("Asteroids").transform;
            int placed = 0;
            for (int attempt = 0; placed < RockCount && attempt < RockCount * 10; attempt++)
            {
                Vector3 position = RandomVector(random) * FieldRadius;
                if (position.magnitude > FieldRadius) continue;
                float scale = Mathf.Lerp(10f, 140f, Mathf.Pow((float)random.NextDouble(), 2.5f));
                if (clearPoints.Exists(p => (p - position).magnitude < scale + KeepClear)) continue;

                var rock = GameObject.CreatePrimitive(placed % 4 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere);
                rock.name = "Asteroid";
                rock.transform.SetParent(root);
                rock.transform.SetPositionAndRotation(position, Quaternion.Euler(RandomVector(random) * 180f));
                rock.transform.localScale = new Vector3(scale, scale * Mathf.Lerp(0.6f, 1f, (float)random.NextDouble()), scale);
                placed++;
            }
        }
    }
}
