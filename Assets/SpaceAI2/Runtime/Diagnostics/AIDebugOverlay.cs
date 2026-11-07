using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SpaceAI2
{
    /// <summary>
    /// Labels every AI ship on screen with its behavior, avoidance maneuver and target, and lists the behavior scores
    /// of one ship: the inspected one, or the one nearest the screen center.
    /// </summary>
    public class AIDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool show = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F3;
        [Tooltip("Camera the labels are placed with. Empty uses the main camera.")]
        [SerializeField] private Camera view;
        [Tooltip("Ships farther from the camera get no label.")]
        [SerializeField, Min(10f)] private float labelDistance = 3000f;
        [Tooltip("Ship whose scores are listed. Empty picks the one nearest the screen center.")]
        [SerializeField] private CombatBrain inspected;

        private static readonly Color[] TeamColors = { new Color(0.45f, 0.75f, 1f), new Color(1f, 0.5f, 0.4f), new Color(0.6f, 1f, 0.5f), new Color(1f, 0.9f, 0.4f) };

        private readonly StringBuilder text = new StringBuilder();
        private readonly List<CombatBehavior> sorted = new List<CombatBehavior>();
        private GUIStyle label;
        private GUIStyle panel;

        public bool Show
        {
            get => show;
            set => show = value;
        }

        public CombatBrain Inspected
        {
            get => inspected;
            set => inspected = value;
        }

        private void Update()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey)) show = !show;
        }

        private void OnGUI()
        {
            if (!show || Event.current.type != EventType.Repaint) return;
            Camera cam = view != null ? view : Camera.main;
            if (cam == null) return;
            label ??= new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.LowerCenter };
            panel ??= new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.UpperLeft, richText = true };

            CombatBrain focus = inspected != null && inspected.isActiveAndEnabled ? inspected : null;
            float bestCenter = float.MaxValue;
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            foreach (CombatBrain brain in CombatBrain.Active)
            {
                Vector3 screen = cam.WorldToScreenPoint(brain.transform.position);
                if (screen.z <= 0f || screen.z > labelDistance) continue;
                Vector2 point = new Vector2(screen.x, Screen.height - screen.y);
                if (inspected == null)
                {
                    float d = (point - center).sqrMagnitude;
                    if (d < bestCenter)
                    {
                        bestCenter = d;
                        focus = brain;
                    }
                }
                DrawLabel(brain, point);
            }
            if (focus != null) DrawPanel(focus);
        }

        private void DrawLabel(CombatBrain brain, Vector2 point)
        {
            AIPilot pilot = brain.GetComponent<AIPilot>();
            text.Clear();
            text.Append(brain.Current != null ? brain.Current.Name : "-");
            if (pilot != null && pilot.Maneuver != ManeuverKind.None) text.Append(" · ").Append(pilot.Maneuver);
            if (brain.Target != null) text.Append("\n→ ").Append(brain.Target.name);
            GUI.color = brain.Ship.Faction != null ? brain.Ship.Faction.Color : TeamColors[Mathf.Abs(brain.Ship.Team) % TeamColors.Length];
            GUI.Label(new Rect(point.x - 120f, point.y - 52f, 240f, 40f), text.ToString(), label);
            GUI.color = Color.white;
        }

        private void DrawPanel(CombatBrain brain)
        {
            CombatContext c = brain.Context;
            AIPilot pilot = brain.GetComponent<AIPilot>();
            text.Clear();
            text.Append("<b>").Append(brain.name).Append("</b>  (").Append(toggleKey).Append(" hides)\n");
            text.Append("Behavior: ").Append(brain.Current != null ? brain.Current.Name : "-")
                .Append($"  {c.TimeInBehavior:0.0} s, switches {brain.Switches}\n");
            text.Append("Maneuver: ").Append(pilot != null ? pilot.Maneuver.ToString() : "-").Append('\n');
            if (c.Target != null)
            {
                text.Append("Target: ").Append(c.Target.name);
                if (c.Subsystem != null) text.Append(" / ").Append(c.Subsystem.name);
                text.Append($"  {c.Distance:0} m, angle off {c.AngleOff:0}°, aspect {c.Aspect:0}°\n");
            }
            else
            {
                text.Append("Target: none\n");
            }
            if (c.Threat != null) text.Append("Threat: ").Append(c.Threat.name).Append($" {c.ThreatLevel:0.00}\n");
            if (c.HasMissile) text.Append($"Missile inbound, {c.MissileTimeToGo:0.0} s\n");
            if (brain.Formation != null) text.Append("Formation slot ").Append(brain.Slot).Append('\n');
            ShipHealth health = brain.Ship.Health;
            if (health != null) text.Append($"Hull {health.Hull:0} · shield {health.Shield:0}\n");

            text.Append("\n<b>Scores</b>\n");
            sorted.Clear();
            sorted.AddRange(brain.Behaviors);
            sorted.Sort((a, b) => b.LastScore.CompareTo(a.LastScore));
            foreach (CombatBehavior behavior in sorted)
            {
                bool current = behavior == brain.Current;
                if (current) text.Append("<color=yellow>");
                text.Append($"{behavior.LastScore,6:0.00}  ").Append(behavior.Name);
                if (current) text.Append("</color>");
                text.Append('\n');
            }

            float height = panel.CalcHeight(new GUIContent(text.ToString()), 330f) + 8f;
            GUI.Box(new Rect(Screen.width - 346f, 16f, 330f, height), text.ToString(), panel);
        }
    }
}
