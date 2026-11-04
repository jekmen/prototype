using System;
using System.Collections.Generic;
using SpaceAI2.Demo;
using UnityEditor;
using UnityEngine;
using static SpaceAI2.Editor.SceneBuilderUtils;

namespace SpaceAI2.Editor
{
    public static class MissileTestSceneBuilder
    {
        private const float LaneSpacing = 4000f;

        [MenuItem("Tools/SpaceAI2/Build Missile Test Scene")]
        public static void Build()
        {
            if (!NewScene()) return;
            ShipProfile fighter = LoadOrCreateProfile();
            ShipProfile cruiser = LoadOrCreateCruiser();
            Dictionary<string, PilotProfile> pilots = DogfightTestSceneBuilder.LoadPilots();
            MissileProfile[] missiles = LoadMissiles();
            MissileProfile torpedo = LoadOrCreateMissile("Torpedo", ConfigureTorpedo);

            string[] labels = { "Straight", "Rookie", "Veteran", "Ace", "Cruiser" };
            var targets = new SpaceShip[labels.Length];
            var overrides = new MissileProfile[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                Vector3 center = Vector3.right * ((i - 2) * LaneSpacing);
                var area = new GameObject($"{labels[i]} Area").transform;
                area.position = center;

                bool big = labels[i] == "Cruiser";
                GameObject ship = CreateShip($"{labels[i]} Target", big ? cruiser : fighter, center, Quaternion.identity);
                if (big)
                {
                    Transform model = ship.transform.Find("Model");
                    if (model != null) model.localScale = Vector3.one * (cruiser.Radius / fighter.Radius);
                    overrides[i] = torpedo;
                }
                targets[i] = ship.AddComponent<SpaceShip>();
                AIPilot pilot = ship.AddComponent<AIPilot>();
                if (pilots.TryGetValue(labels[i], out PilotProfile profile))
                {
                    pilot.Profile = profile;
                    ship.AddComponent<CombatBrain>().PatrolArea = area;
                }
                AddTrail(ship, new Color(1f, 0.35f, 0.3f));
            }

            ScatterRocks(new GameObject("Backdrop").transform, new System.Random(7), 150, 12000f, 15000f, 100f, 400f);

            SetupCamera(targets[0].transform, out ChaseCamera chase, out FlightHud hud);
            new GameObject(nameof(SpaceAIWorld)).AddComponent<SpaceAIWorld>();
            new GameObject(nameof(MissileSystem)).AddComponent<MissileSystem>().TrailMaterial =
                AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            new GameObject("MissileDemo").AddComponent<MissileDemo>().Setup(labels, targets, overrides, missiles, chase, hud);

            SaveScene("MissileTest");
            Selection.activeObject = targets[0].gameObject;
        }

        internal static MissileProfile[] LoadMissiles() => new[]
        {
            LoadOrCreateMissile("MissileHeat", _ => { }),
            LoadOrCreateMissile("MissileRadar", m =>
            {
                m.Guidance = MissileGuidance.Radar;
                m.MaxSpeed = 340f;
                m.Acceleration = 200f;
                m.BurnTime = 6f;
                m.Lifetime = 14f;
                m.Navigation = 3.5f;
                m.MaxTurnAcceleration = 300f;
                m.GuidanceLag = 0.45f;
                m.SeekerAngle = 60f;
                m.LockTime = 1.5f;
                m.LockAngle = 10f;
                m.LockRange = 2500f;
                m.MinRange = 400f;
                m.Damage = 110f;
                m.BlastRadius = 18f;
                m.Reload = 5f;
                m.FlameColor = new Color(0.5f, 0.7f, 1f, 1f);
                m.Length = 3.2f;
                m.Width = 0.4f;
            }),
            LoadOrCreateMissile("MissileSwarm", m =>
            {
                m.Salvo = 6;
                m.SalvoSpread = 12f;
                m.MaxSpeed = 280f;
                m.Acceleration = 300f;
                m.BurnTime = 4.5f;
                m.Drag = 0.15f;
                m.Lifetime = 8f;
                m.SeekerAngle = 60f;
                m.MaxTurnAcceleration = 260f;
                m.GuidanceLag = 0.45f;
                m.LockRange = 1200f;
                m.MinRange = 150f;
                m.Damage = 25f;
                m.BlastRadius = 8f;
                m.ProximityFuse = 5f;
                m.Hull = 2f;
                m.Reload = 6f;
                m.FlameColor = new Color(1f, 0.85f, 0.4f, 1f);
                m.Length = 1.4f;
                m.Width = 0.2f;
                m.TrailColor = new Color(0.8f, 0.8f, 0.8f, 0.3f);
            })
        };

        internal static void ConfigureTorpedo(MissileProfile m)
        {
            m.Guidance = MissileGuidance.Radar;
            m.EjectSpeed = 10f;
            m.MaxSpeed = 90f;
            m.Acceleration = 30f;
            m.BurnTime = 40f;
            m.Drag = 0f;
            m.TurnDrag = 0f;
            m.Lifetime = 40f;
            m.ArmTime = 1f;
            m.Navigation = 3f;
            m.MaxTurnAcceleration = 25f;
            m.GuidanceLag = 1f;
            m.SeekerAngle = 90f;
            m.FlareSusceptibility = 0f;
            m.LockTime = 2.5f;
            m.LockAngle = 20f;
            m.LockRange = 3000f;
            m.MinRange = 300f;
            m.MinTargetRadius = 15f;
            m.Damage = 600f;
            m.BlastRadius = 40f;
            m.ProximityFuse = 5f;
            m.Hull = 40f;
            m.Reload = 10f;
            m.BodyColor = new Color(0.5f, 0.55f, 0.6f, 1f);
            m.FlameColor = new Color(0.4f, 1f, 0.7f, 1f);
            m.Length = 7f;
            m.Width = 1.2f;
            m.TrailColor = new Color(0.6f, 0.9f, 0.8f, 0.4f);
        }

        /// <summary>Resets the asset to defaults and applies the configuration, so tuning here always reaches the asset.</summary>
        internal static MissileProfile LoadOrCreateMissile(string name, Action<MissileProfile> configure)
        {
            string path = $"{Root}/Profiles/{name}.asset";
            var defaults = ScriptableObject.CreateInstance<MissileProfile>();
            var missile = AssetDatabase.LoadAssetAtPath<MissileProfile>(path);
            if (missile == null)
            {
                EnsureFolder(Root + "/Profiles");
                missile = defaults;
                configure(missile);
                AssetDatabase.CreateAsset(missile, path);
            }
            else
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(defaults), missile);
                UnityEngine.Object.DestroyImmediate(defaults);
                configure(missile);
                EditorUtility.SetDirty(missile);
            }
            AssetDatabase.SaveAssets();
            return missile;
        }

        private static ShipProfile LoadOrCreateCruiser()
        {
            string path = $"{Root}/Profiles/Cruiser.asset";
            var profile = AssetDatabase.LoadAssetAtPath<ShipProfile>(path);
            if (profile != null) return profile;

            EnsureFolder(Root + "/Profiles");
            profile = ScriptableObject.CreateInstance<ShipProfile>();
            profile.MinSpeed = 10f;
            profile.CruiseSpeed = 25f;
            profile.MaxSpeed = 35f;
            profile.BoostSpeed = 45f;
            profile.Acceleration = 8f;
            profile.Deceleration = 10f;
            profile.PitchRate = 8f;
            profile.YawRate = 8f;
            profile.RollRate = 15f;
            profile.Hull = 3000f;
            profile.Shield = 1000f;
            profile.Radius = 30f;
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
