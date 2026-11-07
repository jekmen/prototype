using SpaceAI2.Demo;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using static SpaceAI2.Editor.SceneBuilderUtils;

namespace SpaceAI2.Editor
{
    /// <summary>
    /// The guide's starting point: two wings of four built the way a user would, with one <see cref="SpaceShip"/> and
    /// its profiles each, their UnityEvents wired to a feed, and the AI debug overlay on the camera.
    /// </summary>
    internal static class QuickstartSceneBuilder
    {
        private static readonly string[] TeamNames = { "Blue", "Red" };
        private static readonly Color[] TeamColors = { new Color(0.3f, 0.6f, 1f), new Color(1f, 0.35f, 0.3f) };
        private static readonly string[] Pilots = { "PilotAce", "PilotVeteran", "PilotVeteran", "PilotRookie" };
        private const float TeamDistance = 1600f;
        private const float WingSpacing = 40f;

        [MenuItem("Tools/SpaceAI2/Build Quickstart Scene")]
        public static void BuildScene()
        {
            if (!NewScene()) return;
            const string profiles = Root + "/Profiles/";
            var fighter = AssetDatabase.LoadAssetAtPath<ShipProfile>(profiles + "Fighter.asset");
            var cannon = AssetDatabase.LoadAssetAtPath<WeaponProfile>(profiles + "Cannon.asset");
            var heat = AssetDatabase.LoadAssetAtPath<MissileProfile>(profiles + "MissileHeat.asset");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);

            var log = new GameObject("Event Log").AddComponent<ShipEventLog>();
            SpaceShip first = null;
            for (int team = 0; team < 2; team++)
            {
                float side = team == 0 ? -1f : 1f;
                for (int i = 0; i < Pilots.Length; i++)
                {
                    var go = new GameObject($"{TeamNames[team]} {i + 1}");
                    go.transform.SetPositionAndRotation(
                        new Vector3((i - 1.5f) * WingSpacing, 0f, side * TeamDistance * 0.5f),
                        Quaternion.LookRotation(Vector3.forward * -side));
                    if (model != null)
                    {
                        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, go.transform);
                        visual.name = "Model";
                        go.AddComponent<ShipBank>().Visual = visual.transform;
                    }

                    var ship = go.AddComponent<SpaceShip>();
                    ship.Team = team;
                    ship.Profile = fighter;
                    ship.Control = ShipControl.AI;
                    ship.Pilot = AssetDatabase.LoadAssetAtPath<PilotProfile>(profiles + Pilots[i] + ".asset");
                    ship.Weapon = cannon;
                    ship.Missile = i < 2 ? heat : null;
                    ship.ApplySetup();
                    AddTrail(go, TeamColors[team]);

                    UnityEventTools.AddPersistentListener(ship.OnDestroyed, log.ShipDestroyed);
                    UnityEventTools.AddPersistentListener(ship.OnTargetAcquired, log.TargetAcquired);
                    UnityEventTools.AddPersistentListener(ship.OnMissileLaunched, log.MissileLaunched);
                    if (first == null) first = ship;
                }
            }

            Camera camera = SetupCamera(first.transform, out ChaseCamera chase, out FlightHud hud);
            Object.DestroyImmediate(hud);
            camera.gameObject.AddComponent<AIDebugOverlay>();
            chase.Offset = new Vector3(0f, 7f, -32f);
            log.Chase = chase;

            new GameObject(nameof(SpaceAIWorld)).AddComponent<SpaceAIWorld>();
            new GameObject(nameof(ProjectileSystem)).AddComponent<ProjectileSystem>().TracerMaterial =
                GunneryTestSceneBuilder.LoadOrCreateTracerMaterial();
            new GameObject(nameof(MissileSystem)).AddComponent<MissileSystem>().TrailMaterial =
                AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            SaveScene("Quickstart");
        }
    }
}
