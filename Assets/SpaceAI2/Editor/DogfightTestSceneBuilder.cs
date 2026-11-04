using System;
using System.Collections.Generic;
using SpaceAI2.Demo;
using UnityEditor;
using UnityEngine;
using static SpaceAI2.Editor.SceneBuilderUtils;

namespace SpaceAI2.Editor
{
    public static class DogfightTestSceneBuilder
    {
        private const float TeamDistance = 2000f;
        private const float WingSpacing = 60f;
        private const float ArenaRadius = 1500f;
        private const int AsteroidCount = 300;
        private const float SpawnClearance = 300f;

        private static readonly Color[] TeamColors = { new Color(0.4f, 0.8f, 1f), new Color(1f, 0.35f, 0.3f) };
        private static readonly string[] TeamNames = { "Blue", "Red" };

        [MenuItem("Tools/SpaceAI2/Build Dogfight 1v1 Scene")]
        public static void Build1v1() => Build("Dogfight1v1", new[] { "Ace" }, new[] { "Veteran" });

        [MenuItem("Tools/SpaceAI2/Build Dogfight 10v10 Scene")]
        public static void Build10v10()
        {
            string[] wing = { "Ace", "Ace", "Veteran", "Veteran", "Veteran", "Veteran", "Rookie", "Rookie", "Rookie", "Rookie" };
            Build("Dogfight10v10", wing, wing);
        }

        [MenuItem("Tools/SpaceAI2/Build Dogfight 10v10 Asteroids Scene")]
        public static void Build10v10Asteroids()
        {
            string[] wing = { "Ace", "Ace", "Veteran", "Veteran", "Veteran", "Veteran", "Rookie", "Rookie", "Rookie", "Rookie" };
            Build("Dogfight10v10Asteroids", wing, wing, AsteroidCount);
        }

        private static void Build(string sceneName, string[] blue, string[] red, int asteroids = 0)
        {
            if (!NewScene()) return;
            ShipProfile shipProfile = LoadOrCreateProfile();
            WeaponProfile cannon = GunneryTestSceneBuilder.LoadOrCreateWeapon("Cannon", _ => { });
            var profiles = new Dictionary<string, PilotProfile>
            {
                ["Ace"] = LoadOrCreatePilot("Ace", p => Configure(p, 0.15f, 0.8f, 0.15f, 0.8f, 0.25f, 10f, 2f, 1f)),
                ["Veteran"] = LoadOrCreatePilot("Veteran", p => Configure(p, 0.4f, 1f, 0.3f, 0.5f, 0.35f, 12f, 3.5f, 0.85f)),
                ["Rookie"] = LoadOrCreatePilot("Rookie", p => Configure(p, 1.5f, 1.6f, 0.6f, 0.3f, 0.5f, 15f, 6f, 0.7f))
            };

            var arena = new GameObject("Arena").transform;
            var spawns = new Transform[2];
            var brains = new List<CombatBrain>();
            string[][] teams = { blue, red };
            for (int team = 0; team < 2; team++)
            {
                float side = team == 0 ? -1f : 1f;
                spawns[team] = new GameObject($"{TeamNames[team]} Spawn").transform;
                spawns[team].SetPositionAndRotation(Vector3.forward * (side * TeamDistance * 0.5f), Quaternion.LookRotation(Vector3.forward * -side));

                string[] wing = teams[team];
                for (int i = 0; i < wing.Length; i++)
                {
                    Vector3 slot = new Vector3((i % 5 - 2) * WingSpacing, (i / 5) * WingSpacing, 0f);
                    Vector3 position = spawns[team].TransformPoint(slot);
                    GameObject ship = CreateShip($"{TeamNames[team]} {wing[i]} {i + 1}", shipProfile, position, spawns[team].rotation);
                    ship.AddComponent<SpaceShip>().Team = team;
                    ship.AddComponent<ShipHealth>();
                    ShipGun gun = ship.AddComponent<ShipGun>();
                    gun.Weapon = cannon;
                    gun.GimbalAngle = 6f;
                    ship.AddComponent<AIPilot>().Profile = profiles[wing[i]];
                    CombatBrain brain = ship.AddComponent<CombatBrain>();
                    brain.PatrolArea = arena;
                    brains.Add(brain);
                    AddTrail(ship, TeamColors[team]);
                }
            }

            var random = new System.Random(5);
            if (asteroids > 0) CreateAsteroids(new GameObject("Asteroids").transform, random, asteroids, spawns);
            ScatterRocks(new GameObject("Backdrop").transform, random, 150, ArenaRadius + 2000f, ArenaRadius + 4000f, 50f, 200f);

            SetupCamera(brains[0].transform, out ChaseCamera chase, out FlightHud hud);
            new GameObject(nameof(SpaceAIWorld)).AddComponent<SpaceAIWorld>();
            new GameObject(nameof(ProjectileSystem)).AddComponent<ProjectileSystem>().TracerMaterial =
                GunneryTestSceneBuilder.LoadOrCreateTracerMaterial();
            new GameObject("DogfightDemo").AddComponent<DogfightDemo>().Setup(brains.ToArray(), spawns, chase, hud);

            SaveScene(sceneName);
            Selection.activeObject = brains[0].gameObject;
        }

        private static void CreateAsteroids(Transform parent, System.Random random, int count, Transform[] spawns)
        {
            for (int placed = 0, attempt = 0; placed < count && attempt < count * 20; attempt++)
            {
                Vector3 position = RandomVector(random) * ArenaRadius;
                if (position.magnitude > ArenaRadius) continue;
                float scale = Mathf.Lerp(30f, 160f, Mathf.Pow((float)random.NextDouble(), 2f));
                bool blocked = false;
                foreach (Transform spawn in spawns)
                    if (Vector3.Distance(position, spawn.position) < SpawnClearance + scale) blocked = true;
                if (blocked) continue;

                var rock = GameObject.CreatePrimitive(placed % 3 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere);
                rock.name = "Asteroid";
                rock.transform.SetParent(parent);
                rock.transform.SetPositionAndRotation(position, Quaternion.Euler(RandomVector(random) * 180f));
                rock.transform.localScale = Vector3.one * scale;
                placed++;
            }
        }

        private static void Configure(PilotProfile p, float aimError, float fireTolerance, float reaction, float aggression, float caution, float patience, float spray, float turnSkill)
        {
            p.AimError = aimError;
            p.FireTolerance = fireTolerance;
            p.ReactionTime = reaction;
            p.Aggression = aggression;
            p.Caution = caution;
            p.Patience = patience;
            p.SprayAngle = spray;
            p.TurnSkill = turnSkill;
        }

        private static PilotProfile LoadOrCreatePilot(string name, Action<PilotProfile> configure)
        {
            string path = $"{Root}/Profiles/Pilot{name}.asset";
            var pilot = AssetDatabase.LoadAssetAtPath<PilotProfile>(path);
            if (pilot != null)
            {
                configure(pilot);
                EditorUtility.SetDirty(pilot);
                AssetDatabase.SaveAssets();
                return pilot;
            }

            EnsureFolder(Root + "/Profiles");
            pilot = ScriptableObject.CreateInstance<PilotProfile>();
            configure(pilot);
            AssetDatabase.CreateAsset(pilot, path);
            AssetDatabase.SaveAssets();
            return pilot;
        }
    }
}
