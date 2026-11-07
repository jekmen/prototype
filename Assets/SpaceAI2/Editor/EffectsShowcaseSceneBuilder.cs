using SpaceAI2.Demo;
using UnityEditor;
using UnityEngine;
using static SpaceAI2.Editor.SceneBuilderUtils;

namespace SpaceAI2.Editor
{
    internal static class EffectsShowcaseSceneBuilder
    {
        [MenuItem("Tools/SpaceAI2/Build Effects Showcase Scene")]
        public static void BuildScene()
        {
            if (!NewScene()) return;
            ShipProfile fighter = LoadOrCreateProfile();
            GameObject go = CreateShip("VFX Fighter", fighter, Vector3.zero, Quaternion.identity);
            SpaceShip ship = go.AddComponent<SpaceShip>();
            ship.Control = ShipControl.None;
            go.GetComponent<ShipMotor>().enabled = false;
            Rigidbody body = go.GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            go.AddComponent<ShipHealth>();
            go.AddComponent<ShipEffects>();
            var demo = new GameObject("Effects Showcase").AddComponent<EffectsShowcase>();
            demo.enabled = true;
            demo.Ship = ship;
            demo.Mode = 4;
            demo.AutoCycle = false;
            demo.Laser = AssetDatabase.LoadAssetAtPath<WeaponProfile>(Root + "/Profiles/Laser.asset");
            demo.Missile = AssetDatabase.LoadAssetAtPath<MissileProfile>(Root + "/Profiles/MissileHeat.asset");
            Camera camera = Camera.main;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.006f, 0.012f, 0.025f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 5000;
            camera.fieldOfView = 45;
            camera.transform.position = new Vector3(20, 10, -28);
            camera.transform.LookAt(Vector3.zero);
            Light key = Object.FindObjectOfType<Light>();
            key.transform.rotation = Quaternion.Euler(35, -35, 0);
            key.intensity = 1.4f;
            key.color = new Color(0.7f, 0.82f, 1f);
            var fill = new GameObject("Warm rim").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(10, 140, 0);
            fill.color = new Color(1f, 0.48f, 0.22f);
            fill.intensity = 0.8f;
            RenderSettings.ambientLight = new Color(0.15f, 0.2f, 0.3f);
            new GameObject(nameof(ProjectileSystem)).AddComponent<ProjectileSystem>().TracerMaterial = GunneryTestSceneBuilder.LoadOrCreateTracerMaterial();
            new GameObject(nameof(MissileSystem)).AddComponent<MissileSystem>().TrailMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            AddCombatEffects();
            SaveScene("EffectsShowcase");
        }
    }
}
