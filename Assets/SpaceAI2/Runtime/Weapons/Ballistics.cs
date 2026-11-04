using UnityEngine;

namespace SpaceAI2
{
    public static class Ballistics
    {
        /// <summary>
        /// Direction to fire a projectile so it meets the target. Projectiles inherit the shooter's velocity,
        /// so position and velocity are relative to the shooter; acceleration is the target's own.
        /// </summary>
        public static bool Intercept(Vector3 relativePosition, Vector3 relativeVelocity, Vector3 targetAcceleration,
            float projectileSpeed, out Vector3 direction, out float time, int iterations = 3)
        {
            direction = relativePosition.sqrMagnitude > 0f ? relativePosition.normalized : Vector3.forward;
            if (!SolveConstantVelocity(relativePosition, relativeVelocity, projectileSpeed, out time)) return false;

            Vector3 predicted = Predict(relativePosition, relativeVelocity, targetAcceleration, time);
            for (int i = 0; i < iterations; i++)
            {
                time = predicted.magnitude / projectileSpeed;
                predicted = Predict(relativePosition, relativeVelocity, targetAcceleration, time);
            }

            if (predicted.sqrMagnitude > 0f) direction = predicted.normalized;
            return true;
        }

        public static Vector3 Predict(Vector3 position, Vector3 velocity, Vector3 acceleration, float time) =>
            position + velocity * time + acceleration * (0.5f * time * time);

        private static bool SolveConstantVelocity(Vector3 p, Vector3 v, float speed, out float time)
        {
            // |p + v t| = speed t
            float a = Vector3.Dot(v, v) - speed * speed;
            float b = 2f * Vector3.Dot(p, v);
            float c = Vector3.Dot(p, p);
            time = 0f;

            if (Mathf.Abs(a) < 1e-4f)
            {
                if (b >= 0f) return false;
                time = -c / b;
                return true;
            }

            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f) return false;
            float root = Mathf.Sqrt(discriminant);
            float t1 = (-b - root) / (2f * a);
            float t2 = (-b + root) / (2f * a);
            time = t1 > 0f && (t1 < t2 || t2 <= 0f) ? t1 : t2;
            return time > 0f;
        }
    }
}
