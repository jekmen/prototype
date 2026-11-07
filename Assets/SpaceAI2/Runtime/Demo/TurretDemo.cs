using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI2.Demo
{
    /// <summary>
    /// A cruiser with point defense, flak and a main gun under missile, torpedo and fighter attacks. Counts how many
    /// missiles its defenses shoot down against how many hit, and how its turrets do against fighters. Fighters come
    /// either head-on or in attack runs from the turrets' dead zones; every time they destroy the subsystems their
    /// priority picks, the cruiser is repaired and the pass is counted, with the losses it took.
    /// </summary>
    public class TurretDemo : MonoBehaviour
    {
        private enum Mode { Torpedoes, TorpedoSalvo, Missiles, Swarm, Fighters, AttackRuns }

        private const float RespawnDelay = 3f;

        [SerializeField] private SpaceShip cruiser;
        [SerializeField] private MissileProfile torpedo;
        [SerializeField] private MissileProfile heat;
        [SerializeField] private MissileProfile swarm;
        [SerializeField] private SpaceShip[] fighters;
        [SerializeField, Min(100f)] private float torpedoDistance = 2500f;
        [SerializeField, Min(100f)] private float missileDistance = 1200f;
        [SerializeField, Min(100f)] private float swarmDistance = 900f;
        [SerializeField, Min(1)] private int torpedoSalvo = 6;
        [SerializeField, Min(100f)] private float fighterSpawnDistance = 2500f;
        [SerializeField, Min(0f)] private float launchInterval = 2f;
        [SerializeField] private ChaseCamera chaseCamera;
        [SerializeField] private FlightHud hud;

        private readonly HashSet<int> inFlight = new HashSet<int>();
        private readonly Dictionary<WeaponProfile, Vector2Int> gunnery = new Dictionary<WeaponProfile, Vector2Int>();
        private readonly List<(SpaceShip ship, float time)> respawns = new List<(SpaceShip, float)>();
        private ShipTurret[] turrets;
        private AIPilot pilot;
        private Collider hull;
        private Mode mode;
        private bool defense = true;
        private int view;
        private float cooldown;
        private float elapsed;
        private int launched, shotDown, hits, kills, subsystemsLost;
        private TargetPriority priority = TargetPriority.PointDefense;
        private int cycles, cycleKills;
        private float cycleStart, cycleTime;
        private GUIStyle style;

        public void Setup(SpaceShip cruiserShip, MissileProfile torpedoProfile, MissileProfile heatProfile, MissileProfile swarmProfile,
            SpaceShip[] attackers, ChaseCamera cameraRig, FlightHud flightHud)
        {
            cruiser = cruiserShip;
            torpedo = torpedoProfile;
            heat = heatProfile;
            swarm = swarmProfile;
            fighters = attackers;
            chaseCamera = cameraRig;
            hud = flightHud;
        }

        private void Start()
        {
            turrets = cruiser.GetComponentsInChildren<ShipTurret>();
            pilot = cruiser.GetComponent<AIPilot>();
            hull = cruiser.GetComponent<Collider>();
            foreach (ShipTurret turret in turrets) turret.Fired += OnTurretFired;
            foreach (Subsystem subsystem in cruiser.Subsystems) subsystem.Destroyed += OnSubsystemDestroyed;
            foreach (SpaceShip fighter in fighters)
                fighter.GetComponent<ShipHealth>().Destroyed += (health, _) => OnFighterDestroyed(health.GetComponent<SpaceShip>());

            MissileSystem missiles = MissileSystem.Instance;
            missiles.Detonated += OnDetonated;
            missiles.ShotDown += OnShotDown;
            missiles.Expired += track => inFlight.Remove(track.Id);
            ProjectileSystem.Instance.Impact += OnImpact;
            SetMode(Mode.Torpedoes);
            ShowView(0);
        }

        private void OnDestroy()
        {
            if (MissileSystem.TryGet(out MissileSystem missiles))
            {
                missiles.Detonated -= OnDetonated;
                missiles.ShotDown -= OnShotDown;
            }
            if (ProjectileSystem.TryGet(out ProjectileSystem projectiles)) projectiles.Impact -= OnImpact;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab)) ShowView((view + 1) % (fighters.Length + 1));
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) ResetStats();
            if (UnityEngine.Input.GetKeyDown(KeyCode.P)) SetDefense(!defense);
            if (UnityEngine.Input.GetKeyDown(KeyCode.O)) SetPriority((TargetPriority)(((int)priority + 1) % (int)TargetPriority.Hull));
            for (int i = 0; i <= (int)Mode.AttackRuns; i++)
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1 + i)) SetMode((Mode)i);

            pilot.Direct(cruiser.Forward);
            for (int i = respawns.Count - 1; i >= 0; i--)
            {
                if (Time.time < respawns[i].time) continue;
                Respawn(respawns[i].ship);
                respawns.RemoveAt(i);
            }

            if (FighterMode || inFlight.Count > 0) return;
            cooldown -= Time.deltaTime;
            if (cooldown <= 0f) Launch();
        }

        private void Launch()
        {
            cooldown = launchInterval;
            Vector3 from = UnityEngine.Random.onUnitSphere;
            switch (mode)
            {
                case Mode.Torpedoes:
                    Fire(torpedo, from * torpedoDistance, 1, 0f);
                    break;
                case Mode.TorpedoSalvo:
                    Fire(torpedo, from * torpedoDistance, torpedoSalvo, 0f, 150f);
                    break;
                case Mode.Missiles:
                    Fire(heat, from * missileDistance, 1, 0f);
                    break;
                case Mode.Swarm:
                    Fire(swarm, from * swarmDistance, swarm.Salvo, swarm.SalvoSpread);
                    break;
            }
        }

        private void Fire(MissileProfile profile, Vector3 offset, int count, float spread, float lateral = 5f)
        {
            Vector3 origin = cruiser.Position + offset;
            Vector3 direction = (cruiser.Position - origin).normalized;
            for (int i = 0; i < count; i++)
            {
                Vector3 heading = spread > 0f ? Quaternion.AngleAxis(spread, UnityEngine.Random.onUnitSphere) * direction : direction;
                Vector3 jitter = Vector3.ProjectOnPlane(UnityEngine.Random.insideUnitSphere, direction) * lateral;
                inFlight.Add(MissileSystem.Instance.Launch(profile, null, origin + jitter, heading, cruiser));
            }
            launched += count;
        }

        private void OnDetonated(MissileTrack track, Vector3 point)
        {
            if (!inFlight.Remove(track.Id)) return;
            if (Vector3.Distance(hull.ClosestPoint(point), point) <= track.Profile.BlastRadius) hits++;
            // Missile runs measure the defenses, not how long they last.
            cruiser.RestoreSubsystems();
        }

        private void OnShotDown(MissileTrack track, SpaceShip source)
        {
            if (inFlight.Remove(track.Id)) shotDown++;
        }

        private void OnTurretFired(ShipTurret turret)
        {
            gunnery.TryGetValue(turret.Weapon, out Vector2Int stats);
            gunnery[turret.Weapon] = stats + new Vector2Int(1, 0);
        }

        private void OnImpact(HitInfo hit)
        {
            if (hit.Source != cruiser || hit.Ship == null || hit.Ship == cruiser || hit.Weapon == null) return;
            gunnery.TryGetValue(hit.Weapon, out Vector2Int stats);
            gunnery[hit.Weapon] = stats + new Vector2Int(0, 1);
        }

        private bool FighterMode => mode >= Mode.Fighters;

        private void OnSubsystemDestroyed(Subsystem subsystem, HitInfo hit)
        {
            subsystemsLost++;
            if (!FighterMode) return;
            foreach (Subsystem part in cruiser.Subsystems)
                if (Wanted(part) && !part.IsDestroyed) return;
            cycles++;
            cycleTime += elapsed - cycleStart;
            cycleStart = elapsed;
            cycleKills = kills;
            cruiser.RestoreSubsystems();
        }

        /// <summary>Subsystems a pass has to destroy under the current priority.</summary>
        private bool Wanted(Subsystem part)
        {
            ShipTurret turret = part.Turret;
            switch (priority)
            {
                case TargetPriority.PointDefense: return turret != null && turret.Role == TurretRole.PointDefense;
                case TargetPriority.Turrets: return turret != null;
                case TargetPriority.Engines: return part.Kind == SubsystemKind.Engine;
                default: return true;
            }
        }

        private void SetPriority(TargetPriority next)
        {
            priority = next;
            foreach (SpaceShip fighter in fighters) fighter.GetComponent<CombatBrain>().PriorityOverride = next;
            ResetStats();
        }

        private void OnFighterDestroyed(SpaceShip fighter)
        {
            kills++;
            respawns.Add((fighter, Time.time + RespawnDelay));
        }

        private void Respawn(SpaceShip fighter)
        {
            if (!FighterMode) return;
            Vector3 position = cruiser.Position + UnityEngine.Random.onUnitSphere * fighterSpawnDistance;
            fighter.transform.SetPositionAndRotation(position, Quaternion.LookRotation(cruiser.Position - position));
            fighter.GetComponent<ShipHealth>().Restore();
            fighter.gameObject.SetActive(true);
        }

        private void SetMode(Mode next)
        {
            mode = next;
            MissileSystem.Instance.Clear();
            ProjectileSystem.Instance.Clear();
            inFlight.Clear();
            respawns.Clear();
            foreach (SpaceShip fighter in fighters)
            {
                fighter.gameObject.SetActive(false);
                var brain = fighter.GetComponent<CombatBrain>();
                brain.UseAttackRuns = mode == Mode.AttackRuns;
                brain.PriorityOverride = priority;
                if (FighterMode) Respawn(fighter);
            }
            if (view > 0 && !FighterMode) ShowView(0);
            ResetStats();
        }

        private void SetDefense(bool on)
        {
            defense = on;
            foreach (ShipTurret turret in turrets) turret.enabled = on;
            ResetStats();
        }

        private void ShowView(int index)
        {
            view = index;
            SpaceShip ship = view == 0 ? cruiser : fighters[view - 1];
            chaseCamera.Target = ship.transform;
            chaseCamera.Offset = new Vector3(0f, 4f, -16f) * Mathf.Max(1f, ship.Radius / 4f);
            hud.Bind(ship.Motor, null);
        }

        private void ResetStats()
        {
            elapsed = 0f;
            launched = shotDown = hits = kills = subsystemsLost = 0;
            cycles = cycleKills = 0;
            cycleStart = cycleTime = 0f;
            gunnery.Clear();
            cruiser.RestoreSubsystems();
        }

        private void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };
            string text = $"{mode} · defense {(defense ? "on" : "off")} · {elapsed:0} s\n";
            if (FighterMode)
            {
                text += $"Fighters killed {kills}, cruiser subsystems lost {subsystemsLost}, power {cruiser.Motor.Power:P0}, speed {cruiser.Motor.Speed:0} m/s\n";
                float perPass = cycles > 0 ? cycleTime / cycles : 0f;
                float lossesPerPass = cycles > 0 ? (float)cycleKills / cycles : 0f;
                text += $"Priority {priority}: done {cycles} times, {perPass:0} s and {lossesPerPass:0.0} fighters lost each\n";
                foreach (KeyValuePair<WeaponProfile, Vector2Int> pair in gunnery)
                    text += $"{pair.Key.name}: shots {pair.Value.x}, hits {pair.Value.y}, accuracy {(pair.Value.x > 0 ? 100f * pair.Value.y / pair.Value.x : 0f):0}%\n";
            }
            else
            {
                float rate = launched > 0 ? 100f * shotDown / launched : 0f;
                float hitRate = launched > 0 ? 100f * hits / launched : 0f;
                text += $"Launched {launched}, shot down {shotDown} ({rate:0}%), hits {hits} ({hitRate:0}%)\n";
            }
            int alive = 0;
            foreach (ShipTurret turret in turrets) if (!turret.IsDestroyed) alive++;
            text += $"Turrets {alive}/{turrets.Length}\n";
            if (view > 0 && fighters[view - 1].TryGetComponent(out CombatBrain watched) && watched.Current != null)
                text += $"{watched.name}: {watched.Current.Name} → {(watched.TargetSubsystem != null ? watched.TargetSubsystem.name : "hull")}\n";
            text += "1 torpedo · 2 torpedo salvo · 3 heat missile · 4 swarm · 5 fighters head-on · 6 attack runs · O priority · P defense · Tab view · R reset";
            GUI.Label(new Rect(16, Screen.height - 40 - 22 * 10, 1300, 22 * 10 + 20), text, style);
        }
    }
}
