using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace SpaceAI2.Demo
{
    /// <summary>
    /// Main-thread cost per frame, averaged over a second: the asset's own systems measured directly, plus Unity's
    /// script and physics markers for everything else (motors, turrets, guns).
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class PerformanceOverlay : MonoBehaviour
    {
        [SerializeField] private bool show = true;
        [SerializeField] private Rect area = new Rect(16, 16, 900, 90);

        private ProfilerRecorder scriptsUpdate;
        private ProfilerRecorder scriptsFixed;
        private ProfilerRecorder physics;
        private double frameAi, frameProjectiles, frameMissiles;
        private double sumAi, sumProjectiles, sumMissiles, sumScripts, sumPhysics, sumFrame, peak;
        private int frames;
        private float window;
        private GUIStyle style;
        private readonly StringBuilder text = new StringBuilder();

        /// <summary>AI, projectiles and missiles per frame, averaged over the last second.</summary>
        public double AssetMs { get; private set; }
        public double PeakMs { get; private set; }
        public double AiMs { get; private set; }
        public double ProjectilesMs { get; private set; }
        public double MissilesMs { get; private set; }
        public double ScriptsMs { get; private set; }
        public double PhysicsMs { get; private set; }
        public double FrameMs { get; private set; }

        private void OnEnable()
        {
            scriptsUpdate = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Update.ScriptRunBehaviourUpdate");
            scriptsFixed = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "FixedUpdate.ScriptRunBehaviourFixedUpdate");
            physics = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "FixedUpdate.PhysicsFixedUpdate");
        }

        private void OnDisable()
        {
            scriptsUpdate.Dispose();
            scriptsFixed.Dispose();
            physics.Dispose();
        }

        // Runs after the projectile and missile steps of this physics step.
        private void FixedUpdate()
        {
            if (ProjectileSystem.TryGet(out ProjectileSystem projectiles)) frameProjectiles += projectiles.LastUpdateMs;
            if (MissileSystem.TryGet(out MissileSystem missiles)) frameMissiles += missiles.LastUpdateMs;
        }

        private void Update()
        {
            frameAi = SpaceAIWorld.TryGet(out SpaceAIWorld world) ? world.LastUpdateMs : 0.0;
            double asset = frameAi + frameProjectiles + frameMissiles;
            sumAi += frameAi;
            sumProjectiles += frameProjectiles;
            sumMissiles += frameMissiles;
            sumScripts += (scriptsUpdate.LastValue + scriptsFixed.LastValue) * 1e-6;
            sumPhysics += physics.LastValue * 1e-6;
            sumFrame += Time.unscaledDeltaTime * 1000.0;
            if (asset > peak) peak = asset;
            frameProjectiles = frameMissiles = 0.0;
            frames++;

            window += Time.unscaledDeltaTime;
            if (window < 1f) return;
            AiMs = sumAi / frames;
            ProjectilesMs = sumProjectiles / frames;
            MissilesMs = sumMissiles / frames;
            AssetMs = AiMs + ProjectilesMs + MissilesMs;
            ScriptsMs = sumScripts / frames;
            PhysicsMs = sumPhysics / frames;
            FrameMs = sumFrame / frames;
            PeakMs = peak;
            sumAi = sumProjectiles = sumMissiles = sumScripts = sumPhysics = sumFrame = peak = 0.0;
            frames = 0;
            window = 0f;
        }

        private void OnGUI()
        {
            if (!show) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };
            int ships = SpaceAIWorld.TryGet(out SpaceAIWorld world) ? world.PilotCount : 0;
            int shots = ProjectileSystem.TryGet(out ProjectileSystem projectiles) ? projectiles.Count : 0;
            int missiles = MissileSystem.TryGet(out MissileSystem system) ? system.Count : 0;
            text.Clear();
            text.Append($"Pilots {ships} · projectiles {shots} · missiles {missiles} · origin offset {SpaceAI.OriginOffset.magnitude / 1000f:0.0} km\n");
            text.Append($"SpaceAI {AssetMs:0.00} ms/frame (peak {PeakMs:0.00}): AI {AiMs:0.00} · projectiles {ProjectilesMs:0.00} · missiles {MissilesMs:0.00}\n");
            text.Append($"All scripts {ScriptsMs:0.00} ms · physics {PhysicsMs:0.00} ms · frame {FrameMs:0.0} ms");
            GUI.Label(area, text.ToString(), style);
        }
    }
}
