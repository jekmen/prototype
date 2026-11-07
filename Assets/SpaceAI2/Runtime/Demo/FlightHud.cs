using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI2.Demo
{
    public class FlightHud : MonoBehaviour
    {
        [SerializeField] private ShipMotor motor;
        [SerializeField] private PlayerPilot pilot;
        [SerializeField, Min(20f)] private float stickRadius = 60f;
        [Tooltip("Lead markers are shown for enemies within this many gun ranges.")]
        [SerializeField, Min(0.1f)] private float leadRange = 1.5f;
        [SerializeField, Min(4f)] private float leadPipSize = 18f;
        [Tooltip("Subsystems of enemies within this distance get markers.")]
        [SerializeField, Min(0f)] private float subsystemRange = 1600f;

        private GUIStyle style;
        private GUIStyle smallStyle;
        private Texture2D ring;
        private readonly List<SpaceShip> nearby = new List<SpaceShip>();

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
            DrawLeads(pilot);
            DrawSubsystems(pilot);
            DrawLock(pilot.Launcher);
        }

        /// <summary>Marks where to put the nose so the guns hit each enemy in range.</summary>
        private void DrawLeads(PlayerPilot playerPilot)
        {
            Camera view = Camera.main;
            SpaceShip ship = playerPilot.Ship;
            ShipGun gun = playerPilot.Gun;
            if (view == null || ship == null || gun == null || gun.Weapon == null || !SpaceAIWorld.TryGet(out SpaceAIWorld world)) return;
            ring ??= BuildRing(64);

            WeaponProfile weapon = gun.Weapon;
            Vector3 origin = ship.Position;
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            world.Registry.Query(origin, weapon.Range * leadRange, nearby);
            foreach (SpaceShip enemy in nearby)
            {
                if (enemy == null || !enemy.isActiveAndEnabled || !ship.IsEnemy(enemy)) continue;
                Vector3 offset = enemy.Position - origin;
                if (!Ballistics.Intercept(offset, enemy.Velocity - ship.Velocity, enemy.Motor.Acceleration, weapon.MuzzleSpeed,
                        out Vector3 direction, out float time)) continue;
                if (time > weapon.Lifetime * leadRange) continue;

                Vector3 screen = view.WorldToScreenPoint(view.transform.position + direction * 1000f);
                if (screen.z <= 0f) continue;
                Vector2 pip = new Vector2(screen.x, Screen.height - screen.y);

                bool inRange = time <= weapon.Lifetime;
                // The ring is as wide as the enemy looks, so a pip on the crosshair means the shots will hit its hull.
                float pixelsPerRadian = Screen.height * 0.5f / Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float size = Mathf.Clamp(2f * enemy.Radius / offset.magnitude * pixelsPerRadian, leadPipSize, leadPipSize * 3f);
                bool onTarget = inRange && (pip - center).magnitude <= size * 0.5f;
                Color color = onTarget ? new Color(1f, 0.25f, 0.2f, 0.95f) : inRange ? new Color(0.4f, 1f, 0.5f, 0.85f) : new Color(0.4f, 1f, 0.5f, 0.35f);

                Vector3 body = view.WorldToScreenPoint(enemy.Position);
                if (body.z > 0f) DrawLine(new Vector2(body.x, Screen.height - body.y), pip, new Color(color.r, color.g, color.b, color.a * 0.35f));
                DrawRing(pip, size, color);
                DrawBox(pip, 3f, color);
            }
        }

        /// <summary>Marks each part of enemies in range with its health, so missiles can be locked on a part.</summary>
        private void DrawSubsystems(PlayerPilot playerPilot)
        {
            Camera view = Camera.main;
            SpaceShip ship = playerPilot.Ship;
            if (view == null || ship == null || subsystemRange <= 0f || !SpaceAIWorld.TryGet(out SpaceAIWorld world)) return;
            smallStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.UpperCenter };

            Subsystem locked = playerPilot.Launcher != null ? playerPilot.Launcher.LockSubsystem : null;
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            world.Registry.Query(ship.Position, subsystemRange, nearby);
            foreach (SpaceShip enemy in nearby)
            {
                if (enemy == null || !enemy.isActiveAndEnabled || !ship.IsEnemy(enemy) || enemy.Subsystems.Count == 0) continue;
                foreach (Subsystem part in enemy.Subsystems)
                {
                    Vector3 screen = view.WorldToScreenPoint(part.AimPoint);
                    if (screen.z <= 0f) continue;
                    Vector2 point = new Vector2(screen.x, Screen.height - screen.y);
                    if (part.IsDestroyed)
                    {
                        DrawBox(point, 4f, new Color(0.6f, 0.6f, 0.6f, 0.5f));
                        continue;
                    }
                    float health = part.Health / Mathf.Max(part.MaxHealth, 1f);
                    Color color = part == locked ? new Color(1f, 0.25f, 0.2f, 0.95f) : Color.Lerp(new Color(1f, 0.4f, 0.2f, 0.8f), new Color(1f, 0.85f, 0.3f, 0.8f), health);
                    DrawDiamond(point, 12f, color);
                    DrawBar(point + new Vector2(0f, 10f), 16f, health, color);
                    // Only the part under the crosshair is named; a cruiser's labels all at once pile into one smudge.
                    if ((point - center).magnitude > 40f) continue;
                    Color previous = GUI.color;
                    GUI.color = color;
                    GUI.Label(new Rect(point.x - 60f, point.y + 13f, 120f, 16f), part.name, smallStyle);
                    GUI.color = previous;
                }
            }
        }

        private static void DrawDiamond(Vector2 center, float size, Color color)
        {
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, center);
            DrawFrame(center, size * 0.7f, color);
            GUI.matrix = matrix;
        }

        private static void DrawBar(Vector2 center, float width, float fill, Color color)
        {
            Rect back = new Rect(center.x - width * 0.5f, center.y - 1f, width, 2f);
            DrawRect(back, new Color(0f, 0f, 0f, 0.5f));
            DrawRect(new Rect(back.x, back.y, width * Mathf.Clamp01(fill), back.height), color);
        }

        private static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawRing(Vector2 center, float size, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), ring);
            GUI.color = previous;
        }

        private static void DrawLine(Vector2 from, Vector2 to, Color color)
        {
            Vector2 span = to - from;
            float length = span.magnitude;
            if (length < 1f) return;
            Matrix4x4 matrix = GUI.matrix;
            Color previous = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg, from);
            GUI.DrawTexture(new Rect(from.x, from.y - 0.5f, length, 1f), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = previous;
        }

        private static Texture2D BuildRing(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            float outer = size * 0.5f - 1f, inner = outer - size * 0.09f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f).magnitude;
                float alpha = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            return texture;
        }

        private void OnDestroy()
        {
            if (ring != null) Destroy(ring);
        }

        private void DrawLock(MissileLauncher launcher)
        {
            if (launcher == null || launcher.Missile == null) return;
            string state = launcher.IsLocked ? "LOCK" : launcher.LockTarget != null ? "seeking" : "no target";
            if (launcher.LockSubsystem != null && launcher.LockTarget != null) state += " " + launcher.LockSubsystem.name;
            GUI.Label(new Rect(Screen.width * 0.5f - 160f, Screen.height * 0.5f + stickRadius + 12f, 320f, 24f),
                $"Missiles {(launcher.Ammo < 0 ? "∞" : launcher.Ammo.ToString())} · {state}", style);

            SpaceShip target = launcher.LockTarget;
            Camera view = Camera.main;
            if (target == null || view == null) return;
            Vector3 screen = view.WorldToScreenPoint(launcher.LockPoint);
            if (screen.z <= 0f) return;
            Vector2 center = new Vector2(screen.x, Screen.height - screen.y);
            float size = Mathf.Lerp(64f, launcher.LockSubsystem != null ? 20f : 28f, launcher.LockProgress);
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
