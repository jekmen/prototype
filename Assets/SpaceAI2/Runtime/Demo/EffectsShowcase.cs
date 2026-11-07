using UnityEngine;

namespace SpaceAI2.Demo
{
    public class EffectsShowcase : MonoBehaviour
    {
        public SpaceShip Ship;
        public WeaponProfile Laser;
        public MissileProfile Missile;
        [Range(0, 5)] public int Mode = 1;
        public bool AutoCycle = true;
        private float nextEffect, nextMode;
        private GUIStyle title, caption, small;
        private readonly string[] names = { "ENGINES", "SHIELD HIT", "PULSE LASER", "GUIDED MISSILE", "EXPLOSION", "HULL IMPACT" };
        private readonly string[] descriptions = {
            "Twin jets / throttle and afterburner", "Shield flash and sparks",
            "Muzzle flash / beam / impact", "Exhaust trail / detonation",
            "Fire / smoke / embers / fragments", "Sparks / smoke / armor flash" };
        private void Start()
        {
            Ship.Motor.enabled = false;
            Rigidbody body = Ship.Motor.Body;
            if (!body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            nextMode = Time.time + 7;
        }
        private void Update()
        {
            for (int i = 0; i < 6; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { Mode = i; AutoCycle = false; nextEffect = 0; }
            if (Input.GetKeyDown(KeyCode.Space)) AutoCycle = !AutoCycle;
            if (AutoCycle && Time.time >= nextMode) { Mode = (Mode + 1) % 6; nextMode = Time.time + 7; nextEffect = 0; }
            if (!Ship.gameObject.activeSelf) { Ship.Health.Restore(); Ship.gameObject.SetActive(true); }
            Ship.Motor.Input = new ShipInput { Throttle = 0.7f, Boost = Mode == 0 && Mathf.Sin(Time.time * 1.3f) > 0 };
            if (Time.time < nextEffect) return;
            nextEffect = Time.time + (Mode == 4 ? 4.2f : Mode == 3 ? 3.5f : 0.8f);
            Trigger();
        }
        public void Trigger()
        {
            Ship.Health.Restore();
            Vector3 point = Ship.transform.TransformPoint(-3f, 0.8f, -3f);
            if (Mode == 1) Ship.ReceiveHit(new HitInfo { Ship = Ship, Point = point, Normal = Vector3.back, Damage = 8 });
            if (Mode == 2)
            {
                Vector3 origin = Ship.Position + new Vector3(-24, 3, -16);
                ProjectileSystem.Instance.Fire(Laser, null, origin, (Ship.Position - origin).normalized);
            }
            if (Mode == 3)
            {
                Vector3 origin = Ship.Position + new Vector3(-35, 8, 25);
                MissileSystem.Instance.Launch(Missile, null, origin, (Ship.Position - origin).normalized, Ship);
            }
            if (Mode == 4 && CombatEffects.TryGet(out CombatEffects effects)) effects.Explosion(Ship.Position + new Vector3(15, 0, 0), 7, true);
            if (Mode == 5)
            {
                // One hit crosses the shield boundary; the next demonstrates bare armor.
                Ship.ReceiveHit(new HitInfo { Ship = Ship, Point = point, Normal = Vector3.back, Damage = Ship.Health.MaxShield });
                Ship.ReceiveHit(new HitInfo { Ship = Ship, Point = point + Vector3.right, Normal = Vector3.back, Damage = 5 });
            }
        }
        private void LateUpdate()
        {
            Camera camera = Camera.main;
            float angle = Mathf.Sin(Time.time * 0.12f) * 0.2f;
            Vector3 offset = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0) * new Vector3(20, 10, Mode == 4 ? -36 : -28);
            camera.transform.position = Ship.Position + offset;
            camera.transform.LookAt(Ship.Position + new Vector3(Mode == 4 ? 7 : 1, 0, 0));
        }
        private void OnGUI()
        {
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.8f, 0.92f, 1) } };
                caption = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = new Color(0.5f, 0.73f, 0.85f) } };
                small = new GUIStyle(caption) { fontSize = 11 };
            }
            GUI.Label(new Rect(32, 24, 600, 22), "S P A C E A I   /   V I S U A L   E F F E C T S", small);
            GUI.Label(new Rect(30, 48, 700, 50), names[Mode], title);
            GUI.Label(new Rect(32, 98, 800, 25), descriptions[Mode], caption);
            GUI.Label(new Rect(32, Screen.height - 58, 1000, 25), "1  Engines     2  Shield     3  Laser     4  Missile     5  Explosion     6  Hull", caption);
            GUI.Label(new Rect(32, Screen.height - 32, 1000, 22), "SPACE  Auto-cycle: " + (AutoCycle ? "ON" : "OFF") + "     /     Plain particle system prefabs in Content/Effects", small);
        }
    }
}
