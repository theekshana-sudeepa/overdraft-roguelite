using UnityEngine;

namespace Overdraft.Enemies
{
    /// <summary>
    /// A pooled enemy bullet (currently fired only by SPITTER, FR-302). Deliberately dumb — a
    /// data holder — because EnemyManager owns the per-frame update loop over the whole pool,
    /// the same pattern Weapon uses for player projectiles.
    /// </summary>
    public sealed class EnemyProjectile : MonoBehaviour
    {
        public Vector3 Velocity;
        public float LifeRemaining;
        public float Radius;
        public int Damage;

        public void Launch(in Vector3 position, in Vector3 velocity, float lifetime, float radius, int damage)
        {
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(velocity.sqrMagnitude > 0f ? velocity : Vector3.forward, Vector3.up);
            Velocity = velocity;
            LifeRemaining = lifetime;
            Radius = radius;
            Damage = damage;
        }
    }
}
