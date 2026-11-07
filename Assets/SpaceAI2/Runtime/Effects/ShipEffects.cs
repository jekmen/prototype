using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceAI2
{
    [DisallowMultipleComponent, RequireComponent(typeof(SpaceShip))]
    public class ShipEffects : MonoBehaviour
    {
        [Tooltip("Nozzle positions in the visual model's local space.")]
        [SerializeField] private Vector3[] nozzles = { new Vector3(-0.583f, 0.357f, -6.595f), new Vector3(0.583f, 0.357f, -6.595f) };
        [SerializeField, Min(0.01f)] private float nozzleWidth = 0.32f;
        [SerializeField] private Vector3 shieldSize = new Vector3(19f, 5f, 16.5f);
        private readonly Vector4[] hits = new Vector4[4];
        private MaterialPropertyBlock properties;
        private SpaceShip ship;
        private ShipHealth health;
        private Transform visual;
        private int hitIndex;
        private float lastImpact = -100f, nextMote;
        private bool initialized;

        private void Start()
        {
            ship = GetComponent<SpaceShip>();
            health = ship.Health;
            var bank = GetComponent<ShipBank>();
            visual = bank != null && bank.Visual != null ? bank.Visual : transform;
            properties = new MaterialPropertyBlock();
            var legacyTrail = GetComponent<TrailRenderer>();
            if (legacyTrail != null) { legacyTrail.emitting = false; legacyTrail.Clear(); legacyTrail.enabled = false; }
            for (int i = 0; i < hits.Length; i++) hits[i] = new Vector4(0, 0, 1, -100);
            initialized = true;
            Subscribe();
        }
        private void OnEnable() { if (initialized) Subscribe(); }
        private void Subscribe()
        {
            lastImpact = -100f;
            for (int i = 0; i < hits.Length; i++) hits[i].w = -100;
            SpaceEffects.Instance.Register(this);
            if (health != null) { health.Damaged += Damaged; health.Destroyed += Destroyed; }
        }
        private void OnDisable()
        {
            if (SpaceEffects.TryGet(out SpaceEffects effects)) effects.Unregister(this);
            if (health != null) { health.Damaged -= Damaged; health.Destroyed -= Destroyed; }
        }
        private void Damaged(ShipHealth source, HitInfo hit)
        {
            bool shield = source.LastShieldDamage > 0;
            if (shield)
            {
                Vector3 local = visual.InverseTransformPoint(hit.Point);
                local = new Vector3(local.x / shieldSize.x, local.y / shieldSize.y, local.z / shieldSize.z).normalized;
                if (local.sqrMagnitude < 0.01f) local = Vector3.forward;
                hits[hitIndex++ % hits.Length] = new Vector4(local.x, local.y, local.z, Time.time);
                lastImpact = Time.time;
            }
            SpaceEffects.Instance.Hit(hit.Point, hit.Normal, shield);
            if (shield && hit.Damage > source.LastShieldDamage) SpaceEffects.Instance.Hit(hit.Point, hit.Normal, false);
        }
        private void Destroyed(ShipHealth source, HitInfo hit) => SpaceEffects.Instance.Explosion(ship.Position, Mathf.Max(ship.Radius * 1.8f, 4), ship.Velocity, true);

        internal void Render(SpaceEffects effects, Camera camera)
        {
            float throttle = ship.Motor.Power * (ship.Motor.Input.Boost ? 1.7f : Mathf.Lerp(0.45f, 1, (ship.Motor.Input.Throttle + 1) * 0.5f));
            float length = 4.5f * throttle * (1 + 0.07f * Mathf.Sin(Time.time * 47 + GetInstanceID()));
            if (throttle > 0.01f)
                foreach (Vector3 nozzle in nozzles)
                {
                    Vector3 point = visual.TransformPoint(nozzle);
                    effects.Engine(Matrix4x4.TRS(point - visual.forward * length * 0.5f, Quaternion.LookRotation(-visual.forward, visual.up),
                        new Vector3(nozzleWidth, nozzleWidth, length)));
                    if (Time.time >= nextMote && camera != null && (camera.transform.position - point).sqrMagnitude < 90000)
                        effects.EngineMote(point - visual.forward * length * 0.3f, ship.Velocity - visual.forward * 22, nozzleWidth * 1.5f);
                }
            if (Time.time >= nextMote) nextMote = Time.time + 0.04f;
            float pulse = Mathf.Clamp01(1 - (Time.time - lastImpact) / 1.25f);
            if (health == null || pulse <= 0) return;
            properties.SetVectorArray("_Hits", hits);
            properties.SetColor("_Color", ship.Team == 0 ? new Color(0.08f, 0.5f, 1f) : new Color(1f, 0.18f, 0.08f));
            Graphics.DrawMesh(effects.Sphere, Matrix4x4.TRS(visual.position, visual.rotation, shieldSize), effects.ShieldMaterial,
                gameObject.layer, null, 0, properties, ShadowCastingMode.Off, false);
        }
    }
}
