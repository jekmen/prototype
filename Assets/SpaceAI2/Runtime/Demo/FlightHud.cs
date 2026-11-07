using UnityEngine;

namespace SpaceAI2.Demo
{
    public class FlightHud : MonoBehaviour
    {
        [SerializeField] private ShipMotor motor;
        [SerializeField] private PlayerPilot pilot;
        [SerializeField, Min(20f)] private float stickRadius = 60f;

        private GUIStyle style;

        /// <summary>Height of the text block in the top-left corner, for other overlays to sit below it.</summary>
        public const float TextHeight = 96f;

        public bool Visible => isActiveAndEnabled && motor != null && motor.Profile != null;

        public void Bind(ShipMotor shipMotor, PlayerPilot playerPilot)
        {
            motor = shipMotor;
            pilot = playerPilot;
        }

        private void OnGUI()
        {
            if (motor == null || motor.Profile == null) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };

            ShipProfile profile = motor.Profile;
            float speed = motor.Speed;
            float slip = Vector3.Angle(motor.transform.forward, motor.Velocity);
            string text =
                $"Speed {speed:0} / {motor.TargetSpeed:0} m/s\n" +
                $"Turn radius {profile.TurnRadius(speed):0} m, 180° in {profile.HalfTurnTime:0.0} s\n" +
                $"Slip {slip:0}°\n" +
                "Mouse stick · W/S throttle · Shift boost · A/D yaw · Q/E roll · Space center · Esc cursor" +
                (pilot != null ? "\nLMB guns · RMB missile · C cockpit" : "");
            GUI.Label(new Rect(16, 16, 700, 140), text, style);

            if (pilot == null) return;
            DrawStick(pilot);
            DrawLock(pilot.Launcher);
        }

        private void DrawLock(MissileLauncher launcher)
        {
            if (launcher == null || launcher.Missile == null) return;
            string state = launcher.IsLocked ? "LOCK" : launcher.LockTarget != null ? "seeking" : "no target";
            GUI.Label(new Rect(Screen.width * 0.5f - 120f, Screen.height * 0.5f + stickRadius + 12f, 240f, 24f),
                $"Missiles {(launcher.Ammo < 0 ? "∞" : launcher.Ammo.ToString())} · {state}", style);

            SpaceShip target = launcher.LockTarget;
            Camera view = Camera.main;
            if (target == null || view == null) return;
            Vector3 screen = view.WorldToScreenPoint(target.Position);
            if (screen.z <= 0f) return;
            Vector2 center = new Vector2(screen.x, Screen.height - screen.y);
            float size = Mathf.Lerp(64f, 28f, launcher.LockProgress);
            Color color = launcher.IsLocked ? new Color(1f, 0.25f, 0.2f, 0.9f) : new Color(1f, 0.85f, 0.2f, 0.8f);
            DrawFrame(center, size, color);
        }

        private static void DrawFrame(Vector2 center, float size, Color color)
        {
            const float line = 2f;
            float half = size * 0.5f;
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - half, center.y - half, size, line), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x - half, center.y + half - line, size, line), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x - half, center.y - half, line, size), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x + half - line, center.y - half, line, size), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawStick(PlayerPilot playerPilot)
        {
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            DrawBox(center, stickRadius * 2f, new Color(1f, 1f, 1f, 0.08f));
            DrawBox(center, stickRadius * 2f * playerPilot.DeadZone, new Color(1f, 1f, 1f, 0.2f));

            Vector2 stick = playerPilot.Stick;
            Vector2 marker = center + new Vector2(stick.x, -stick.y) * stickRadius;
            DrawBox(marker, 8f, new Color(0.3f, 1f, 0.4f, 0.9f));
        }

        private static void DrawBox(Vector2 center, float size, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
