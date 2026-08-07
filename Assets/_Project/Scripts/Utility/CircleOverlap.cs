using UnityEngine;

namespace Overdraft.Utility
{
    /// <summary>
    /// The only collision primitive in the project (C5, FR-306). Squared distance on the XZ
    /// plane — no sqrt, no Rigidbody, no Collider, no layers, no triggers.
    /// </summary>
    public static class CircleOverlap
    {
        public static bool Overlaps(in Vector3 a, float ra, in Vector3 b, float rb)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            float r = ra + rb;
            return dx * dx + dz * dz <= r * r;
        }

        public static bool Overlaps(float ax, float az, float ra, float bx, float bz, float rb)
        {
            float dx = ax - bx;
            float dz = az - bz;
            float r = ra + rb;
            return dx * dx + dz * dz <= r * r;
        }

        public static float SqrDistanceXZ(in Vector3 a, in Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
