using System;
using System.Collections.Generic;
using SpaceAI2.Demo;
using UnityEditor;
using UnityEngine;
using static SpaceAI2.Editor.SceneBuilderUtils;
using Object = UnityEngine.Object;

namespace SpaceAI2.Editor
{
    public static class TurretTestSceneBuilder
    {
        private const int FighterCount = 4;

        [MenuItem("Tools/SpaceAI2/Build Turret Test Scene")]
        public static void Build()
        {
            if (!NewScene()) return;
            ShipProfile fighter = LoadOrCreateProfile();
            ShipProfile cruiserProfile = MissileTestSceneBuilder.LoadOrCreateCruiser();
            WeaponProfile cannon = GunneryTestSceneBuilder.LoadOrCreateWeapon("Cannon", _ => { });
            WeaponProfile[] guns = LoadTurretWeapons();
            MissileProfile[] missiles = MissileTestSceneBuilder.LoadMissiles();
            MissileProfile torpedo = MissileTestSceneBuilder.LoadOrCreateMissile("Torpedo", MissileTestSceneBuilder.ConfigureTorpedo);
            Dictionary<string, PilotProfile> pilots = DogfightTestSceneBuilder.LoadPilots();

            SpaceShip cruiser = CreateCruiser("Cruiser", cruiserProfile, fighter, Vector3.zero, Quaternion.identity, 0, guns[0], guns[1], guns[2]);
            cruiser.gameObject.AddComponent<AIPilot>();

            var attackers = new SpaceShip[FighterCount];
            for (int i = 0; i < FighterCount; i++)
            {
                Vector3 position = new Vector3((i - 1.5f) * 80f, 300f, 2500f);
                GameObject ship = CreateShip($"Red Veteran {i + 1}", fighter, position, Quaternion.LookRotation(-position));
                attackers[i] = ship.AddComponent<SpaceShip>();
                attackers[i].Team = 1;
                ship.AddComponent<ShipHealth>();
                ShipGun gun = ship.AddComponent<ShipGun>();
                gun.Weapon = cannon;
                gun.GimbalAngle = 6f;
                ship.AddComponent<AIPilot>().Profile = pilots["Veteran"];
                // The cruiser keeps flying, so patrol around it rather than a fixed point.
                ship.AddComponent<CombatBrain>().PatrolArea = cruiser.transform;
                AddTrail(ship, new Color(1f, 0.35f, 0.3f));
            }

            ScatterRocks(new GameObject("Backdrop").transform, new System.Random(8), 150, 9000f, 12000f, 100f, 400f);

            SetupCamera(cruiser.transform, out ChaseCamera chase, out FlightHud hud);
            new GameObject(nameof(SpaceAIWorld)).AddComponent<SpaceAIWorld>();
            new GameObject(nameof(ProjectileSystem)).AddComponent<ProjectileSystem>().TracerMaterial =
                GunneryTestSceneBuilder.LoadOrCreateTracerMaterial();
            new GameObject(nameof(MissileSystem)).AddComponent<MissileSystem>().TrailMaterial =
                AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            new GameObject("TurretDemo").AddComponent<TurretDemo>().Setup(cruiser, torpedo, missiles[0], missiles[2], attackers, chase, hud);

            SaveScene("TurretTest");
            Selection.activeObject = cruiser.gameObject;
        }

        /// <summary>
        /// A large ship on the fighter model scaled up, with a box hull fitted to it: point defense above and below,
        /// flak either side, a main gun forward and engines at the stern, each a subsystem with its own hitbox.
        /// </summary>
        internal static SpaceShip CreateCruiser(string name, ShipProfile profile, ShipProfile fighter, Vector3 position, Quaternion rotation, int team,
            WeaponProfile pointDefense, WeaponProfile flak, WeaponProfile main)
        {
            GameObject ship = CreateShip(name, profile, position, Quaternion.identity);
            Transform model = ship.transform.Find("Model");
            if (model != null) model.localScale = Vector3.one * (profile.Radius / fighter.Radius);

            Bounds bounds = LocalBounds(ship.transform, profile.Radius);
            Object.DestroyImmediate(ship.GetComponent<SphereCollider>());
            var box = ship.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;

            var spaceShip = ship.AddComponent<SpaceShip>();
            spaceShip.Team = team;

            Vector3 c = bounds.center, e = bounds.extents;
            float top = c.y + e.y, bottom = c.y - e.y;
            AddTurret(ship.transform, "PD Top Fore", new Vector3(0f, top + 2f, c.z + e.z * 0.45f), false, TurretRole.PointDefense, pointDefense, 180f, 180f, 900f, 2.5f, 2, 4f, 150f);
            AddTurret(ship.transform, "PD Top Aft", new Vector3(0f, top + 2f, c.z - e.z * 0.45f), false, TurretRole.PointDefense, pointDefense, 180f, 180f, 900f, 2.5f, 2, 4f, 150f);
            AddTurret(ship.transform, "PD Bottom Fore", new Vector3(0f, bottom - 2f, c.z + e.z * 0.45f), true, TurretRole.PointDefense, pointDefense, 180f, 180f, 900f, 2.5f, 2, 4f, 150f);
            AddTurret(ship.transform, "PD Bottom Aft", new Vector3(0f, bottom - 2f, c.z - e.z * 0.45f), true, TurretRole.PointDefense, pointDefense, 180f, 180f, 900f, 2.5f, 2, 4f, 150f);
            AddTurret(ship.transform, "Flak Top", new Vector3(e.x * 0.5f, top + 2.8f, c.z), false, TurretRole.Flak, flak, 180f, 90f, 300f, 3.5f, 2, 5f, 250f);
            AddTurret(ship.transform, "Flak Bottom", new Vector3(-e.x * 0.5f, bottom - 2.8f, c.z), true, TurretRole.Flak, flak, 180f, 90f, 300f, 3.5f, 2, 5f, 250f);
            AddTurret(ship.transform, "Main Gun", new Vector3(0f, top + 4.8f, c.z + e.z * 0.75f), false, TurretRole.Main, main, 160f, 25f, 50f, 6f, 3, 12f, 500f);

            float engine = Mathf.Max(4f, e.x * 0.35f);
            for (int i = -1; i <= 1; i++)
                AddEngine(ship.transform, $"Engine {i + 2}", new Vector3(i * e.x * 0.55f, c.y, c.z - e.z - engine * 0.3f), engine);

            ship.transform.rotation = rotation;
            return spaceShip;
        }

        private static void AddTurret(Transform ship, string name, Vector3 localPosition, bool underside, TurretRole role, WeaponProfile weapon,
            float yawLimit, float rate, float acceleration, float size, int barrels, float length, float health)
        {
            // The pivot sits above the base, so own-hull checks start outside the hull box.
            var mount = new GameObject(name).transform;
            mount.SetParent(ship, false);
            mount.localPosition = localPosition;
            mount.localRotation = underside ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;

            var yaw = new GameObject("Yaw").transform;
            yaw.SetParent(mount, false);
            Visual(PrimitiveType.Cylinder, yaw, new Vector3(0f, -size * 0.4f, 0f), new Vector3(size * 1.6f, size * 0.4f, size * 1.6f));
            var pitch = new GameObject("Pitch").transform;
            pitch.SetParent(yaw, false);
            float spacing = size * 0.5f;
            for (int i = 0; i < barrels; i++)
            {
                float x = barrels > 1 ? (i / (barrels - 1f) - 0.5f) * spacing : 0f;
                Visual(PrimitiveType.Cube, pitch, new Vector3(x, 0f, length * 0.5f), new Vector3(size * 0.18f, size * 0.18f, length));
            }

            var hitbox = mount.gameObject.AddComponent<BoxCollider>();
            hitbox.center = new Vector3(0f, -size * 0.4f, 0f);
            hitbox.size = new Vector3(size * 1.6f, size * 0.8f, size * 1.6f);
            var subsystem = mount.gameObject.AddComponent<Subsystem>();
            subsystem.Kind = SubsystemKind.Turret;
            subsystem.MaxHealth = health;

            var turret = mount.gameObject.AddComponent<ShipTurret>();
            turret.Weapon = weapon;
            turret.Configure(role, yawLimit, -5f, 85f, rate, acceleration);
            turret.SetBarrels(barrels, spacing, length, yaw, pitch);
        }

        private static void AddEngine(Transform ship, string name, Vector3 localPosition, float size)
        {
            GameObject engine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            engine.name = name;
            engine.transform.SetParent(ship, false);
            engine.transform.localPosition = localPosition;
            engine.transform.localScale = new Vector3(size, size, size * 0.8f);
            var subsystem = engine.AddComponent<Subsystem>();
            subsystem.Kind = SubsystemKind.Engine;
            subsystem.MaxHealth = 400f;
        }

        private static void Visual(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 scale)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
        }

        private static Bounds LocalBounds(Transform ship, float radius)
        {
            Renderer[] renderers = ship.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.zero, new Vector3(radius * 0.8f, radius * 0.4f, radius * 2f));
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            bounds.center -= ship.position;
            return bounds;
        }

        internal static WeaponProfile[] LoadTurretWeapons() => new[]
        {
            LoadOrResetWeapon("PointDefenseGun", w =>
            {
                w.Damage = 2f;
                w.FireRate = 12f;
                w.MuzzleSpeed = 1200f;
                w.Range = 800f;
                w.Spread = 1.2f;
                w.TracerColor = new Color(0.5f, 1f, 0.9f, 1f);
                w.TracerLength = 6f;
                w.TracerWidth = 0.35f;
            }),
            LoadOrResetWeapon("FlakGun", w =>
            {
                w.Kind = WeaponKind.Flak;
                w.Damage = 8f;
                w.BlastRadius = 20f;
                w.FireRate = 1.5f;
                w.MuzzleSpeed = 700f;
                w.Range = 1800f;
                w.Spread = 0.8f;
                w.TracerColor = new Color(1f, 0.5f, 0.2f, 1f);
                w.TracerLength = 4f;
                w.TracerWidth = 1.2f;
            }),
            LoadOrResetWeapon("MainGun", w =>
            {
                w.Damage = 80f;
                w.FireRate = 0.4f;
                w.MuzzleSpeed = 900f;
                w.Range = 3000f;
                w.Spread = 0.1f;
                w.TracerColor = new Color(1f, 0.4f, 0.9f, 1f);
                w.TracerLength = 30f;
                w.TracerWidth = 2f;
            })
        };

        /// <summary>Resets the asset to defaults and applies the configuration, so tuning here always reaches the asset.</summary>
        private static WeaponProfile LoadOrResetWeapon(string name, Action<WeaponProfile> configure)
        {
            string path = $"{Root}/Profiles/{name}.asset";
            var defaults = ScriptableObject.CreateInstance<WeaponProfile>();
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponProfile>(path);
            if (weapon == null)
            {
                EnsureFolder(Root + "/Profiles");
                configure(defaults);
                AssetDatabase.CreateAsset(defaults, path);
                AssetDatabase.SaveAssets();
                return defaults;
            }
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(defaults), weapon);
            Object.DestroyImmediate(defaults);
            configure(weapon);
            EditorUtility.SetDirty(weapon);
            AssetDatabase.SaveAssets();
            return weapon;
        }
    }
}
