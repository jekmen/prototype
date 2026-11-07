using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceAI2
{
    /// <summary>Shared, bounded particle buffers and instanced engine plumes for one scene.</summary>
    [DefaultExecutionOrder(200)]
    public class SpaceEffects : MonoBehaviour
    {
        [SerializeField] private ParticleSystem shipExplosionPrefab;
        [SerializeField] private ParticleSystem missileExplosionPrefab;
        private static SpaceEffects instance;
        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly List<ShipEffects> ships = new List<ShipEffects>();
        private readonly Matrix4x4[] engines = new Matrix4x4[1023];
        private ParticleSystem glow, smoke, sparks;
        private sealed class ExplosionInstance
        {
            public ParticleSystem Root;
            public ParticleSystem[] Systems;
            public Vector3 Velocity;
            public float Started;
        }
        private readonly List<ExplosionInstance> shipBursts = new List<ExplosionInstance>(16);
        private readonly List<ExplosionInstance> missileBursts = new List<ExplosionInstance>(32);
        private Material exhaust;
        private Mesh plume;
        private ProjectileSystem projectiles;
        private MissileSystem missiles;
        private int engineCount, emittedThisFrame, emissionFrame;
        private Camera view;
        public Material ShieldMaterial { get; private set; }
        public Mesh Sphere { get; private set; }
        public Mesh ExhaustMesh => plume;
        public int ParticleCount => glow.particleCount + smoke.particleCount + sparks.particleCount
            + CountParticles(shipBursts) + CountParticles(missileBursts);
        public static SpaceEffects Instance
        {
            get
            {
                if (instance == null) instance = FindObjectOfType<SpaceEffects>();
                if (instance == null) instance = new GameObject(nameof(SpaceEffects)).AddComponent<SpaceEffects>();
                return instance;
            }
        }
        public static bool TryGet(out SpaceEffects effects) { effects = instance; return effects != null; }
        public void Register(ShipEffects ship) { if (!ships.Contains(ship)) ships.Add(ship); }
        public void Unregister(ShipEffects ship) => ships.Remove(ship);

        private Material MakeMaterial(string shader, int mode = 0)
        {
            var result = new Material(Shader.Find(shader)) { enableInstancing = true };
            if (result.HasProperty("_Mode")) result.SetFloat("_Mode", mode);
            ownedMaterials.Add(result);
            return result;
        }
        private void Awake()
        {
            instance = this;
            Sphere = EffectsMeshes.Sphere(64, 32);
            ShieldMaterial = MakeMaterial("SpaceAI2/Effects/Shield");
            exhaust = MakeMaterial("SpaceAI2/Effects/Exhaust");
            plume = MakePlume();
            var camera = Camera.main;
            if (camera != null && camera.GetComponent<SpaceBloom>() == null) camera.gameObject.AddComponent<SpaceBloom>();
            glow = MakeParticles("Plasma", 0, 3000, false, false);
            smoke = MakeParticles("Cooling vapor", 2, 1000, true, false);
            sparks = MakeParticles("Sparks", 0, 4000, false, true);
            if (shipExplosionPrefab == null) shipExplosionPrefab = Resources.Load<ParticleSystem>("VFX/ExplosionShip");
            if (missileExplosionPrefab == null) missileExplosionPrefab = Resources.Load<ParticleSystem>("VFX/ExplosionMissile");
            for (int i = 0; i < 4; i++)
            {
                if (shipExplosionPrefab != null) CreateExplosion(shipBursts, shipExplosionPrefab);
                if (missileExplosionPrefab != null) CreateExplosion(missileBursts, missileExplosionPrefab);
            }
            projectiles = ProjectileSystem.Instance;
            missiles = MissileSystem.Instance;
            projectiles.Fired += OnFired;
            projectiles.Impact += OnImpact;
            projectiles.Detonated += OnFlak;
            missiles.Detonated += OnMissile;
            missiles.ShotDown += OnShotDown;
            missiles.Launched += OnLaunch;
            missiles.Expired += OnExpired;
        }
        private ParticleSystem MakeParticles(string name, int mode, int capacity, bool grow, bool stretch)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = capacity;
            main.startSpeed = 0;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var size = system.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, grow
                ? AnimationCurve.EaseInOut(0, 0.3f, 1, 1.6f) : AnimationCurve.Linear(0, 1, 1, 0.1f));
            var color = system.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.8f, 0.15f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MakeMaterial("SpaceAI2/Effects/Particle", mode);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 0.6f;
            if (stretch) { renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = 1.5f; renderer.velocityScale = 0.045f; }
            system.Play();
            return system;
        }
        private bool Visible(Vector3 point, float distance = 1800f) => view == null || (view.transform.position - point).sqrMagnitude < distance * distance;
        private void Emit(ParticleSystem system, Vector3 point, Vector3 velocity, float size, float life, Color color)
        {
            if (emissionFrame != Time.frameCount) { emissionFrame = Time.frameCount; emittedThisFrame = 0; }
            if (++emittedThisFrame > 900) return;
            var p = new ParticleSystem.EmitParams { position = point, velocity = velocity, startSize = size, startLifetime = life,
                startColor = color, rotation = Random.value * 360f };
            system.Emit(p, 1);
        }
        public void Engine(Matrix4x4 matrix) { if (engineCount < engines.Length) engines[engineCount++] = matrix; }
        public void EngineMote(Vector3 point, Vector3 velocity, float size) =>
            Emit(glow, point, velocity, size, 0.16f, new Color(0.12f, 0.65f, 1f, 0.5f));
        public void Hit(Vector3 point, Vector3 normal, bool shield)
        {
            if (!Visible(point)) return;
            Color color = shield ? new Color(0.12f, 0.65f, 1f) : new Color(1f, 0.43f, 0.08f);
            Emit(glow, point, Vector3.zero, shield ? 3f : 2.5f, 0.18f, color);
            for (int i = 0; i < (shield ? 7 : 14); i++)
                Emit(sparks, point, (normal + Random.onUnitSphere) * Random.Range(6f, 24f), Random.Range(0.08f, 0.24f), Random.Range(0.15f, 0.6f), color);
        }
        public void Explosion(Vector3 point, float radius, Vector3 velocity, bool shipDeath = false)
        {
            if (!Visible(point, 3500f)) return;
            ParticleSystem prefab = shipDeath ? shipExplosionPrefab : missileExplosionPrefab;
            if (prefab == null) return;
            List<ExplosionInstance> pool = shipDeath ? shipBursts : missileBursts;
            ExplosionInstance burst = null;
            foreach (ExplosionInstance candidate in pool)
                if (!candidate.Root.gameObject.activeSelf) { burst = candidate; break; }
            if (burst == null && pool.Count < (shipDeath ? 16 : 32)) burst = CreateExplosion(pool, prefab);
            if (burst == null)
            {
                burst = pool[0];
                foreach (ExplosionInstance candidate in pool)
                    if (candidate.Started < burst.Started) burst = candidate;
            }
            burst.Root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            burst.Root.transform.SetPositionAndRotation(point, Quaternion.identity);
            burst.Root.transform.localScale = Vector3.one * Mathf.Clamp(radius, 1f, 25f);
            burst.Velocity = velocity * 0.2f;
            burst.Started = Time.time;
            burst.Root.gameObject.SetActive(true);
            burst.Root.Play(true);
        }
        private ExplosionInstance CreateExplosion(List<ExplosionInstance> pool, ParticleSystem prefab)
        {
            ParticleSystem root = Instantiate(prefab, transform);
            root.name = prefab.name;
            root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            root.gameObject.SetActive(false);
            var burst = new ExplosionInstance { Root = root, Systems = root.GetComponentsInChildren<ParticleSystem>(true) };
            pool.Add(burst);
            return burst;
        }
        private static int CountParticles(List<ExplosionInstance> pool)
        {
            int count = 0;
            foreach (ExplosionInstance burst in pool)
                if (burst.Root.gameObject.activeSelf)
                    foreach (ParticleSystem system in burst.Systems) count += system.particleCount;
            return count;
        }
        private static void UpdateExplosions(List<ExplosionInstance> pool)
        {
            foreach (ExplosionInstance burst in pool)
            {
                if (!burst.Root.gameObject.activeSelf) continue;
                burst.Root.transform.position += burst.Velocity * Time.deltaTime;
                if (!burst.Root.IsAlive(true)) burst.Root.gameObject.SetActive(false);
            }
        }
        private void OnFired(WeaponProfile weapon, Vector3 point, Vector3 direction)
        {
            if (!Visible(point)) return;
            Emit(glow, point, direction * 3, weapon.IsInstant ? 2f : 1.6f, 0.07f, weapon.TracerColor);
            for (int i = 0; i < 3; i++)
                Emit(sparks, point, direction * Random.Range(8f, 20f) + Random.insideUnitSphere * 3, 0.12f, 0.07f, weapon.TracerColor);
        }
        private void OnImpact(HitInfo hit) { if (hit.Ship == null && hit.BlastRadius <= 0) Hit(hit.Point, hit.Normal, false); }
        private void OnFlak(WeaponProfile weapon, Vector3 point) => Explosion(point, weapon.BlastRadius * 0.25f, Vector3.zero);
        private void OnMissile(MissileTrack track, Vector3 point) => Explosion(point, track.Profile.BlastRadius * 0.24f, track.Velocity * 0.1f);
        private void OnShotDown(MissileTrack track, SpaceShip source) => Explosion(track.Position, 2f, track.Velocity * 0.15f);
        private void OnLaunch(MissileTrack track) => Hit(track.Position, -track.Velocity.normalized, false);
        private void OnExpired(MissileTrack track)
        {
            if (Visible(track.Position))
                Emit(smoke, track.Position, track.Velocity * 0.1f, 2, 0.6f, new Color(0.4f, 0.3f, 0.2f));
        }
        private void LateUpdate()
        {
            view = Camera.main;
            UpdateExplosions(shipBursts);
            UpdateExplosions(missileBursts);
            engineCount = 0;
            for (int i = 0; i < ships.Count; i++)
                if (ships[i] != null && Visible(ships[i].transform.position)) ships[i].Render(this, view);
            if (engineCount > 0) Graphics.DrawMeshInstanced(plume, 0, exhaust, engines, engineCount, null, ShadowCastingMode.Off, false);
        }
        private Mesh MakePlume()
        {
            const int sides = 16, rows = 9;
            var vertices = new Vector3[sides * rows];
            var indices = new int[sides * (rows - 1) * 6];
            int at = 0;
            for (int z = 0; z < rows; z++)
            {
                float t = (float)z / (rows - 1);
                float radius = Mathf.Lerp(0.5f, 0.015f, t) * (0.85f + 0.15f * Mathf.Cos(t * 24));
                for (int s = 0; s < sides; s++)
                {
                    float angle = s * Mathf.PI * 2 / sides;
                    vertices[z * sides + s] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, t - 0.5f);
                    if (z == rows - 1) continue;
                    int a = z * sides + s, b = z * sides + (s + 1) % sides;
                    indices[at++] = a; indices[at++] = a + sides; indices[at++] = b;
                    indices[at++] = b; indices[at++] = a + sides; indices[at++] = b + sides;
                }
            }
            var mesh = new Mesh { name = "Plasma plume", vertices = vertices, triangles = indices };
            mesh.RecalculateBounds();
            return mesh;
        }
        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (projectiles != null) { projectiles.Fired -= OnFired; projectiles.Impact -= OnImpact; projectiles.Detonated -= OnFlak; }
            if (missiles != null) { missiles.Detonated -= OnMissile; missiles.ShotDown -= OnShotDown; missiles.Launched -= OnLaunch; missiles.Expired -= OnExpired; }
            foreach (Material material in ownedMaterials) Destroy(material);
            if (plume != null) Destroy(plume);
            if (Sphere != null) Destroy(Sphere);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;
    }
}
