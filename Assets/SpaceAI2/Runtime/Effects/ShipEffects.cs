using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceAI2
{
    /// <summary>
    /// Engine exhaust on the ship's nozzles, scaled by throttle, the shield bubble that ripples out from shield
    /// hits, and the ship's hit and death effects from <see cref="CombatEffects"/>. Added by <see cref="SpaceShip"/> when the scene has CombatEffects.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SpaceShip))]
    public class ShipEffects : MonoBehaviour
    {
        [Tooltip("Nozzle positions in the model's local space. The defaults fit SM_Ship_Fighter_05.")]
        [SerializeField] private Vector3[] nozzles = { new Vector3(-0.583f, 0.357f, -6.595f), new Vector3(0.583f, 0.357f, -6.595f) };
        [Tooltip("Exhaust length at idle, as a share of full throttle.")]
        [SerializeField, Range(0f, 1f)] private float idle = 0.35f;
        [SerializeField, Min(1f)] private float boost = 1.6f;
        [Tooltip("Size of the shield ellipsoid in the model's local space. The default fits SM_Ship_Fighter_05.")]
        [SerializeField] private Vector3 shieldSize = new Vector3(19f, 5f, 16.5f);

        private const float ShieldFade = 1.25f;
        private static readonly int HitsId = Shader.PropertyToID("_Hits");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color[] ShieldColors = { new Color(0.08f, 0.5f, 1f), new Color(1f, 0.18f, 0.08f) };

        private SpaceShip ship;
        private ShipHealth health;
        private CombatEffects effects;
        private ParticleSystem[] exhausts;
        private bool exhaustOn = true;
        private float baseLifetime, baseRate;
        private Transform model;
        // xyz: hit direction on the unit shield, w: hit time. The shader animates the last four.
        private readonly Vector4[] shieldHits = new Vector4[4];
        private int shieldHitIndex;
        private float lastShieldHit = -100f;
        private MaterialPropertyBlock shieldProperties;

        private void Awake()
        {
            ship = GetComponent<SpaceShip>();
            health = ship.Health;
        }

        private void Start()
        {
            if (!CombatEffects.TryGet(out effects)) return;
            var bank = GetComponent<ShipBank>();
            model = bank != null && bank.Visual != null ? bank.Visual : transform;
            if (effects.EngineExhaust == null) return;
            ParticleSystem prefab = effects.EngineExhaust;
            exhausts = new ParticleSystem[nozzles.Length];
            for (int i = 0; i < nozzles.Length; i++)
            {
                // The prefab emits along its +Z, so it faces backwards out of the nozzle.
                exhausts[i] = Instantiate(prefab, model.TransformPoint(nozzles[i]), model.rotation * Quaternion.Euler(0f, 180f, 0f), model);
                exhausts[i].name = prefab.name;
            }
            baseLifetime = prefab.main.startLifetimeMultiplier;
            baseRate = prefab.emission.rateOverTimeMultiplier;
            // Test scenes give ships a debug trail from the hull centre; the exhaust replaces it.
            if (TryGetComponent(out TrailRenderer trail)) trail.enabled = false;
        }

        private void OnEnable()
        {
            if (health == null) return;
            lastShieldHit = -100f;
            for (int i = 0; i < shieldHits.Length; i++) shieldHits[i] = new Vector4(0f, 0f, 1f, -100f);
            health.Damaged += OnDamaged;
            health.Destroyed += OnDestroyed;
        }

        private void OnDisable()
        {
            if (health == null) return;
            health.Damaged -= OnDamaged;
            health.Destroyed -= OnDestroyed;
        }

        private void LateUpdate()
        {
            DrawShield();
            if (exhausts == null) return;
            // Far ships stop simulating their exhaust: in big battles it is most of the particles on screen.
            bool on = effects.IsNear(ship.Position);
            if (on != exhaustOn)
            {
                exhaustOn = on;
                foreach (ParticleSystem exhaust in exhausts) exhaust.gameObject.SetActive(on);
            }
            if (!on) return;
            ShipInput input = ship.Motor.Input;
            float thrust = input.Boost ? boost : Mathf.Lerp(idle, 1f, (input.Throttle + 1f) * 0.5f);
            thrust *= ship.Motor.Power;
            foreach (ParticleSystem exhaust in exhausts)
            {
                ParticleSystem.MainModule main = exhaust.main;
                main.startLifetimeMultiplier = baseLifetime * thrust;
                ParticleSystem.EmissionModule emission = exhaust.emission;
                emission.rateOverTimeMultiplier = thrust > 0.01f ? baseRate : 0f;
            }
        }

        private void OnDamaged(ShipHealth source, HitInfo hit)
        {
            if (effects == null) return;
            bool shield = source.LastShieldDamage > 0f;
            if (shield && model != null) AddShieldHit(hit.Point);
            effects.Hit(hit.Point, hit.Normal, shield);
            // A hit that breaks through the shield also marks the hull.
            if (shield && hit.Damage > source.LastShieldDamage) effects.Hit(hit.Point, hit.Normal, false);
        }

        private void AddShieldHit(Vector3 point)
        {
            Vector3 local = model.InverseTransformPoint(point);
            local = new Vector3(local.x / shieldSize.x, local.y / shieldSize.y, local.z / shieldSize.z).normalized;
            if (local.sqrMagnitude < 0.01f) local = Vector3.forward;
            shieldHits[shieldHitIndex++ % shieldHits.Length] = new Vector4(local.x, local.y, local.z, Time.time);
            lastShieldHit = Time.time;
        }

        private void DrawShield()
        {
            if (Time.time - lastShieldHit > ShieldFade || effects == null || effects.ShieldMaterial == null ||
                !effects.IsNear(ship.Position)) return;
            shieldProperties ??= new MaterialPropertyBlock();
            shieldProperties.SetVectorArray(HitsId, shieldHits);
            shieldProperties.SetColor(ColorId, ShieldColors[ship.Team == 0 ? 0 : 1]);
            Graphics.DrawMesh(effects.ShieldMesh, Matrix4x4.TRS(model.position, model.rotation, shieldSize), effects.ShieldMaterial,
                gameObject.layer, null, 0, shieldProperties, ShadowCastingMode.Off, false);
        }

        private void OnDestroyed(ShipHealth source, HitInfo hit)
        {
            if (effects != null)
                effects.Explosion(ship.Position, Mathf.Max(ship.Radius * 1.8f, 4f), true);
        }
    }
}
