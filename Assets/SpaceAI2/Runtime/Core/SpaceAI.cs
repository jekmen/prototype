using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpaceAI2
{
    /// <summary>Scene-wide services of the asset.</summary>
    public static class SpaceAI
    {
        private static readonly List<GameObject> roots = new List<GameObject>();
        private static readonly List<Vector3> trailScratch = new List<Vector3>();
        private static ParticleSystem.Particle[] particleScratch = new ParticleSystem.Particle[256];

        /// <summary>
        /// Raised after the world moved by <c>-offset</c>. Systems that keep world positions outside transforms
        /// (projectiles, missiles, cached aim points) subtract it here.
        /// </summary>
        public static event Action<Vector3> OriginShifted;

        /// <summary>Total offset of the current origin from the scene's original one.</summary>
        public static Vector3 OriginOffset { get; private set; }

        /// <summary>
        /// Moves the world origin to <paramref name="offset"/>: every root object, trail and world-space particle in
        /// the loaded scenes moves by <c>-offset</c>, so nothing the player sees changes but coordinates stay small.
        /// Velocities are untouched. Call it between physics steps, e.g. from <see cref="FloatingOrigin"/>.
        /// </summary>
        public static void ShiftOrigin(Vector3 offset)
        {
            if (offset == Vector3.zero) return;
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                scene.GetRootGameObjects(roots);
                foreach (GameObject root in roots) root.transform.position -= offset;
            }
            ShiftTrails(offset);
            ShiftParticles(offset);
            Physics.SyncTransforms();
            // Assigning the position teleports the body, so interpolation does not blend from the old coordinates.
            foreach (Rigidbody body in UnityEngine.Object.FindObjectsOfType<Rigidbody>())
                if (body.interpolation != RigidbodyInterpolation.None) body.position = body.transform.position;

            OriginOffset += offset;
            if (SpaceAIWorld.TryGet(out SpaceAIWorld world)) world.Registry.Rebuild();
            OriginShifted?.Invoke(offset);
        }

        private static void ShiftTrails(Vector3 offset)
        {
            foreach (TrailRenderer trail in UnityEngine.Object.FindObjectsOfType<TrailRenderer>())
            {
                int count = trail.positionCount;
                if (count == 0) continue;
                trailScratch.Clear();
                for (int i = 0; i < count; i++) trailScratch.Add(trail.GetPosition(i) - offset);
                trail.SetPositions(trailScratch.ToArray());
            }
            foreach (LineRenderer line in UnityEngine.Object.FindObjectsOfType<LineRenderer>())
            {
                if (!line.useWorldSpace) continue;
                for (int i = 0; i < line.positionCount; i++) line.SetPosition(i, line.GetPosition(i) - offset);
            }
        }

        private static void ShiftParticles(Vector3 offset)
        {
            foreach (ParticleSystem system in UnityEngine.Object.FindObjectsOfType<ParticleSystem>())
            {
                if (system.main.simulationSpace != ParticleSystemSimulationSpace.World) continue;
                if (particleScratch.Length < system.particleCount) particleScratch = new ParticleSystem.Particle[system.particleCount];
                int count = system.GetParticles(particleScratch);
                for (int i = 0; i < count; i++) particleScratch[i].position -= offset;
                system.SetParticles(particleScratch, count);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OriginShifted = null;
            OriginOffset = Vector3.zero;
        }
    }
}
