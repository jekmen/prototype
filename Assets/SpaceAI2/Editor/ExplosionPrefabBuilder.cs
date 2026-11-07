using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceAI2.Editor
{
    internal static class ExplosionPrefabBuilder
    {
        private const string Folder = "Assets/SpaceAI2/Runtime/Effects/Resources/VFX";
        private const string AtlasPath = "Assets/SpaceAI2/Runtime/Effects/Resources/ExplosionGenerated64.png";
        private const float FrameRemap = (63f / 64) / (15f / 16);

        [MenuItem("Tools/SpaceAI2/Rebuild Explosion Prefabs")]
        public static void Build()
        {
            SceneBuilderUtils.EnsureFolder(Folder);
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (atlas == null) { Debug.LogError("ExplosionGenerated64.png is missing. Run Editor/Tools/bake_explosion_64.py first."); return; }
            Material fire = AtlasMaterial("ExplosionFire", atlas, new Color(1.7f, 1.4f, 1.12f, 1));
            Material smoke = AtlasMaterial("ExplosionSmoke", atlas, new Color(0.5f, 0.55f, 0.6f, 1));
            Material glow = MaterialAsset("ExplosionGlow", "SpaceAI2/Effects/Particle");
            glow.SetFloat("_Mode", 0);
            Material debris = MaterialAsset("ExplosionFragments", "SpaceAI2/Effects/Debris");
            Mesh mesh = FragmentMesh();
            Light flashLight = FlashLight();
            BuildPrefab("ExplosionShip", true, fire, smoke, glow, debris, mesh, flashLight);
            BuildPrefab("ExplosionMissile", false, fire, smoke, glow, debris, mesh, flashLight);
            AssetDatabase.SaveAssets();
            Debug.Log("Built editable Particle System explosion prefabs (8x8 Texture Sheet Animation).");
        }

        [MenuItem("Tools/SpaceAI2/Upgrade Explosion Flipbooks to 64 Frames")]
        public static void UpgradeFlipbooks()
        {
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (atlas == null) { Debug.LogError("Bake ExplosionGenerated64.png before upgrading."); return; }
            foreach (string name in new[] { "ExplosionShip", "ExplosionMissile" })
            {
                string path = Folder + "/" + name + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        var sheet = system.textureSheetAnimation;
                        if (!sheet.enabled || sheet.numTilesX != 4 || sheet.numTilesY != 4) continue;
                        sheet.frameOverTime = RemapFrames(sheet.frameOverTime);
                        sheet.startFrame = RemapFrames(sheet.startFrame);
                        sheet.numTilesX = sheet.numTilesY = 8;
                        system.name = system.name.Replace("4x4", "8x8");
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (string name in new[] { "ExplosionFire", "ExplosionSmoke" })
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + name + ".mat");
                material.mainTexture = atlas;
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Upgraded explosion flipbooks to 64 frames; particle module tuning and playback timing preserved.");
        }

        private static ParticleSystem.MinMaxCurve RemapFrames(ParticleSystem.MinMaxCurve curve)
        {
            if (curve.mode == ParticleSystemCurveMode.Constant) curve.constant *= FrameRemap;
            else if (curve.mode == ParticleSystemCurveMode.TwoConstants)
            {
                curve.constantMin *= FrameRemap;
                curve.constantMax *= FrameRemap;
            }
            else if (curve.mode == ParticleSystemCurveMode.Curve) curve.curve = RemapKeys(curve.curve);
            else
            {
                curve.curveMin = RemapKeys(curve.curveMin);
                curve.curveMax = RemapKeys(curve.curveMax);
            }
            return curve;
        }

        private static AnimationCurve RemapKeys(AnimationCurve curve)
        {
            // Texture Sheet Animation clamps curveMultiplier to 1; scale actual keys instead.
            Keyframe[] keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].value *= FrameRemap;
                keys[i].inTangent *= FrameRemap;
                keys[i].outTangent *= FrameRemap;
            }
            return new AnimationCurve(keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
        }

        private static Material MaterialAsset(string name, string shader)
        {
            string path = Folder + "/" + name + ".mat";
            Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null)
            {
                result = new Material(Shader.Find(shader)) { name = name };
                AssetDatabase.CreateAsset(result, path);
            }
            result.shader = Shader.Find(shader);
            EditorUtility.SetDirty(result);
            return result;
        }

        private static Material AtlasMaterial(string name, Texture2D atlas, Color color)
        {
            Material material = MaterialAsset(name, "Particles/Standard Unlit");
            material.mainTexture = atlas;
            material.SetColor("_Color", color);
            material.SetFloat("_Mode", 2); // Fade: smoke must attenuate the background.
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_FlipbookMode", 1);
            material.EnableKeyword("_ALPHABLEND_ON");
            material.EnableKeyword("_REQUIRE_UV2");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            return material;
        }

        private static ParticleSystem Layer(Transform parent, string name, Material material, int count, float life, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = false;
            main.duration = 4;
            main.simulationSpeed = 2;
            main.playOnAwake = true;
            main.startLifetime = life;
            main.startSpeed = 0;
            main.startSize = size;
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.maxParticles = Mathf.Max(count * 2, 8);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = system.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var shape = system.shape; shape.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = 1;
            return system;
        }

        private static void ColorOverLife(ParticleSystem system, Color initial, Color final, float hold, float opacity = 1)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(initial, 0), new GradientColorKey(final, 1) },
                new[] { new GradientAlphaKey(opacity, 0), new GradientAlphaKey(opacity, hold), new GradientAlphaKey(0, 1) });
            var color = system.colorOverLifetime; color.enabled = true; color.color = gradient;
        }

        private static void Flipbook(ParticleSystem system, bool smokeOnly = false)
        {
            var sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.numTilesX = sheet.numTilesY = 8;
            sheet.cycleCount = 1;
            sheet.startFrame = 0;
            // Preserve the approved timing and stop on frame 63 without wrapping to ignition.
            sheet.frameOverTime = RemapFrames(new ParticleSystem.MinMaxCurve(1, smokeOnly
                ? AnimationCurve.Linear(0, 9f / 16, 1, 15f / 16)
                : new AnimationCurve(new Keyframe(0, 0, 2.4f, 2.4f), new Keyframe(0.15f, 0.3f, 1.2f, 1.2f),
                    new Keyframe(0.45f, 0.62f, 0.65f, 0.65f), new Keyframe(1, 15f / 16, 0.3f, 0.3f))));
            system.GetComponent<ParticleSystemRenderer>().SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV, ParticleSystemVertexStream.UV2, ParticleSystemVertexStream.AnimBlend });
        }

        private static void BuildPrefab(string name, bool ship, Material fire, Material smokeMaterial, Material glow,
            Material debris, Mesh mesh, Light light)
        {
            ParticleSystem root = Layer(null, name, glow, 0, 0.1f, 0);
            var rootEmission = root.emission; rootEmission.enabled = false;
            var rootMain = root.main; rootMain.duration = ship ? 4 : 3;

            ParticleSystem flash = Layer(root.transform, "01 Flash", glow, 1, 0.13f, 2.4f);
            ColorOverLife(flash, new Color(1, 0.86f, 0.6f), new Color(1, 0.35f, 0.06f), 0.05f);
            var flashSize = flash.sizeOverLifetime; flashSize.enabled = true;
            flashSize.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 0.3f, 1, 1));
            var lights = flash.lights;
            lights.enabled = true; lights.light = light; lights.ratio = 1; lights.maxLights = 1;
            lights.useParticleColor = true; lights.alphaAffectsIntensity = true; lights.sizeAffectsRange = true;

            ParticleSystem core = Layer(root.transform, "02 Fire and smoke - 8x8", fire, 1, ship ? 2.8f : 1.7f, 3.4f);
            Flipbook(core);
            ColorOverLife(core, Color.white, new Color(0.65f, 0.68f, 0.74f), 0.68f);

            if (ship)
            {
                ParticleSystem secondary = Layer(root.transform, "03 Secondary fireballs - 8x8", fire, 1, 1.8f, 1.3f);
                var main = secondary.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.1f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.95f, 1.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
                var emission = secondary.emission;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0.12f, 1), new ParticleSystem.Burst(0.27f, 1), new ParticleSystem.Burst(0.44f, 1) });
                var shape = secondary.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.4f;
                Flipbook(secondary);
                ColorOverLife(secondary, Color.white, new Color(0.6f, 0.62f, 0.65f), 0.55f, 0.8f);
            }

            ParticleSystem smoke = Layer(root.transform, "04 Drifting smoke - 8x8", smokeMaterial, ship ? 3 : 2, ship ? 2.7f : 1.9f, 2.6f);
            var smokeMain = smoke.main;
            smokeMain.startDelay = 0.5f;
            smokeMain.startSize = new ParticleSystem.MinMaxCurve(2, 2.8f);
            smokeMain.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            var smokeShape = smoke.shape; smokeShape.enabled = true; smokeShape.shapeType = ParticleSystemShapeType.Sphere; smokeShape.radius = 0.12f;
            Flipbook(smoke, true);
            ColorOverLife(smoke, Color.white, Color.white, 0.15f, 0.22f);
            var smokeSize = smoke.sizeOverLifetime; smokeSize.enabled = true;
            smokeSize.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 0.8f, 1, 1.25f));
            var noise = smoke.noise; noise.enabled = true; noise.strength = 0.04f; noise.frequency = 0.7f; noise.scrollSpeed = 0.1f;
            var spin = smoke.rotationOverLifetime; spin.enabled = true; spin.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

            ParticleSystem sparks = Layer(root.transform, "05 Embers", glow, ship ? 32 : 18, 0.8f, 0.018f);
            var sparkMain = sparks.main;
            sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 1.1f);
            sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.025f);
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 5.5f);
            var sparkShape = sparks.shape; sparkShape.enabled = true; sparkShape.shapeType = ParticleSystemShapeType.Sphere; sparkShape.radius = 0.05f;
            ColorOverLife(sparks, new Color(1, 0.7f, 0.22f), new Color(0.6f, 0.08f, 0.01f), 0.1f);
            var sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch; sparkRenderer.lengthScale = 2; sparkRenderer.velocityScale = 0.04f;
            var limit = sparks.limitVelocityOverLifetime; limit.enabled = true; limit.limit = 2; limit.dampen = 0.12f;

            ParticleSystem fragments = Layer(root.transform, "06 Hull fragments", debris, ship ? 10 : 4, 2, 0.04f);
            var fragmentMain = fragments.main;
            fragmentMain.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
            fragmentMain.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.3f);
            fragmentMain.startRotation3D = true;
            fragmentMain.startRotationX = fragmentMain.startRotationY = fragmentMain.startRotationZ = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var fragmentShape = fragments.shape; fragmentShape.enabled = true; fragmentShape.shapeType = ParticleSystemShapeType.Sphere; fragmentShape.radius = 0.12f;
            var fragmentSpin = fragments.rotationOverLifetime; fragmentSpin.enabled = true; fragmentSpin.separateAxes = true;
            fragmentSpin.x = new ParticleSystem.MinMaxCurve(-4, 4); fragmentSpin.y = new ParticleSystem.MinMaxCurve(-6, 6); fragmentSpin.z = new ParticleSystem.MinMaxCurve(-3, 3);
            ColorOverLife(fragments, new Color(1, 0.28f, 0.015f), Color.black, 0.8f);
            var fragmentRenderer = fragments.GetComponent<ParticleSystemRenderer>(); fragmentRenderer.renderMode = ParticleSystemRenderMode.Mesh; fragmentRenderer.mesh = mesh;

            root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, Folder + "/" + name + ".prefab");
            Object.DestroyImmediate(root.gameObject);
        }

        private static Light FlashLight()
        {
            var go = new GameObject("Explosion flash light");
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point; light.range = 4; light.intensity = 4;
            light.shadows = LightShadows.None; light.color = new Color(1, 0.7f, 0.35f);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, Folder + "/ExplosionFlashLight.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Light>();
        }

        private static Mesh FragmentMesh()
        {
            string path = Folder + "/ExplosionFragment.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            Vector3[] corners = { new Vector3(-0.4f,-0.25f,-0.6f), new Vector3(0.5f,-0.15f,-0.4f),
                new Vector3(0.2f,-0.1f,0.6f), new Vector3(-0.3f,0.35f,0.2f) };
            int[] faces = { 0,2,1, 0,1,3, 1,2,3, 2,0,3 };
            var vertices = new Vector3[faces.Length]; var triangles = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++) { vertices[i] = corners[faces[i]]; triangles[i] = i; }
            mesh = new Mesh { name = "Hull fragment", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
