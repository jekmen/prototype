using System;
using SpaceAI2.Demo;
using UnityEditor;
using UnityEngine;
using static SpaceAI2.Editor.SceneBuilderUtils;

namespace SpaceAI2.Editor
{
    public static class GunneryTestSceneBuilder
    {
        private const float DuelSpacing = 1500f;
        private const float ArenaRadius = 450f;
        private const int WaypointCount = 10;

        [MenuItem("Tools/SpaceAI2/Build Gunnery Test Scene")]
        public static void Build()
        {
            if (!NewScene()) return;
            ShipProfile profile = LoadOrCreateProfile();
            WeaponProfile[] weapons =
            {
                LoadOrCreateWeapon("Cannon", _ => { }),
                LoadOrCreateWeapon("Flak", w =>
                {
                    w.Kind = WeaponKind.Flak;
                    w.Damage = 15f;
                    w.BlastRadius = 20f;
                    w.FireRate = 3f;
                    w.MuzzleSpeed = 450f;
                    w.Range = 1000f;
                    w.Spread = 0.3f;
                    w.TracerColor = new Color(1f, 0.45f, 0.2f, 1f);
                    w.TracerLength = 4f;
                    w.TracerWidth = 0.8f;
                }),
                LoadOrCreateWeapon("Laser", w =>
                {
                    w.Kind = WeaponKind.Laser;
                    w.Damage = 4f;
                    w.FireRate = 6f;
                    w.Range = 700f;
                    w.Spread = 0.05f;
                    w.TracerColor = new Color(0.3f, 0.8f, 1f, 1f);
                    w.TracerWidth = 0.4f;
                })
            };

            var random = new System.Random(4);
            string[] labels = { "Ace", "Rookie" };
            float[] aimErrors = { 0.15f, 1.5f };
            float[] gimbals = { 3f, 0f };
            var shooters = new AIPilot[labels.Length];
            var targets = new SpaceShip[labels.Length];

            for (int i = 0; i < labels.Length; i++)
            {
                Vector3 center = Vector3.right * (i == 0 ? -DuelSpacing : DuelSpacing);
                Transform[] route = CreateRoute(labels[i], center, random);

                GameObject target = CreateShip($"{labels[i]} Target", profile, center, Quaternion.identity);
                targets[i] = target.AddComponent<SpaceShip>();
                targets[i].Team = 1;
                target.AddComponent<AIPilot>();
                target.AddComponent<WaypointRoute>().Points = route;
                AddTrail(target, new Color(1f, 0.35f, 0.3f));

                GameObject shooter = CreateShip($"{labels[i]} Shooter", profile, center - Vector3.forward * 250f, Quaternion.identity);
                shooter.AddComponent<SpaceShip>();
                ShipGun gun = shooter.AddComponent<ShipGun>();
                gun.Weapon = weapons[0];
                gun.GimbalAngle = gimbals[i];
                shooters[i] = shooter.AddComponent<AIPilot>();
                shooters[i].AimError = aimErrors[i];
                AddTrail(shooter, new Color(0.4f, 0.8f, 1f));
            }

            ScatterRocks(new GameObject("Backdrop").transform, random, 150, 3000f, 5000f, 50f, 200f);

            SetupCamera(shooters[0].transform, out ChaseCamera chase, out FlightHud hud);
            new GameObject(nameof(SpaceAIWorld)).AddComponent<SpaceAIWorld>();
            new GameObject(nameof(ProjectileSystem)).AddComponent<ProjectileSystem>().TracerMaterial =
                LoadOrCreateTracerMaterial();
            new GameObject("GunneryDemo").AddComponent<GunneryDemo>().Setup(labels, shooters, targets, weapons, chase, hud);

            SaveScene("GunneryTest");
            Selection.activeObject = shooters[0].gameObject;
        }

        private static Transform[] CreateRoute(string label, Vector3 center, System.Random random)
        {
            var root = new GameObject($"{label} Route").transform;
            var points = new Transform[WaypointCount];
            for (int i = 0; i < WaypointCount; i++)
            {
                points[i] = new GameObject($"Waypoint {i}").transform;
                points[i].SetParent(root);
                points[i].position = center + RandomVector(random) * ArenaRadius;
            }
            return points;
        }

        internal static Material LoadOrCreateTracerMaterial()
        {
            string path = $"{Root}/Profiles/Tracer.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            EnsureFolder(Root + "/Profiles");
            material = new Material(Shader.Find("SpaceAI2/Tracer")) { enableInstancing = true };
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            return material;
        }

        internal static WeaponProfile LoadOrCreateWeapon(string name, Action<WeaponProfile> configure)
        {
            string path = $"{Root}/Profiles/{name}.asset";
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponProfile>(path);
            if (weapon != null) return weapon;

            EnsureFolder(Root + "/Profiles");
            weapon = ScriptableObject.CreateInstance<WeaponProfile>();
            configure(weapon);
            AssetDatabase.CreateAsset(weapon, path);
            AssetDatabase.SaveAssets();
            return weapon;
        }
    }
}
