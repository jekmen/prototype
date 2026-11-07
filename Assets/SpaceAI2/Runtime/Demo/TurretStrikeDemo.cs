using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SpaceAI2.Demo
{
    /// <summary>
    /// A strike wing ordered onto one capital ship and onto the parts the chosen priority names: keys 1-4 switch
    /// between turrets, point defense, engines and the hull, R repairs the target. Lost ships come back at their spawn.
    /// </summary>
    public class TurretStrikeDemo : MonoBehaviour
    {
        [SerializeField] private SpaceShip target;
        [SerializeField] private CombatBrain[] strikers;
        [SerializeField] private TargetPriority priority = TargetPriority.Turrets;
        [SerializeField] private ChaseCamera chase;
        [SerializeField, Min(0f)] private float respawnDelay = 4f;

        private static readonly TargetPriority[] Keys = { TargetPriority.Turrets, TargetPriority.PointDefense, TargetPriority.Engines, TargetPriority.Hull };

        private readonly Dictionary<SpaceShip, Pose> spawns = new Dictionary<SpaceShip, Pose>();
        private readonly StringBuilder text = new StringBuilder();
        private GUIStyle style;
        private int view;

        public void Setup(SpaceShip capital, CombatBrain[] wing, ChaseCamera cameraRig)
        {
            target = capital;
            strikers = wing;
            chase = cameraRig;
        }

        private void Start()
        {
            Remember(target);
            foreach (CombatBrain brain in strikers)
            {
                Remember(brain.Ship);
                brain.ForcedTarget = target;
            }
            SetPriority(priority);
            if (chase != null && chase.Target == null) chase.Target = strikers[0].transform;
        }

        private void Remember(SpaceShip ship)
        {
            spawns[ship] = new Pose(ship.transform.position, ship.transform.rotation);
            ship.Health.Destroyed += OnDestroyed;
        }

        private void OnDestroy()
        {
            foreach (SpaceShip ship in spawns.Keys)
                if (ship != null) ship.Health.Destroyed -= OnDestroyed;
        }

        private void SetPriority(TargetPriority value)
        {
            priority = value;
            foreach (CombatBrain brain in strikers) brain.PriorityOverride = value;
        }

        private void OnDestroyed(ShipHealth health, HitInfo hit) => StartCoroutine(Respawn(health.GetComponent<SpaceShip>()));

        private IEnumerator Respawn(SpaceShip ship)
        {
            yield return new WaitForSeconds(respawnDelay);
            Pose pose = spawns[ship];
            ship.transform.SetPositionAndRotation(pose.position, pose.rotation);
            ship.Health.Restore();
            ship.RestoreSubsystems();
            if (ship.TryGetComponent(out MissileLauncher launcher)) launcher.Rearm();
            ship.gameObject.SetActive(true);
        }

        private void Update()
        {
            for (int i = 0; i < Keys.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) SetPriority(Keys[i]);
            if (Input.GetKeyDown(KeyCode.R))
            {
                target.Health.Restore();
                target.RestoreSubsystems();
            }
            if (chase != null && (Input.GetKeyDown(KeyCode.Tab) || chase.Target == null || !chase.Target.gameObject.activeInHierarchy))
            {
                // Cycles the wing and the target.
                for (int step = 0; step <= strikers.Length; step++)
                {
                    view = (view + 1) % (strikers.Length + 1);
                    SpaceShip next = view < strikers.Length ? strikers[view].Ship : target;
                    if (!next.gameObject.activeInHierarchy) continue;
                    chase.Target = next.transform;
                    break;
                }
            }
        }

        private void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 14 };
            text.Clear();
            text.Append($"Strike priority: {priority}   (1 turrets, 2 point defense, 3 engines, 4 hull, R repair, Tab camera)\n");
            text.Append($"{target.name}  hull {target.Health.Hull:0}\n");
            foreach (Subsystem part in target.Subsystems)
            {
                int shooters = 0;
                foreach (CombatBrain brain in strikers)
                    if (brain.isActiveAndEnabled && brain.TargetSubsystem == part) shooters++;
                text.Append(part.IsDestroyed ? $"  {part.name}: destroyed" : $"  {part.name}: {part.Health:0}/{part.MaxHealth:0}");
                if (shooters > 0) text.Append($"  <- {shooters}");
                text.Append('\n');
            }
            GUI.Label(new Rect(16, 16, 640, 400), text.ToString(), style);
        }
    }
}
