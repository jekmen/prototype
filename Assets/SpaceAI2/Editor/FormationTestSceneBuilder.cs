using System.Collections.Generic;
using SpaceAI2.Demo;
using UnityEditor;
using UnityEngine;
using static SpaceAI2.Editor.SceneBuilderUtils;

namespace SpaceAI2.Editor
{
    public static class FormationTestSceneBuilder
    {
        private const float FieldRadius = 1500f;
        private const int RockCount = 600;
        private const float KeepClear = 120f;
        private const float SquadSpacing = 400f;
        private const float SquadHeightStep = 250f;

        // Legs 0→1 and 2→3 cross the asteroid field, the other two run outside it, so squads break up and rejoin every lap.
        private static readonly Vector3[] Route =
        {
            new Vector3(-2600f, 0f, -600f), new Vector3(2600f, 150f, 600f), new Vector3(600f, -150f, 2600f), new Vector3(-600f, 100f, -2600f)
        };

        [MenuItem("Tools/SpaceAI2/Build Formation Test Scene")]
        public static void Build()
        {
            if (!NewScene()) return;
            ShipProfile shipProfile = LoadOrCreateProfile();
            WeaponProfile cannon = GunneryTestSceneBuilder.LoadOrCreateWeapon("Cannon", _ => { });
            PilotProfile veteran = DogfightTestSceneBuilder.LoadPilots()["Veteran"];
            (FormationShape shape, int wingmen, Color color)[] squads =
            {
                (FormationShape.Wedge, 4, new Color(0.4f, 0.8f, 1f)),
                (FormationShape.Echelon, 3, new Color(0.5f, 1f, 0.5f)),
                (FormationShape.Diamond, 3, new Color(1f, 0.85f, 0.3f))
            };

            var clearPoints = new List<Vector3>();
            var formations = new List<Formation>();
            for (int s = 0; s < squads.Length; s++)
            {
                var root = new GameObject($"Squad {squads[s].shape}").transform;
                Vector3 height = Vector3.up * ((s - 1) * SquadHeightStep);
                var points = new Transform[Route.Length];
                for (int i = 0; i < Route.Length; i++)
                {
                    points[i] = new GameObject($"Waypoint {i}").transform;
                    points[i].SetParent(root);
                    points[i].position = Route[i] + height;
                    clearPoints.Add(points[i].position);
                }

                Vector3 start = Route[Route.Length - 1] + height;
                Quaternion rotation = Quaternion.LookRotation(Route[0] - Route[Route.Length - 1]);
                start += rotation * Vector3.right * ((s - 1) * SquadSpacing);
                clearPoints.Add(start);

                GameObject leader = CreateFighter($"{squads[s].shape} Lead", shipProfile, cannon, start, rotation, squads[s].color);
                leader.transform.SetParent(root);
                leader.AddComponent<AIPilot>();
                var route = leader.AddComponent<WaypointRoute>();
                route.Points = points;
                route.RandomOrder = false;

                FormationProfile profile = LoadOrCreateFormation(squads[s].shape);
                var wingmen = new List<CombatBrain>();
                for (int w = 0; w < squads[s].wingmen; w++)
                {
                    Vector3 position = start + rotation * profile.Slot(w);
                    GameObject ship = CreateFighter($"{squads[s].shape} {w + 2}", shipProfile, cannon, position, rotation, squads[s].color * 0.8f);
                    ship.transform.SetParent(root);
                    ship.AddComponent<AIPilot>().Profile = veteran;
                    wingmen.Add(ship.AddComponent<CombatBrain>());
                }

                Formation formation = root.gameObject.AddComponent<Formation>();
                formation.Setup(profile, leader.GetComponent<SpaceShip>(), wingmen);
                formations.Add(formation);
            }

            var enemyRoot = new GameObject("Enemies").transform;
            var enemies = new GameObject[4];
            for (int i = 0; i < enemies.Length; i++)
            {
                Vector3 position = new Vector3((i - 1.5f) * 80f, FieldRadius + 300f, 0f);
                GameObject enemy = CreateFighter($"Enemy {i + 1}", shipProfile, cannon, position, Quaternion.LookRotation(Vector3.right), new Color(1f, 0.35f, 0.3f));
                enemy.transform.SetParent(enemyRoot);
                enemy.GetComponent<SpaceShip>().Team = 1;
                enemy.AddComponent<AIPilot>().Profile = veteran;
                enemy.AddComponent<CombatBrain>();
                enemy.SetActive(false);
                enemies[i] = enemy;
                clearPoints.Add(position);
            }

            CreateRocks(new System.Random(7), clearPoints);

            SetupCamera(formations[0].Leader.transform, out ChaseCamera chase, out FlightHud hud);
            new GameObject(nameof(SpaceAIWorld)).AddComponent<SpaceAIWorld>();
            new GameObject(nameof(ProjectileSystem)).AddComponent<ProjectileSystem>().TracerMaterial =
                GunneryTestSceneBuilder.LoadOrCreateTracerMaterial();
            new GameObject("FormationDemo").AddComponent<FormationDemo>().Setup(formations.ToArray(), enemies, chase, hud);

            SaveScene("FormationTest");
            Selection.activeObject = formations[0].gameObject;
        }

        private static GameObject CreateFighter(string name, ShipProfile profile, WeaponProfile weapon, Vector3 position, Quaternion rotation, Color color)
        {
            GameObject ship = CreateShip(name, profile, position, rotation);
            ship.AddComponent<SpaceShip>().Team = 0;
            ship.AddComponent<ShipHealth>();
            ShipGun gun = ship.AddComponent<ShipGun>();
            gun.Weapon = weapon;
            gun.GimbalAngle = 6f;
            AddTrail(ship, color);
            return ship;
        }

        private static FormationProfile LoadOrCreateFormation(FormationShape shape)
        {
            string path = $"{Root}/Profiles/Formation{shape}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<FormationProfile>(path);
            if (profile == null)
            {
                EnsureFolder(Root + "/Profiles");
                profile = ScriptableObject.CreateInstance<FormationProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            profile.Shape = shape;
            profile.Spacing = 40f;
            profile.RejoinTime = 10f;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static void CreateRocks(System.Random random, List<Vector3> clearPoints)
        {
            var root = new GameObject("Asteroids").transform;
            int placed = 0;
            for (int attempt = 0; placed < RockCount && attempt < RockCount * 10; attempt++)
            {
                Vector3 position = RandomVector(random) * FieldRadius;
                if (position.magnitude > FieldRadius) continue;
                float scale = Mathf.Lerp(15f, 150f, Mathf.Pow((float)random.NextDouble(), 2.5f));
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
