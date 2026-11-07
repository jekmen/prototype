using UnityEngine;

namespace SpaceAI2
{
    /// <summary>
    /// Engine exhaust on the ship's nozzles, scaled by throttle, and the ship's hit and death effects from
    /// <see cref="CombatEffects"/>. Added by <see cref="SpaceShip"/> when the scene has CombatEffects.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SpaceShip))]
    public class ShipEffects : MonoBehaviour
    {
        [Tooltip("Nozzle positions in the model's local space. The defaults fit SM_Ship_Fighter_05.")]
        [SerializeField] private Vector3[] nozzles = { new Vector3(-0.583f, 0.357f, -6.595f), new Vector3(0.583f, 0.357f, -6.595f) };
        [Tooltip("Exhaust length at idle, as a share of full throttle.")]
        [SerializeField, Range(0f, 1f)] private float idle = 0.35f;
        [SerializeField, Min(1f)] private float boost = 1.6f;

        private SpaceShip ship;
        private ShipHealth health;
        private CombatEffects effects;
        private ParticleSystem[] exhausts;
        private bool exhaustOn = true;
        private float baseLifetime, baseRate;

        private void Awake()
        {
            ship = GetComponent<SpaceShip>();
            health = ship.Health;
        }

        private void Start()
        {
            if (!CombatEffects.TryGet(out effects) || effects.EngineExhaust == null) return;
            var bank = GetComponent<ShipBank>();
            Transform model = bank != null && bank.Visual != null ? bank.Visual : transform;
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
            effects.Hit(hit.Point, hit.Normal, shield);
            // A hit that breaks through the shield also marks the hull.
            if (shield && hit.Damage > source.LastShieldDamage) effects.Hit(hit.Point, hit.Normal, false);
        }

        private void OnDestroyed(ShipHealth source, HitInfo hit)
        {
            if (effects != null)
                effects.Explosion(ship.Position, Mathf.Max(ship.Radius * 1.8f, 4f), true);
        }
    }
}
