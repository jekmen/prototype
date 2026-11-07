using System;
using UnityEngine;

namespace SpaceAI2.Demo
{
    public class GunneryDemo : MonoBehaviour
    {
        [Serializable]
        private class Duel
        {
            public string Label;
            public AIPilot Shooter;
            public SpaceShip Target;
            [NonSerialized] public ShipGun Gun;
            [NonSerialized] public int Shots;
            [NonSerialized] public int Hits;
        }

        [SerializeField] private Duel[] duels;
        [SerializeField] private WeaponProfile[] weapons;
        [SerializeField] private ChaseCamera chaseCamera;
        [SerializeField] private FlightHud hud;

        private int weapon;
        private int view;
        private float elapsed;
        private GUIStyle style;

        public void Setup(string[] labels, AIPilot[] shooters, SpaceShip[] targets, WeaponProfile[] weaponSet, ChaseCamera cameraRig, FlightHud flightHud)
        {
            duels = new Duel[shooters.Length];
            for (int i = 0; i < duels.Length; i++) duels[i] = new Duel { Label = labels[i], Shooter = shooters[i], Target = targets[i] };
            weapons = weaponSet;
            chaseCamera = cameraRig;
            hud = flightHud;
        }

        private void Start()
        {
            foreach (Duel duel in duels)
            {
                Duel d = duel;
                d.Gun = d.Shooter.GetComponent<ShipGun>();
                d.Gun.Fired += _ => d.Shots++;
                d.Target.Hit += hit =>
                {
                    if (hit.Source == d.Shooter.Ship) d.Hits++;
                };
                d.Shooter.Pursue(d.Target, PursuitMode.Pure);
            }
            SetWeapon(0);
            ShowView(0);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab)) ShowView((view + 1) % (duels.Length * 2));
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) ResetStats();
            for (int i = 0; i < weapons.Length && i < 9; i++)
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1 + i)) SetWeapon(i);
        }

        private void SetWeapon(int index)
        {
            weapon = index;
            foreach (Duel duel in duels) duel.Gun.Weapon = weapons[weapon];
            if (ProjectileSystem.TryGet(out ProjectileSystem system)) system.Clear();
            ResetStats();
        }

        private void ShowView(int index)
        {
            view = index;
            Duel duel = duels[view / 2];
            SpaceShip ship = view % 2 == 0 ? duel.Shooter.Ship : duel.Target;
            chaseCamera.Target = ship.transform;
            hud.Bind(ship.Motor, null);
        }

        private void ResetStats()
        {
            elapsed = 0f;
            foreach (Duel duel in duels)
            {
                duel.Shots = 0;
                duel.Hits = 0;
            }
        }

        private void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };
            WeaponProfile current = weapons[weapon];
            string text = $"Weapon {current.name} ({current.Kind}) · {elapsed:0} s\n";
            foreach (Duel duel in duels)
            {
                float distance = Vector3.Distance(duel.Shooter.transform.position, duel.Target.Position);
                float accuracy = duel.Shots > 0 ? 100f * duel.Hits / duel.Shots : 0f;
                text += $"{duel.Label}: aim error {duel.Shooter.AimError:0.00}°, gimbal {duel.Gun.GimbalAngle:0}°, " +
                        $"distance {distance:0} m, shots {duel.Shots}, hits {duel.Hits}, accuracy {accuracy:0}%\n";
                string aim = duel.Shooter.GunAimAngle >= 0f ? $"{duel.Shooter.GunAimAngle:0.0}°" : "no solution";
                text += $"    weapon {(duel.Gun.Weapon != null ? duel.Gun.Weapon.name : "none")}, " +
                        $"order {(duel.Shooter.Target != null ? "pursue" : "none")}, aim off {aim}, trigger {(duel.Gun.Trigger ? "on" : "off")}\n";
            }

            string viewed = view % 2 == 0 ? "shooter" : "target";
            text += $"View: {duels[view / 2].Label} {viewed}\n";
            text += "1-" + weapons.Length + " weapon · Tab switch view · R reset stats";
            GUI.Label(new Rect(16, Screen.height - 170, 1100, 160), text, style);
        }
    }
}
