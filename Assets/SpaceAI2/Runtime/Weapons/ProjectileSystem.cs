using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceAI2
{
    /// <summary>
    /// Simulates all projectiles in plain arrays and checks hits with one batched raycast per physics step.
    /// Runs after other FixedUpdates so shots fired this step are swept right away, and after the missiles so
    /// projectiles meet them where they are.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public class ProjectileSystem : MonoBehaviour
    {
        private struct Projectile
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Fuse;
            public float Age;
            public int Weapon;
            public SpaceShip Owner;
        }

        private struct Beam
        {
            public Vector3 From;
            public Vector3 To;
            public float Life;
            public int Weapon;
        }

        private const int MaxHits = 2;
        private const int BatchSize = 1023;

        private static ProjectileSystem instance;

        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;
        [Tooltip("Base material for tracers; must support GPU instancing. Falls back to the SpaceAI2/Tracer shader.")]
        [SerializeField] private Material tracerMaterial;
        [SerializeField, Min(16)] private int initialCapacity = 1024;

        private readonly List<WeaponProfile> weapons = new List<WeaponProfile>();
        private readonly Dictionary<WeaponProfile, int> weaponIndex = new Dictionary<WeaponProfile, int>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Beam> beams = new List<Beam>();
        private readonly List<SpaceShip> blastScratch = new List<SpaceShip>();
        private readonly RaycastHit[] laserHits = new RaycastHit[8];
        private readonly Matrix4x4[] matrices = new Matrix4x4[BatchSize];

        private Projectile[] projectiles;
        private readonly System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        private int count;
        private NativeArray<RaycastCommand> commands;
        private NativeArray<RaycastHit> results;
        private Mesh mesh;

        public event Action<HitInfo> Impact;
        /// <summary>Raised for every shot, from the muzzle, before the shot travels.</summary>
        public event Action<WeaponProfile, SpaceShip, Vector3, Vector3> Fired;
        /// <summary>Raised where a fused shot bursts, whether or not it damages anything.</summary>
        public event Action<WeaponProfile, Vector3> Detonated;

        public static ProjectileSystem Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = FindObjectOfType<ProjectileSystem>();
                if (instance == null) instance = new GameObject(nameof(ProjectileSystem)).AddComponent<ProjectileSystem>();
                return instance;
            }
        }

        public static bool TryGet(out ProjectileSystem system)
        {
            system = instance;
            return system != null;
        }

        public int Count => count;
        /// <summary>Main-thread time of the last physics step, in milliseconds.</summary>
        public double LastUpdateMs { get; private set; }

        public Material TracerMaterial
        {
            get => tracerMaterial;
            set => tracerMaterial = value;
        }

        public void Fire(WeaponProfile weapon, SpaceShip owner, Vector3 origin, Vector3 direction, float fuse = 0f)
        {
            if (weapon == null) return;
            int index = IndexOf(weapon);
            Fired?.Invoke(weapon, owner, origin, direction);

            if (weapon.IsInstant)
            {
                FireLaser(weapon, index, owner, origin, direction);
                return;
            }

            if (count == projectiles.Length) Array.Resize(ref projectiles, count * 2);
            projectiles[count++] = new Projectile
            {
                Position = origin,
                Velocity = (owner != null ? owner.Velocity : Vector3.zero) + direction * weapon.MuzzleSpeed,
                Fuse = fuse > 0f ? Mathf.Min(fuse, weapon.Lifetime) : weapon.Lifetime,
                Weapon = index,
                Owner = owner
            };
        }

        public void Clear()
        {
            count = 0;
            beams.Clear();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
            projectiles = new Projectile[initialCapacity];
            mesh = BuildQuad();
        }

        // The tracer shader turns this unit quad to face the camera, so its own orientation does not matter.
        private static Mesh BuildQuad()
        {
            var quad = new Mesh { name = "Tracer Quad" };
            quad.SetVertices(new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f) });
            quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            quad.bounds = new Bounds(Vector3.zero, Vector3.one * 4f);
            return quad;
        }

        private void OnEnable() => SpaceAI.OriginShifted += OnOriginShifted;

        private void OnDisable() => SpaceAI.OriginShifted -= OnOriginShifted;

        private void OnOriginShifted(Vector3 offset)
        {
            for (int i = 0; i < count; i++) projectiles[i].Position -= offset;
            for (int i = 0; i < beams.Count; i++)
            {
                Beam beam = beams[i];
                beam.From -= offset;
                beam.To -= offset;
                beams[i] = beam;
            }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (commands.IsCreated) commands.Dispose();
            if (results.IsCreated) results.Dispose();
            foreach (Material material in materials) Destroy(material);
            if (mesh != null) Destroy(mesh);
        }

        private void FixedUpdate()
        {
            stopwatch.Restart();
            Step();
            LastUpdateMs = stopwatch.Elapsed.TotalMilliseconds;
        }

        private void Step()
        {
            if (count == 0) return;
            float dt = Time.fixedDeltaTime;
            EnsureNativeCapacity(count);

            var query = new QueryParameters(hitMask, false, QueryTriggerInteraction.Ignore, false);
            for (int i = 0; i < count; i++)
            {
                Projectile p = projectiles[i];
                float speed = p.Velocity.magnitude;
                float distance = speed * Mathf.Min(dt, p.Fuse);
                commands[i] = new RaycastCommand(p.Position, p.Velocity / Mathf.Max(speed, 0.001f), query, distance);
            }
            RaycastCommand.ScheduleBatch(commands.GetSubArray(0, count), results.GetSubArray(0, count * MaxHits), 32, MaxHits).Complete();

            ShipRegistry registry = SpaceAIWorld.Instance.Registry;
            MissileSystem missiles = MissileSystem.TryGet(out MissileSystem system) && system.Count > 0 ? system : null;
            int stepped = count;
            int alive = 0;
            for (int i = 0; i < stepped; i++)
            {
                Projectile p = projectiles[i];
                bool struck = TryGetHit(i, p.Owner, registry, out RaycastHit hit, out SpaceShip ship);
                if (missiles != null && missiles.Sweep(p.Position, p.Velocity, Mathf.Min(dt, p.Fuse), p.Owner, out int missileId, out float time)
                    && (!struck || time * p.Velocity.magnitude <= hit.distance))
                {
                    WeaponProfile weapon = weapons[p.Weapon];
                    if (weapon.Kind == WeaponKind.Flak) Detonate(weapon, p.Owner, p.Position + p.Velocity * time, registry);
                    else missiles.Damage(missileId, weapon.Damage, p.Owner);
                    continue;
                }
                if (struck)
                {
                    WeaponProfile weapon = weapons[p.Weapon];
                    if (weapon.Kind == WeaponKind.Flak) Detonate(weapon, p.Owner, hit.point, registry);
                    else Resolve(weapon, p.Owner, ship, hit.collider, hit.point, hit.normal, weapon.Damage, direction: p.Velocity.normalized);
                    continue;
                }

                p.Position += p.Velocity * Mathf.Min(dt, p.Fuse);
                p.Fuse -= dt;
                p.Age += dt;
                if (p.Fuse <= 0f)
                {
                    WeaponProfile weapon = weapons[p.Weapon];
                    if (weapon.Kind == WeaponKind.Flak) Detonate(weapon, p.Owner, p.Position, registry);
                    continue;
                }
                projectiles[alive++] = p;
            }

            // Hit handlers may fire new shots, which land past the stepped range.
            for (int i = stepped; i < count; i++) projectiles[alive++] = projectiles[i];
            count = alive;
        }

        private void Update()
        {
            for (int i = beams.Count - 1; i >= 0; i--)
            {
                Beam beam = beams[i];
                beam.Life -= Time.deltaTime;
                if (beam.Life <= 0f) beams.RemoveAt(i);
                else beams[i] = beam;
            }
            Render();
        }

        private bool TryGetHit(int index, SpaceShip owner, ShipRegistry registry, out RaycastHit hit, out SpaceShip ship)
        {
            for (int h = 0; h < MaxHits; h++)
            {
                hit = results[index * MaxHits + h];
                if (hit.colliderInstanceID == 0) break;
                registry.TryGetShipByCollider(hit.colliderInstanceID, out ship);
                if (ship != null && ship == owner) continue;
                return true;
            }
            hit = default;
            ship = null;
            return false;
        }

        private void FireLaser(WeaponProfile weapon, int index, SpaceShip owner, Vector3 origin, Vector3 direction)
        {
            ShipRegistry registry = SpaceAIWorld.Instance.Registry;
            int hits = Physics.RaycastNonAlloc(origin, direction, laserHits, weapon.Range, hitMask, QueryTriggerInteraction.Ignore);
            int nearest = -1;
            SpaceShip nearestShip = null;
            for (int i = 0; i < hits; i++)
            {
                registry.TryGetShipByCollider(laserHits[i].colliderInstanceID, out SpaceShip ship);
                if (ship != null && ship == owner) continue;
                if (nearest >= 0 && laserHits[i].distance >= laserHits[nearest].distance) continue;
                nearest = i;
                nearestShip = ship;
            }

            Vector3 end = origin + direction * weapon.Range;
            if (nearest >= 0)
            {
                RaycastHit hit = laserHits[nearest];
                end = hit.point;
                Resolve(weapon, owner, nearestShip, hit.collider, hit.point, hit.normal, weapon.Damage, direction: direction);
            }
            beams.Add(new Beam { From = origin, To = end, Life = weapon.BeamTime, Weapon = index });
        }

        private void Detonate(WeaponProfile weapon, SpaceShip owner, Vector3 point, ShipRegistry registry)
        {
            Detonated?.Invoke(weapon, point);
            registry.Query(point, weapon.BlastRadius + 50f, blastScratch);
            foreach (SpaceShip ship in blastScratch)
            {
                if (ship == owner) continue;
                Vector3 offset = ship.Position - point;
                float distance = Mathf.Max(0f, offset.magnitude - ship.Radius);
                if (distance > weapon.BlastRadius) continue;
                float damage = weapon.Damage * (1f - distance / Mathf.Max(weapon.BlastRadius, 0.01f));
                Resolve(weapon, owner, ship, null, point, -offset.normalized, damage, weapon.BlastRadius);
            }
            if (MissileSystem.TryGet(out MissileSystem missiles) && missiles.Count > 0)
                missiles.DamageInRadius(point, weapon.BlastRadius, weapon.Damage, owner);
        }

        private void Resolve(WeaponProfile weapon, SpaceShip owner, SpaceShip ship, Collider collider, Vector3 point, Vector3 normal, float damage, float blastRadius = 0f,
            Vector3 direction = default)
        {
            var info = new HitInfo
            {
                Source = owner,
                Ship = ship,
                Collider = collider,
                Weapon = weapon,
                Point = point,
                Normal = normal,
                Direction = direction,
                Damage = damage,
                BlastRadius = blastRadius
            };
            if (ship != null) ship.ReceiveHit(info);
            Impact?.Invoke(info);
        }

        private int IndexOf(WeaponProfile weapon)
        {
            if (weaponIndex.TryGetValue(weapon, out int index)) return index;
            index = weapons.Count;
            weapons.Add(weapon);
            weaponIndex.Add(weapon, index);

            Material source = tracerMaterial != null ? tracerMaterial : new Material(Shader.Find("SpaceAI2/Tracer"));
            var material = new Material(source) { color = weapon.TracerColor, enableInstancing = true };
            if (source != tracerMaterial) Destroy(source);
            materials.Add(material);
            return index;
        }

        private void EnsureNativeCapacity(int needed)
        {
            if (commands.IsCreated && commands.Length >= needed) return;
            int size = Mathf.NextPowerOfTwo(Mathf.Max(needed, 64));
            if (commands.IsCreated) commands.Dispose();
            if (results.IsCreated) results.Dispose();
            commands = new NativeArray<RaycastCommand>(size, Allocator.Persistent);
            results = new NativeArray<RaycastHit>(size * MaxHits, Allocator.Persistent);
        }

        private void Render()
        {
            // Ships render one physics step behind (interpolation), so tracers are drawn on the same timeline.
            float behind = Time.time - Time.fixedTime - Time.fixedDeltaTime;
            for (int w = 0; w < weapons.Count; w++)
            {
                WeaponProfile weapon = weapons[w];
                int batch = 0;
                for (int i = 0; i < count; i++)
                {
                    if (projectiles[i].Weapon != w) continue;
                    Vector3 velocity = projectiles[i].Velocity;
                    Vector3 head = projectiles[i].Position + velocity * behind;
                    Vector3 direction = velocity.normalized;
                    // A fresh tracer grows out of the muzzle instead of reaching back through the ship that fired it.
                    float length = Mathf.Min(weapon.TracerLength, weapon.MuzzleSpeed * (projectiles[i].Age + behind));
                    if (length <= 0.01f) continue;
                    matrices[batch++] = Matrix4x4.TRS(head - direction * (length * 0.5f), Quaternion.LookRotation(direction),
                        new Vector3(weapon.TracerWidth, weapon.TracerWidth, length));
                    if (batch == BatchSize) batch = Flush(w, batch);
                }

                foreach (Beam beam in beams)
                {
                    if (beam.Weapon != w) continue;
                    Vector3 span = beam.To - beam.From;
                    if (span.sqrMagnitude < 0.0001f) continue;
                    float width = weapon.TracerWidth * Mathf.Clamp01(beam.Life / weapon.BeamTime);
                    matrices[batch++] = Matrix4x4.TRS(beam.From + span * 0.5f, Quaternion.LookRotation(span), new Vector3(width, width, span.magnitude));
                    if (batch == BatchSize) batch = Flush(w, batch);
                }
                Flush(w, batch);
            }
        }

        private int Flush(int weapon, int batch)
        {
            if (batch > 0) Graphics.DrawMeshInstanced(mesh, 0, materials[weapon], matrices, batch, null, ShadowCastingMode.Off, false);
            return 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;
    }
}
