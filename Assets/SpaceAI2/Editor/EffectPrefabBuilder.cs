using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceAI2.Editor
{
    /// <summary>
    /// Builds the small combat effect prefabs (muzzle flash, hits, engine exhaust) as plain particle systems on the
    /// built-in Particles/Standard Unlit shader. The explosion prefabs are hand-tuned assets and are not rebuilt here.
    /// </summary>
    internal static class EffectPrefabBuilder
    {
        public const string Folder = SceneBuilderUtils.Root + "/Content/Effects";
        private const string AdditivePath = Folder + "/ParticleAdditive.mat";
        private const string AlphaPath = Folder + "/ParticleAlpha.mat";
        private const string TrailPath = Folder + "/MissileTrail.mat";
        private const string ShieldPath = Folder + "/Shield.mat";

        private static readonly Color Orange = new Color(1f, 0.55f, 0.2f);
        private static readonly Color ShieldBlue = new Color(0.3f, 0.7f, 1f);
        private static readonly Color EngineTeal = new Color(0.35f, 0.85f, 1f);

        [MenuItem("Tools/SpaceAI2/Build Effect Prefabs")]
        public static void Build()
        {
            SceneBuilderUtils.EnsureFolder(Folder);
            Material additive = ParticleMaterial(AdditivePath, true);
            Material alpha = ParticleMaterial(AlphaPath, false);
            // A trail stretches the texture over its whole length; sampling only the centre column keeps
            // the soft edge across the width without fading the ends.
            Material trail = ParticleMaterial(TrailPath, false);
            trail.mainTextureScale = new Vector2(0f, 1f);
            trail.mainTextureOffset = new Vector2(0.5f, 0f);
            EditorUtility.SetDirty(trail);
            if (AssetDatabase.LoadAssetAtPath<Material>(ShieldPath) == null)
                AssetDatabase.CreateAsset(new Material(AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/Shield.shader")), ShieldPath);
            Save(MuzzleFlash(additive), "MuzzleFlash");
            Save(Hit("HitHull", additive, alpha, Orange, 4f, 16, true), "HitHull");
            Save(Hit("HitShield", additive, alpha, ShieldBlue, 7f, 8, false), "HitShield");
            Save(Hit("HitSurface", additive, alpha, new Color(1f, 0.8f, 0.55f), 3f, 12, true), "HitSurface");
            Save(Exhaust(additive), "EngineExhaust");
            Save(MissileFlame(additive), "MissileFlame");
            AssetDatabase.SaveAssets();
        }

        /// <summary>Points CombatEffects at the prefabs, building any that are missing.</summary>
        public static void Assign(CombatEffects effects)
        {
            if (Load("MuzzleFlash") == null || Load("EngineExhaust") == null || Load("MissileFlame") == null ||
                AssetDatabase.LoadAssetAtPath<Material>(TrailPath) == null || AssetDatabase.LoadAssetAtPath<Material>(ShieldPath) == null) Build();
            effects.SetPrefabs(Load("MuzzleFlash"), Load("HitHull"), Load("HitShield"), Load("HitSurface"),
                Load("ExplosionShip"), Load("ExplosionMissile"), Load("EngineExhaust"), Load("MissileFlame"));
            effects.ShieldMaterial = AssetDatabase.LoadAssetAtPath<Material>(ShieldPath);
            EditorUtility.SetDirty(effects);
            // The soft edge keeps missile smoke from reading as a flat ribbon up close.
            var missiles = Object.FindObjectOfType<MissileSystem>(true);
            if (missiles != null)
            {
                missiles.TrailMaterial = AssetDatabase.LoadAssetAtPath<Material>(TrailPath);
                EditorUtility.SetDirty(missiles);
            }
        }

        private static ParticleSystem Load(string name) => AssetDatabase.LoadAssetAtPath<ParticleSystem>($"{Folder}/{name}.prefab");

        private static void Save(GameObject root, string name)
        {
            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{name}.prefab");
            Object.DestroyImmediate(root);
        }

        /// <summary>Particles/Standard Unlit set up the way its inspector does for Additive or Fade.</summary>
        public static Material ParticleMaterial(string path, bool additive)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created) material = new Material(Shader.Find("Particles/Standard Unlit"));
            material.mainTexture = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            SetBlend(material, additive);
            if (created) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
        }

        public static void SetBlend(Material material, bool additive)
        {
            material.SetFloat("_Mode", additive ? 4f : 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static GameObject MuzzleFlash(Material additive)
        {
            ParticleSystem flash = System("MuzzleFlash", null, additive, ParticleSystemSimulationSpace.Local);
            var main = flash.main;
            main.duration = 0.1f;
            main.startLifetime = 0.06f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(2f, 3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Burst(flash, 1);
            Fade(flash, Color.white, Color.white, 0.2f);

            ParticleSystem sparks = System("Sparks", flash.transform, additive, ParticleSystemSimulationSpace.Local);
            main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(20f, 40f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startColor = new Color(1f, 0.9f, 0.7f);
            Burst(sparks, 3);
            Cone(sparks, 8f, 0.05f);
            Stretch(sparks, 0.03f);
            return flash.gameObject;
        }

        /// <summary>A flash at the point, sparks thrown along +Z (the surface normal) and, optionally, a puff of smoke.</summary>
        private static GameObject Hit(string name, Material additive, Material alpha, Color color, float flashSize, int sparkCount, bool smoke)
        {
            ParticleSystem flash = System(name, null, additive, ParticleSystemSimulationSpace.World);
            var main = flash.main;
            main.startLifetime = 0.2f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(flashSize * 0.8f, flashSize * 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            Burst(flash, 1);
            Fade(flash, Color.white, Color.white, 0.5f);

            ParticleSystem sparks = System("Sparks", flash.transform, additive, ParticleSystemSimulationSpace.World);
            main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(10f, 30f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startColor = Color.Lerp(color, Color.white, 0.4f);
            Burst(sparks, sparkCount);
            Cone(sparks, 60f, 0.1f);
            Stretch(sparks, 0.04f);
            Fade(sparks, Color.white, color, 0.3f);

            if (smoke)
            {
                ParticleSystem puff = System("Smoke", flash.transform, alpha, ParticleSystemSimulationSpace.World);
                main = puff.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
                main.startSize = new ParticleSystem.MinMaxCurve(2.5f, 4f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(0.4f, 0.37f, 0.35f, 0.6f);
                Burst(puff, 2);
                Cone(puff, 40f, 0.2f);
                Fade(puff, Color.white, Color.white, 1f);
                var size = puff.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            }
            return flash.gameObject;
        }

        /// <summary>Emits along +Z; ShipEffects turns it to face out of the nozzle and scales its lifetime by thrust.</summary>
        private static GameObject Exhaust(Material additive)
        {
            ParticleSystem jet = System("EngineExhaust", null, additive, ParticleSystemSimulationSpace.Local);
            var main = jet.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.startLifetime = 0.15f;
            main.startSpeed = 25f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 0.9f);
            main.startColor = EngineTeal;
            main.maxParticles = 32;
            var emission = jet.emission;
            emission.rateOverTime = 80f;
            Cone(jet, 1f, 0.05f);
            Stretch(jet, 0.08f, 2f);
            Fade(jet, Color.white, EngineTeal, 1f);
            var size = jet.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

            ParticleSystem glow = System("Glow", jet.transform, additive, ParticleSystemSimulationSpace.Local);
            main = glow.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.startLifetime = 0.1f;
            main.startSpeed = 0f;
            main.startSize = 1.6f;
            main.startColor = new Color(EngineTeal.r, EngineTeal.g, EngineTeal.b, 0.7f);
            main.maxParticles = 4;
            emission = glow.emission;
            emission.rateOverTime = 30f;
            return jet.gameObject;
        }

        /// <summary>
        /// Sized for a 1 m wide missile; CombatEffects scales it by the profile's width and tints the core with its flame
        /// colour. World-space smoke would duplicate the missile's trail, so this is only the motor flame.
        /// </summary>
        private static GameObject MissileFlame(Material additive)
        {
            ParticleSystem jet = System("MissileFlame", null, additive, ParticleSystemSimulationSpace.Local);
            var main = jet.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = 0.12f;
            main.startSpeed = 40f;
            main.startSize = new ParticleSystem.MinMaxCurve(2.4f, 3f);
            main.maxParticles = 24;
            var emission = jet.emission;
            emission.rateOverTime = 160f;
            Cone(jet, 2f, 0.1f);
            Stretch(jet, 0.08f, 1.2f);
            Fade(jet, Color.white, Color.white, 0.6f);
            var size = jet.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            ParticleSystem glow = System("Glow", jet.transform, additive, ParticleSystemSimulationSpace.Local);
            main = glow.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = 0.1f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
            main.startColor = new Color(1f, 1f, 1f, 0.6f);
            main.maxParticles = 4;
            emission = glow.emission;
            emission.rateOverTime = 30f;
            return jet.gameObject;
        }

        private static ParticleSystem System(string name, Transform parent, Material material, ParticleSystemSimulationSpace space)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.simulationSpace = space;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 64;
            var emission = system.emission;
            emission.rateOverTime = 0f;
            var shape = system.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 2f;
            return system;
        }

        private static void Burst(ParticleSystem system, int count)
        {
            var emission = system.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        private static void Cone(ParticleSystem system, float angle, float radius)
        {
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
        }

        private static void Stretch(ParticleSystem system, float velocityScale, float lengthScale = 1f)
        {
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = velocityScale;
            renderer.lengthScale = lengthScale;
        }

        /// <summary>Colour from start to end over the lifetime, with alpha falling to zero after the hold.</summary>
        private static void Fade(ParticleSystem system, Color start, Color end, float hold)
        {
            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, Mathf.Clamp(hold, 0f, 0.99f) * 0.5f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
        }
    }
}
