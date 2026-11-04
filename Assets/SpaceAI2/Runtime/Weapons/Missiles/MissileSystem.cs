using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceAI2
{
    /// <summary>A missile as other systems see it: warnings, point defense, stats.</summary>
    public struct MissileTrack
    {
        public int Id;
        public MissileProfile Profile;
        public SpaceShip Owner;
        /// <summary>Null once the seeker lost the target or was decoyed.</summary>
        public SpaceShip Target;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Age;
    }

    /// <summary>
    /// Simulates missiles and torpedoes in plain arrays: motor, proportional navigation limited by the airframe's
    /// turn acceleration, proximity fuse and blast. Bodies and flames are drawn instanced, smoke trails come from a pool.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class MissileSystem : MonoBehaviour
    {
        private struct Missile
        {
            public MissileTrack Track;
            public int Profile;
            public float Hull;
            public Vector3 Acceleration;
            public TrailRenderer Trail;
        }

        private struct Flash
        {
            public Vector3 Position;
            public float Size;
            public float Life;
            public float Duration;
            public int Profile;
        }

        private struct ProfileVisual
        {
            public Material Body;
            public Material Flame;
        }

        private const int BatchSize = 1023;
        private const float MinSpeed = 25f;

        private static MissileSystem instance;

        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;
        [SerializeField] private Material trailMaterial;
        [SerializeField, Min(0f)] private float trailTime = 2.5f;

        private readonly List<MissileProfile> profiles = new List<MissileProfile>();
        private readonly Dictionary<MissileProfile, int> profileIndex = new Dictionary<MissileProfile, int>();
        private readonly List<ProfileVisual> visuals = new List<ProfileVisual>();
        private readonly List<Flash> flashes = new List<Flash>();
        private readonly List<SpaceShip> blastScratch = new List<SpaceShip>();
        private readonly Stack<TrailRenderer> trailPool = new Stack<TrailRenderer>();
        private readonly List<TrailRenderer> fadingTrails = new List<TrailRenderer>();
        private readonly RaycastHit[] hits = new RaycastHit[4];
        private readonly Matrix4x4[] matrices = new Matrix4x4[BatchSize];

        private Missile[] missiles = new Missile[64];
        private int count;
        private int nextId = 1;
        private Mesh bodyMesh;
        private Mesh flashMesh;

        public event Action<MissileTrack> Launched;
        /// <summary>The warhead went off at this point, whether or not anything was in the blast.</summary>
        public event Action<MissileTrack, Vector3> Detonated;
        /// <summary>Ran out of fuel or time, hit something before arming, or was shot down.</summary>
        public event Action<MissileTrack> Expired;
        public event Action<HitInfo> Impact;

        public static MissileSystem Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = FindObjectOfType<MissileSystem>();
                if (instance == null) instance = new GameObject(nameof(MissileSystem)).AddComponent<MissileSystem>();
                return instance;
            }
        }

        public static bool TryGet(out MissileSystem system)
        {
            system = instance;
            return system != null;
        }

        public int Count => count;

        public Material TrailMaterial
        {
            get => trailMaterial;
            set => trailMaterial = value;
        }

        public MissileTrack this[int index] => missiles[index].Track;

        public int Launch(MissileProfile profile, SpaceShip owner, Vector3 origin, Vector3 direction, SpaceShip target)
        {
            if (profile == null) return 0;
            if (count == missiles.Length) Array.Resize(ref missiles, count * 2);

            Vector3 velocity = (owner != null ? owner.Velocity : Vector3.zero) + direction.normalized * profile.EjectSpeed;
            var track = new MissileTrack
            {
                Id = nextId++,
                Profile = profile,
                Owner = owner,
                Target = profile.IsGuided ? target : null,
                Position = origin,
                Velocity = velocity
            };
            missiles[count++] = new Missile
            {
                Track = track,
                Profile = IndexOf(profile),
                Hull = profile.Hull,
                Trail = RentTrail(profile, origin)
            };
            Launched?.Invoke(track);
            return track.Id;
        }

        /// <summary>Guided missiles still tracking this ship.</summary>
        public void GetIncoming(SpaceShip ship, List<MissileTrack> result)
        {
            result.Clear();
            for (int i = 0; i < count; i++)
                if (missiles[i].Track.Target == ship) result.Add(missiles[i].Track);
        }

        public bool IsTracking(SpaceShip owner, SpaceShip target)
        {
            for (int i = 0; i < count; i++)
                if (missiles[i].Track.Owner == owner && missiles[i].Track.Target == target) return true;
            return false;
        }

        /// <summary>Point defense damage; returns true when the missile is destroyed.</summary>
        public bool Damage(int id, float damage)
        {
            for (int i = 0; i < count; i++)
            {
                if (missiles[i].Track.Id != id) continue;
                if (missiles[i].Hull <= 0f) return false;
                missiles[i].Hull -= damage;
                if (missiles[i].Hull > 0f) return false;
                AddFlash(missiles[i].Track.Position, profiles[missiles[i].Profile].Width * 8f, 0.25f, missiles[i].Profile);
                return true;
            }
            return false;
        }

        /// <summary>A flare from this ship: heat seekers tracking it with the flare in view may lose it.</summary>
        public int Decoy(SpaceShip ship, Vector3 flarePosition)
        {
            int decoyed = 0;
            for (int i = 0; i < count; i++)
            {
                ref MissileTrack track = ref missiles[i].Track;
                if (track.Target != ship || track.Profile.Guidance != MissileGuidance.Heat) continue;
                Vector3 toFlare = flarePosition - track.Position;
                if (Vector3.Angle(track.Velocity, toFlare) > track.Profile.SeekerAngle) continue;
                if (UnityEngine.Random.value >= track.Profile.FlareSusceptibility) continue;
                track.Target = null;
                decoyed++;
            }
            return decoyed;
        }

        public void Clear()
        {
            for (int i = 0; i < count; i++) ReleaseTrail(missiles[i].Trail);
            count = 0;
            flashes.Clear();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
            bodyMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            flashMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            foreach (ProfileVisual visual in visuals)
            {
                Destroy(visual.Body);
                Destroy(visual.Flame);
            }
        }

        private void FixedUpdate()
        {
            if (count == 0) return;
            float dt = Time.fixedDeltaTime;
            ShipRegistry registry = SpaceAIWorld.Instance.Registry;

            int stepped = count;
            int alive = 0;
            for (int i = 0; i < stepped; i++)
            {
                Missile m = missiles[i];
                MissileProfile profile = profiles[m.Profile];
                if (m.Hull <= 0f && profile.Hull > 0f)
                {
                    End(m, false, m.Track.Position);
                    continue;
                }
                if (Step(ref m, profile, dt, registry)) missiles[alive++] = m;
            }

            // Hit handlers may launch new missiles, which land past the stepped range.
            for (int i = stepped; i < count; i++) missiles[alive++] = missiles[i];
            count = alive;
        }

        /// <summary>Advances one missile; returns false when it is gone.</summary>
        private bool Step(ref Missile m, MissileProfile profile, float dt, ShipRegistry registry)
        {
            ref MissileTrack t = ref m.Track;
            t.Age += dt;
            bool armed = t.Age >= profile.ArmTime;

            if (t.Target != null && !t.Target.gameObject.activeInHierarchy) t.Target = null;
            if (armed && t.Target != null && Vector3.Angle(t.Velocity, t.Target.Position - t.Position) > profile.SeekerAngle)
                t.Target = null;

            float speed = t.Velocity.magnitude;
            Vector3 heading = t.Velocity / Mathf.Max(speed, 0.001f);
            Vector3 command = Vector3.zero;
            if (armed && t.Target != null)
            {
                // Pure proportional navigation: turn the flight path at N times the line-of-sight rate.
                Vector3 range = t.Target.Position - t.Position;
                Vector3 relative = t.Target.Velocity - t.Velocity;
                Vector3 losRate = Vector3.Cross(range, relative) / Mathf.Max(range.sqrMagnitude, 1f);
                command = Vector3.ClampMagnitude(profile.Navigation * Vector3.Cross(losRate, t.Velocity), profile.MaxTurnAcceleration);
            }
            m.Acceleration = profile.GuidanceLag > 0f
                ? Vector3.Lerp(m.Acceleration, command, Mathf.Clamp01(dt / profile.GuidanceLag))
                : command;
            Vector3 accel = Vector3.ProjectOnPlane(m.Acceleration, heading);

            if (t.Age < profile.BurnTime) speed = Mathf.Min(profile.MaxSpeed, speed + profile.Acceleration * dt);
            else speed *= Mathf.Max(0f, 1f - profile.Drag * dt);
            speed *= Mathf.Max(0f, 1f - profile.TurnDrag * accel.magnitude * dt);
            heading = (heading * Mathf.Max(speed, 1f) + accel * dt).normalized;
            Vector3 velocity = heading * speed;

            Vector3 start = t.Position;
            Vector3 step = velocity * dt;

            // Proximity fuse: closest approach to the target during this step.
            if (armed && t.Target != null)
            {
                Vector3 offset = t.Target.Position - start;
                Vector3 closing = t.Target.Velocity - velocity;
                float when = Mathf.Clamp(-Vector3.Dot(offset, closing) / Mathf.Max(closing.sqrMagnitude, 0.001f), 0f, dt);
                Vector3 miss = offset + closing * when;
                if (miss.magnitude - t.Target.Radius <= profile.ProximityFuse)
                {
                    t.Velocity = velocity;
                    End(m, true, start + velocity * when);
                    return false;
                }
            }

            if (TryHit(start, step, t.Owner, registry, out RaycastHit hit))
            {
                t.Velocity = velocity;
                End(m, armed, hit.point);
                return false;
            }

            t.Position = start + step;
            t.Velocity = velocity;
            if (t.Age >= profile.Lifetime || t.Age >= profile.BurnTime && speed < MinSpeed)
            {
                End(m, false, t.Position);
                return false;
            }
            return true;
        }

        private bool TryHit(Vector3 start, Vector3 step, SpaceShip owner, ShipRegistry registry, out RaycastHit nearest)
        {
            nearest = default;
            float length = step.magnitude;
            if (length < 0.0001f) return false;
            int found = Physics.RaycastNonAlloc(start, step / length, hits, length, hitMask, QueryTriggerInteraction.Ignore);
            bool any = false;
            for (int i = 0; i < found; i++)
            {
                registry.TryGetShipByCollider(hits[i].colliderInstanceID, out SpaceShip ship);
                if (ship != null && ship == owner) continue;
                if (any && hits[i].distance >= nearest.distance) continue;
                nearest = hits[i];
                any = true;
            }
            return any;
        }

        private void End(Missile m, bool detonate, Vector3 point)
        {
            ReleaseTrail(m.Trail);
            MissileProfile profile = profiles[m.Profile];
            if (!detonate)
            {
                AddFlash(point, profile.Width * 6f, 0.2f, m.Profile);
                Expired?.Invoke(m.Track);
                return;
            }

            AddFlash(point, Mathf.Max(profile.BlastRadius, 3f), 0.45f, m.Profile);
            ShipRegistry registry = SpaceAIWorld.Instance.Registry;
            registry.Query(point, profile.BlastRadius + 50f, blastScratch);
            foreach (SpaceShip ship in blastScratch)
            {
                if (ship == m.Track.Owner) continue;
                Vector3 offset = ship.Position - point;
                float distance = Mathf.Max(0f, offset.magnitude - ship.Radius);
                if (distance > profile.BlastRadius) continue;
                float damage = profile.Damage * (1f - distance / Mathf.Max(profile.BlastRadius, 0.01f));
                var info = new HitInfo
                {
                    Source = m.Track.Owner,
                    Ship = ship,
                    Missile = profile,
                    Point = point,
                    Normal = -offset.normalized,
                    Damage = damage
                };
                ship.ReceiveHit(info);
                Impact?.Invoke(info);
            }
            Detonated?.Invoke(m.Track, point);
        }

        private void Update()
        {
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                Flash flash = flashes[i];
                flash.Life -= Time.deltaTime;
                if (flash.Life <= 0f) flashes.RemoveAt(i);
                else flashes[i] = flash;
            }
            for (int i = fadingTrails.Count - 1; i >= 0; i--)
            {
                TrailRenderer trail = fadingTrails[i];
                if (trail.positionCount > 0) continue;
                fadingTrails.RemoveAt(i);
                trail.gameObject.SetActive(false);
                trailPool.Push(trail);
            }
            Render();
        }

        private void Render()
        {
            // Ships render one physics step behind (interpolation), so missiles are drawn on the same timeline.
            float behind = Time.time - Time.fixedTime - Time.fixedDeltaTime;
            for (int p = 0; p < profiles.Count; p++)
            {
                MissileProfile profile = profiles[p];
                int batch = 0;
                for (int i = 0; i < count; i++)
                {
                    if (missiles[i].Profile != p) continue;
                    MissileTrack t = missiles[i].Track;
                    Vector3 position = t.Position + t.Velocity * behind;
                    Quaternion rotation = Quaternion.LookRotation(t.Velocity.sqrMagnitude > 0.01f ? t.Velocity : Vector3.forward);
                    matrices[batch++] = Matrix4x4.TRS(position, rotation, new Vector3(profile.Width, profile.Width, profile.Length));
                    if (missiles[i].Trail != null) missiles[i].Trail.transform.position = position - rotation * Vector3.forward * (profile.Length * 0.5f);
                    if (batch == BatchSize) batch = Flush(bodyMesh, visuals[p].Body, batch);
                }
                Flush(bodyMesh, visuals[p].Body, batch);

                for (int i = 0; i < count; i++)
                {
                    if (missiles[i].Profile != p || missiles[i].Track.Age >= profile.BurnTime) continue;
                    MissileTrack t = missiles[i].Track;
                    Vector3 direction = t.Velocity.sqrMagnitude > 0.01f ? t.Velocity.normalized : Vector3.forward;
                    Vector3 position = t.Position + t.Velocity * behind - direction * (profile.Length * 0.75f);
                    float flicker = profile.Width * UnityEngine.Random.Range(1.4f, 2f);
                    matrices[batch++] = Matrix4x4.TRS(position, Quaternion.LookRotation(direction), new Vector3(flicker, flicker, profile.Length * 0.6f));
                    if (batch == BatchSize) batch = Flush(flashMesh, visuals[p].Flame, batch);
                }
                foreach (Flash flash in flashes)
                {
                    if (flash.Profile != p) continue;
                    float size = flash.Size * Mathf.Lerp(1f, 0.3f, flash.Life / flash.Duration);
                    matrices[batch++] = Matrix4x4.TRS(flash.Position, Quaternion.identity, Vector3.one * size);
                    if (batch == BatchSize) batch = Flush(flashMesh, visuals[p].Flame, batch);
                }
                Flush(flashMesh, visuals[p].Flame, batch);
            }
        }

        private int Flush(Mesh mesh, Material material, int batch)
        {
            if (batch > 0) Graphics.DrawMeshInstanced(mesh, 0, material, matrices, batch, null, ShadowCastingMode.Off, false);
            return 0;
        }

        private void AddFlash(Vector3 position, float size, float duration, int profile) =>
            flashes.Add(new Flash { Position = position, Size = size, Life = duration, Duration = duration, Profile = profile });

        private int IndexOf(MissileProfile profile)
        {
            if (profileIndex.TryGetValue(profile, out int index)) return index;
            index = profiles.Count;
            profiles.Add(profile);
            profileIndex.Add(profile, index);
            visuals.Add(new ProfileVisual
            {
                Body = new Material(Shader.Find("Standard")) { color = profile.BodyColor, enableInstancing = true },
                Flame = new Material(Shader.Find("SpaceAI2/Tracer")) { color = profile.FlameColor * 3f, enableInstancing = true }
            });
            return index;
        }

        private TrailRenderer RentTrail(MissileProfile profile, Vector3 position)
        {
            if (trailTime <= 0f) return null;
            TrailRenderer trail;
            if (trailPool.Count > 0) trail = trailPool.Pop();
            else
            {
                trail = new GameObject("MissileTrail").AddComponent<TrailRenderer>();
                trail.transform.SetParent(transform, false);
                if (trailMaterial == null) trailMaterial = new Material(Shader.Find("Sprites/Default"));
                trail.sharedMaterial = trailMaterial;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.minVertexDistance = 2f;
            }
            trail.transform.position = position;
            trail.Clear();
            trail.time = trailTime;
            trail.widthMultiplier = profile.Width * 3f;
            trail.widthCurve = AnimationCurve.Linear(0f, 0.4f, 1f, 1.5f);
            Color end = profile.TrailColor;
            end.a = 0f;
            trail.startColor = profile.TrailColor;
            trail.endColor = end;
            trail.emitting = true;
            trail.gameObject.SetActive(true);
            return trail;
        }

        private void ReleaseTrail(TrailRenderer trail)
        {
            if (trail == null) return;
            trail.emitting = false;
            fadingTrails.Add(trail);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;
    }
}
