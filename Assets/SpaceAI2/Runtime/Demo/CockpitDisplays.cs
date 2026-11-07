using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SpaceAI2.Demo
{
    /// <summary>
    /// Drives the Player Fighter's three instrument screens: system bars, an attitude horizon and a top-down radar.
    /// Positions are in this transform's space, where the screens face -Z toward the pilot.
    /// </summary>
    public class CockpitDisplays : MonoBehaviour
    {
        [Header("System")]
        [Tooltip("Bar pivots at the left end of each bar; their X scale as saved is a full bar.")]
        [SerializeField] private Transform speedBar;
        [SerializeField] private Transform shieldBar;
        [SerializeField] private Transform hullBar;

        [Header("Flight")]
        [Tooltip("Horizon and pitch lines; rolls and slides against the fixed aircraft symbol.")]
        [SerializeField] private Transform horizon;
        [SerializeField, Min(0f)] private float pitchTravel = 0.035f;
        [SerializeField] private TextMesh speedText;
        [SerializeField] private TextMesh modeText;

        [Header("Radar")]
        [SerializeField] private Vector3 radarCenter = new Vector3(0.34f, 0.005f, 2.944f);
        [SerializeField, Min(0.001f)] private float radarRadius = 0.045f;
        [SerializeField, Min(1f)] private float radarRange = 2500f;
        [SerializeField, Min(0.001f)] private float blipSize = 0.006f;
        [SerializeField] private Material blipMaterial;
        [SerializeField] private Color enemyColor = new Color(1f, 0.3f, 0.25f);
        [SerializeField] private Color friendColor = new Color(0.3f, 0.9f, 1f);
        [SerializeField] private Color lockColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private TextMesh rangeText;

        private SpaceShip ship;
        private MissileLauncher launcher;
        private float speedFull, shieldFull, hullFull;
        private readonly List<SpaceShip> nearby = new List<SpaceShip>();
        private readonly List<MeshRenderer> blips = new List<MeshRenderer>();
        private MaterialPropertyBlock block;
        private Mesh blipMesh;
        private Vector3 horizonPosition;
        private Quaternion horizonRotation;
        private float nextText;

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            ship = GetComponentInParent<SpaceShip>();
            launcher = ship != null ? ship.GetComponent<MissileLauncher>() : null;
            if (horizon != null)
            {
                horizonPosition = horizon.localPosition;
                horizonRotation = horizon.localRotation;
            }
            speedFull = speedBar != null ? speedBar.localScale.x : 1f;
            shieldFull = shieldBar != null ? shieldBar.localScale.x : 1f;
            hullFull = hullBar != null ? hullBar.localScale.x : 1f;
            block = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (ship == null || !ship.isActiveAndEnabled) return;
            ShipProfile profile = ship.Motor.Profile;
            ShipHealth health = ship.Health;

            if (profile != null) SetBar(speedBar, speedFull, ship.Motor.Speed / Mathf.Max(profile.BoostSpeed, 1f));
            if (health != null)
            {
                SetBar(shieldBar, shieldFull, health.MaxShield > 0f ? health.Shield / health.MaxShield : 0f);
                SetBar(hullBar, hullFull, health.Hull / Mathf.Max(health.MaxHull, 1f));
            }

            DrawHorizon();
            DrawRadar();

            if (Time.unscaledTime < nextText) return;
            nextText = Time.unscaledTime + 0.1f;
            if (speedText != null) speedText.text = $"VEL {ship.Motor.Speed:0}";
            if (modeText != null && launcher != null)
            {
                modeText.text = launcher.IsLocked ? "LOCK" : launcher.Missile == null ? "GUN" : launcher.Ammo < 0 ? "MSL ∞" : $"MSL {launcher.Ammo}";
                modeText.color = launcher.IsLocked ? lockColor : friendColor;
            }
        }

        private static void SetBar(Transform bar, float full, float fill)
        {
            if (bar == null) return;
            Vector3 scale = bar.localScale;
            scale.x = full * Mathf.Clamp(fill, 0.001f, 1f);
            bar.localScale = scale;
        }

        private void DrawHorizon()
        {
            if (horizon == null) return;
            Transform body = ship.transform;
            Vector3 up = Vector3.up;
            float roll = Mathf.Atan2(Vector3.Dot(body.right, up), Vector3.Dot(body.up, up)) * Mathf.Rad2Deg;
            float pitch = Mathf.Asin(Mathf.Clamp(Vector3.Dot(body.forward, up), -1f, 1f)) * Mathf.Rad2Deg;
            horizon.localRotation = horizonRotation * Quaternion.Euler(0f, 0f, -roll);
            horizon.localPosition = horizonPosition + Vector3.down * (Mathf.Clamp(pitch / 45f, -1f, 1f) * pitchTravel);
        }

        private void DrawRadar()
        {
            if (blipMaterial == null || !SpaceAIWorld.TryGet(out SpaceAIWorld world)) return;
            blipMesh ??= BuildBlip();

            SpaceShip locked = launcher != null ? launcher.LockTarget : null;
            SpaceShip nearest = null;
            float nearestDistance = float.MaxValue;
            Quaternion toLocal = Quaternion.Inverse(ship.transform.rotation);
            world.Registry.Query(ship.Position, radarRange * 1.5f, nearby);

            int used = 0;
            foreach (SpaceShip other in nearby)
            {
                if (other == null || other == ship || !other.isActiveAndEnabled) continue;
                Vector3 offset = toLocal * (other.Position - ship.Position);
                bool enemy = ship.IsEnemy(other);
                if (enemy && offset.magnitude < nearestDistance)
                {
                    nearestDistance = offset.magnitude;
                    nearest = other;
                }

                // Top-down: right stays right, ahead is up the screen; contacts past the range sit on the rim.
                Vector2 flat = new Vector2(offset.x, offset.z) / radarRange;
                if (flat.sqrMagnitude > 1f) flat.Normalize();
                MeshRenderer blip = Blip(used++);
                blip.transform.localPosition = radarCenter + (Vector3)(flat * radarRadius);
                blip.transform.localScale = Vector3.one * (blipSize * (other == locked ? 1.6f : 1f));
                block.SetColor(ColorId, other == locked ? lockColor : enemy ? enemyColor : friendColor);
                blip.SetPropertyBlock(block);
            }
            for (int i = used; i < blips.Count; i++) blips[i].enabled = false;

            if (rangeText == null || Time.unscaledTime < nextText) return;
            SpaceShip shown = locked != null ? locked : nearest;
            rangeText.text = shown == null ? "TGT --" : "TGT " + (Vector3.Distance(shown.Position, ship.Position) / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " KM";
        }

        private MeshRenderer Blip(int index)
        {
            if (index < blips.Count)
            {
                blips[index].enabled = true;
                return blips[index];
            }
            var go = new GameObject("Blip");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = blipMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = blipMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            blips.Add(renderer);
            return renderer;
        }

        private static Mesh BuildBlip()
        {
            var mesh = new Mesh { name = "Radar Blip", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { new Vector3(0f, 0.5f), new Vector3(0.5f, 0f), new Vector3(0f, -0.5f), new Vector3(-0.5f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            if (blipMesh != null) Destroy(blipMesh);
        }
    }
}
