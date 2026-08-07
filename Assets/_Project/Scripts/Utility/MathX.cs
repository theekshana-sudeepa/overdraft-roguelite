using UnityEngine;

namespace Overdraft.Utility
{
    /// <summary>Small maths helpers used in hot paths. All branch-light, none allocate.</summary>
    public static class MathX
    {
        /// <summary>Framerate-independent exponential approach. dt-safe, unlike a raw Lerp.</summary>
        public static float Damp(float current, float target, float lambda, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-lambda * dt));
        }

        public static Vector3 Damp(in Vector3 current, in Vector3 target, float lambda, float dt)
        {
            float t = 1f - Mathf.Exp(-lambda * dt);
            return new Vector3(
                current.x + (target.x - current.x) * t,
                current.y + (target.y - current.y) * t,
                current.z + (target.z - current.z) * t);
        }

        /// <summary>Ease-out cubic, used for the dash arc and UI pops.</summary>
        public static float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        /// <summary>Moves a value toward a target at a fixed rate. No overshoot.</summary>
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            float d = target - current;
            if (d > maxDelta) { return current + maxDelta; }
            if (d < -maxDelta) { return current - maxDelta; }
            return target;
        }

        /// <summary>Clamps a position to a circle on the XZ plane, preserving Y.</summary>
        public static Vector3 ClampToCircleXZ(in Vector3 p, float radius)
        {
            float sqr = p.x * p.x + p.z * p.z;
            if (sqr <= radius * radius) { return p; }
            float inv = radius / Mathf.Sqrt(sqr);
            return new Vector3(p.x * inv, p.y, p.z * inv);
        }

        /// <summary>Normalises an XZ vector, returning Vector3.zero below the epsilon.</summary>
        public static Vector3 NormalizeXZ(in Vector3 v)
        {
            float sqr = v.x * v.x + v.z * v.z;
            if (sqr < 1e-8f) { return Vector3.zero; }
            float inv = 1f / Mathf.Sqrt(sqr);
            return new Vector3(v.x * inv, 0f, v.z * inv);
        }

        /// <summary>Signed degrees from +Z, matching Transform.rotation on the Y axis.</summary>
        public static float AngleFromForwardXZ(in Vector3 dir)
        {
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }
    }
}
