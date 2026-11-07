using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI2
{
    /// <summary>
    /// Plays pooled particle prefabs for shots, hits and explosions in one scene. Each effect is an ordinary
    /// <see cref="ParticleSystem"/> prefab: edit the prefab to change the look.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatEffects : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private ParticleSystem muzzleFlash;
        [SerializeField] private ParticleSystem hullHit;
        [SerializeField] private ParticleSystem shieldHit;
        [SerializeField] private ParticleSystem surfaceHit;
        [SerializeField] private ParticleSystem shipExplosion;
        [SerializeField] private ParticleSystem missileExplosion;
        [Tooltip("Attached to every ship nozzle by ShipEffects.")]
        [SerializeField] private ParticleSystem engineExhaust;

        [Header("Limits")]
        [Tooltip("Small effects farther than this from the camera are skipped.")]
        [SerializeField, Min(10f)] private float cullDistance = 1500f;
        [SerializeField, Min(10f)] private float explosionCullDistance = 4000f;
        [Tooltip("Most copies of one effect alive at once; the oldest is reused past this.")]
        [SerializeField, Min(1)] private int maxPerEffect = 48;

        private static CombatEffects instance;

        private readonly Dictionary<ParticleSystem, Pool> pools = new Dictionary<ParticleSystem, Pool>();
        private ParticleSystem.Particle[] shiftBuffer = new ParticleSystem.Particle[256];
        private ProjectileSystem projectiles;
        private MissileSystem missiles;
        private Transform view;

        public ParticleSystem EngineExhaust => engineExhaust;

        public static bool TryGet(out CombatEffects effects)
        {
            if (instance == null) instance = FindObjectOfType<CombatEffects>();
            effects = instance;
            return effects != null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        public void SetPrefabs(ParticleSystem muzzle, ParticleSystem hull, ParticleSystem shield, ParticleSystem surface,
            ParticleSystem ship, ParticleSystem missile, ParticleSystem exhaust)
        {
            muzzleFlash = muzzle;
            hullHit = hull;
            shieldHit = shield;
            surfaceHit = surface;
            shipExplosion = ship;
            missileExplosion = missile;
            engineExhaust = exhaust;
        }

        private void Awake() => instance = this;

        private void OnEnable() => SpaceAI.OriginShifted += OnOriginShifted;

        // The weapon systems may be created lazily by the first shot, so this keeps looking until both are found.
        private void Connect()
        {
            if (projectiles == null && ProjectileSystem.TryGet(out projectiles))
            {
                projectiles.Fired += OnFired;
                projectiles.Impact += OnImpact;
                projectiles.Detonated += OnBurst;
            }
            if (missiles == null && MissileSystem.TryGet(out missiles))
            {
                missiles.Detonated += OnMissileDetonated;
                missiles.ShotDown += OnMissileShotDown;
            }
        }

        private void OnDisable() => SpaceAI.OriginShifted -= OnOriginShifted;

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (projectiles != null)
            {
                projectiles.Fired -= OnFired;
                projectiles.Impact -= OnImpact;
                projectiles.Detonated -= OnBurst;
            }
            if (missiles != null)
            {
                missiles.Detonated -= OnMissileDetonated;
                missiles.ShotDown -= OnMissileShotDown;
            }
        }

        private void Start() => Connect();

        private void LateUpdate()
        {
            Connect();
            view = Camera.main != null ? Camera.main.transform : null;
        }

        /// <summary>True when small effects at the point are close enough to the camera to be worth playing.</summary>
        public bool IsNear(Vector3 point) => view == null || (view.position - point).sqrMagnitude <= cullDistance * cullDistance;

        public void Hit(Vector3 point, Vector3 normal, bool shield) =>
            Play(shield ? shieldHit : hullHit, point, Rotation(normal), 1f, cullDistance);

        public void Explosion(Vector3 point, float scale, bool ship) =>
            Play(ship ? shipExplosion : missileExplosion, point, Quaternion.identity, scale, explosionCullDistance);

        private void OnFired(WeaponProfile weapon, SpaceShip owner, Vector3 origin, Vector3 direction)
        {
            ParticleSystem flash = Play(muzzleFlash, origin, Rotation(direction), 1f, cullDistance);
            if (flash == null) return;
            // The flash is shorter than a frame or two of flight, but it still has to ride with the ship.
            if (owner != null) flash.transform.SetParent(owner.transform, true);
            ParticleSystem.MainModule main = flash.main;
            main.startColor = weapon.TracerColor;
        }

        private void OnImpact(HitInfo hit)
        {
            // Ships show their own hits, split by shield and hull; this covers rocks and other colliders.
            if (hit.Ship == null) Play(surfaceHit, hit.Point, Rotation(hit.Normal), 1f, cullDistance);
        }

        private void OnBurst(WeaponProfile weapon, Vector3 point) => Explosion(point, Mathf.Max(weapon.BlastRadius * 0.25f, 1f), false);

        private void OnMissileDetonated(MissileTrack track, Vector3 point) => Explosion(point, Mathf.Max(track.Profile.BlastRadius * 0.24f, 1f), false);

        private void OnMissileShotDown(MissileTrack track, SpaceShip source) => Explosion(track.Position, 1f, false);

        private static Quaternion Rotation(Vector3 forward) =>
            forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward) : Quaternion.identity;

        private ParticleSystem Play(ParticleSystem prefab, Vector3 point, Quaternion rotation, float scale, float distance)
        {
            if (prefab == null) return null;
            if (view != null && (view.position - point).sqrMagnitude > distance * distance) return null;
            if (!pools.TryGetValue(prefab, out Pool pool)) pools[prefab] = pool = new Pool(prefab, transform);

            ParticleSystem effect = pool.Take(maxPerEffect);
            Transform t = effect.transform;
            if (t.parent != transform) t.SetParent(transform, false);
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            t.SetPositionAndRotation(point, rotation);
            t.localScale = pool.Scale * scale;
            if (!effect.gameObject.activeSelf) effect.gameObject.SetActive(true);
            effect.Play(true);
            return effect;
        }

        private void OnOriginShifted(Vector3 offset)
        {
            foreach (Pool pool in pools.Values)
                foreach (ParticleSystem effect in pool.Items)
                {
                    if (effect == null || effect.transform.parent != transform || !effect.IsAlive(true)) continue;
                    effect.transform.position -= offset;
                    foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>())
                        ShiftParticles(system, offset);
                }
        }

        private void ShiftParticles(ParticleSystem system, Vector3 offset)
        {
            if (system.main.simulationSpace != ParticleSystemSimulationSpace.World || system.particleCount == 0) return;
            if (shiftBuffer.Length < system.particleCount) shiftBuffer = new ParticleSystem.Particle[system.main.maxParticles];
            int count = system.GetParticles(shiftBuffer);
            for (int i = 0; i < count; i++) shiftBuffer[i].position -= offset;
            system.SetParticles(shiftBuffer, count);
        }

        private sealed class Pool
        {
            public readonly List<ParticleSystem> Items = new List<ParticleSystem>();
            public readonly Vector3 Scale;
            private readonly ParticleSystem prefab;
            private readonly Transform parent;
            private int next;

            public Pool(ParticleSystem prefab, Transform parent)
            {
                this.prefab = prefab;
                this.parent = parent;
                Scale = prefab.transform.localScale;
            }

            public ParticleSystem Take(int max)
            {
                Items.RemoveAll(item => item == null);
                for (int i = 0; i < Items.Count; i++)
                {
                    ParticleSystem item = Items[(next + i) % Items.Count];
                    if (item.IsAlive(true) && item.gameObject.activeInHierarchy) continue;
                    next = (next + i + 1) % Items.Count;
                    return item;
                }
                if (Items.Count < max)
                {
                    ParticleSystem created = Object.Instantiate(prefab, parent);
                    created.name = prefab.name;
                    Items.Add(created);
                    return created;
                }
                // All busy: reuse the one after the last handed out, which is the oldest.
                ParticleSystem oldest = Items[next];
                next = (next + 1) % Items.Count;
                return oldest;
            }
        }
    }
}
