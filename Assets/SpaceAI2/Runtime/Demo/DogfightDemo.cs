using System.Collections;
using System.Text;
using UnityEngine;

namespace SpaceAI2.Demo
{
    /// <summary>Team dogfight with respawns and stats that show whether pilots get stuck in one behavior.</summary>
    public class DogfightDemo : MonoBehaviour
    {
        [SerializeField] private CombatBrain[] brains;
        [SerializeField] private Transform[] spawns;
        [SerializeField] private ChaseCamera chaseCamera;
        [SerializeField] private FlightHud hud;
        [Tooltip("Optional ship flown by the player; the camera stays on it and Tab is off.")]
        [SerializeField] private SpaceShip player;
        [Tooltip("Large ships that come back where they started, with their subsystems repaired.")]
        [SerializeField] private SpaceShip[] capitalShips = new SpaceShip[0];
        [SerializeField, Min(0f)] private float respawnDelay = 4f;
        [SerializeField, Min(0f)] private float spawnSpread = 150f;

        private readonly int[] kills = new int[2];
        private int view;
        private float elapsed;
        private int switches;
        private int shots;
        private int hits;
        private float longestBehavior;
        private string longestBehaviorName = "-";
        private float longestDryChase;
        private GUIStyle style;
        private readonly StringBuilder text = new StringBuilder();
        private Pose[] capitalPoses;

        public void Setup(CombatBrain[] shipBrains, Transform[] teamSpawns, ChaseCamera cameraRig, FlightHud flightHud)
        {
            brains = shipBrains;
            spawns = teamSpawns;
            chaseCamera = cameraRig;
            hud = flightHud;
        }

        private void Start()
        {
            foreach (CombatBrain brain in brains)
            {
                brain.BehaviorChanged += OnBehaviorChanged;
                brain.GetComponent<ShipHealth>().Destroyed += OnDestroyed;
                brain.GetComponent<ShipGun>().Fired += _ => shots++;
                brain.Ship.Hit += hit =>
                {
                    if (hit.Source != null) hits++;
                };
            }
            if (player != null) player.GetComponent<ShipHealth>().Destroyed += OnDestroyed;
            capitalPoses = new Pose[capitalShips.Length];
            for (int i = 0; i < capitalShips.Length; i++)
            {
                capitalPoses[i] = new Pose(capitalShips[i].transform.position, capitalShips[i].transform.rotation);
                capitalShips[i].Health.Destroyed += OnDestroyed;
            }
            ShowView(0);
        }

        private void OnDestroy()
        {
            foreach (CombatBrain brain in brains)
            {
                if (brain == null) continue;
                brain.BehaviorChanged -= OnBehaviorChanged;
                brain.GetComponent<ShipHealth>().Destroyed -= OnDestroyed;
            }
            if (player != null) player.GetComponent<ShipHealth>().Destroyed -= OnDestroyed;
            foreach (SpaceShip capital in capitalShips)
                if (capital != null) capital.Health.Destroyed -= OnDestroyed;
        }

        private void OnBehaviorChanged(CombatBrain brain, CombatBehavior from, CombatBehavior to)
        {
            if (from != null) switches++;
        }

        private void OnDestroyed(ShipHealth health, HitInfo hit)
        {
            if (hit.Source != null && hit.Source.Team >= 0 && hit.Source.Team < kills.Length) kills[hit.Source.Team]++;
            StartCoroutine(Respawn(health));
        }

        private IEnumerator Respawn(ShipHealth health)
        {
            yield return new WaitForSeconds(respawnDelay);
            var ship = health.GetComponent<SpaceShip>();
            Transform spawn = spawns[Mathf.Clamp(ship.Team, 0, spawns.Length - 1)];
            Vector3 position = spawn.position + Random.insideUnitSphere * spawnSpread;
            Quaternion rotation = spawn.rotation;
            int capital = System.Array.IndexOf(capitalShips, ship);
            if (capital >= 0)
            {
                position = capitalPoses[capital].position;
                rotation = capitalPoses[capital].rotation;
                ship.RestoreSubsystems();
            }

            health.Restore();
            if (ship.TryGetComponent(out MissileLauncher launcher)) launcher.Rearm();
            ship.transform.SetPositionAndRotation(position, rotation);
            Rigidbody body = ship.Motor.Body;
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
                body.velocity = capital >= 0 ? Vector3.zero : rotation * Vector3.forward * ship.Motor.Profile.CruiseSpeed;
                body.angularVelocity = Vector3.zero;
            }
            ship.gameObject.SetActive(true);
            TrailRenderer trail = ship.GetComponentInChildren<TrailRenderer>();
            if (trail != null) trail.Clear();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (player == null && UnityEngine.Input.GetKeyDown(KeyCode.Tab)) ShowView((view + 1) % brains.Length);
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) ResetStats();

            foreach (CombatBrain brain in brains)
            {
                if (!brain.isActiveAndEnabled || brain.Current == null) continue;
                CombatContext context = brain.Context;
                if (!(brain.Current is PatrolBehavior) && context.TimeInBehavior > longestBehavior)
                {
                    longestBehavior = context.TimeInBehavior;
                    longestBehaviorName = brain.Current.Name;
                }
                longestDryChase = Mathf.Max(longestDryChase, context.TimeSinceFiringChance);
            }
        }

        private void ShowView(int index)
        {
            view = index;
            if (player != null)
            {
                chaseCamera.Target = player.transform;
                hud.Bind(player.Motor, player.GetComponent<PlayerPilot>());
                return;
            }
            SpaceShip ship = brains[view].Ship;
            chaseCamera.Target = ship.transform;
            hud.Bind(ship.Motor, null);
        }

        private void ResetStats()
        {
            elapsed = 0f;
            switches = 0;
            shots = 0;
            hits = 0;
            kills[0] = kills[1] = 0;
            longestBehavior = 0f;
            longestBehaviorName = "-";
            longestDryChase = 0f;
        }

        private void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };
            text.Clear();
            float minutes = Mathf.Max(elapsed / 60f, 0.01f);
            int total = brains.Length + (player != null ? 1 : 0);
            int alive = player != null && player.isActiveAndEnabled ? 1 : 0;
            foreach (CombatBrain brain in brains)
                if (brain.isActiveAndEnabled) alive++;

            text.Append($"Dogfight {total / 2}v{total / 2} · {elapsed:0} s · kills blue {kills[0]} / red {kills[1]} · alive {alive}\n");
            double aiMs =SpaceAIWorld.TryGet(out SpaceAIWorld world) ? world.LastUpdateMs : 0f;
            int projectiles = ProjectileSystem.TryGet(out ProjectileSystem system) ? system.Count : 0;
            float accuracy = shots > 0 ? 100f * hits / shots : 0f;
            text.Append($"AI {aiMs:0.00} ms · projectiles {projectiles} · shots/s per ship {shots / Mathf.Max(elapsed, 0.01f) / brains.Length:0.0}, accuracy {accuracy:0}% · switches/min per ship {switches / minutes / brains.Length:0.0} · " +
                        $"longest behavior {longestBehavior:0.0} s ({longestBehaviorName}) · longest dry chase {longestDryChase:0.0} s\n");

            if (player != null)
            {
                GUI.Label(new Rect(16, Screen.height - 70, 1400, 60), text.ToString(), style);
                return;
            }

            CombatBrain viewed = brains[view];
            CombatContext c = viewed.Context;
            var health = viewed.GetComponent<ShipHealth>();
            string target = c.Target != null ? $"{c.Target.name} {c.Distance:0} m, off {c.AngleOff:0}°, aspect {c.Aspect:0}°" : "none";
            string threat = c.Threat != null ? $"{c.Threat.name} {c.ThreatLevel:0.00}" : "none";
            text.Append($"View {viewed.name}: {(viewed.Current != null ? viewed.Current.Name : "-")} {c.TimeInBehavior:0.0} s · " +
                        $"hull {health.Hull:0}/{health.MaxHull:0} shield {health.Shield:0} · target {target} · threat {threat}\n");
            text.Append("Scores:");
            foreach (CombatBehavior behavior in viewed.Behaviors) text.Append($" {behavior.Name} {behavior.LastScore:0.00} ·");
            text.Append("\nTab switch view · R reset stats");
            GUI.Label(new Rect(16, Screen.height - 150, 1400, 140), text.ToString(), style);
        }
    }
}
