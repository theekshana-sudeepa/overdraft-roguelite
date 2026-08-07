using UnityEngine;

namespace Overdraft.Player
{
    /// <summary>
    /// A pooled player bullet. Deliberately dumb: it holds state and nothing else. Weapon owns
    /// the update loop so 512 projectiles cost one iteration over one array instead of 512
    /// MonoBehaviour Update callbacks, which is the single biggest per-frame win available here.
    /// </summary>
    public sealed class PlayerProjectile : MonoBehaviour
    {
        public Vector3 Velocity;
        public float LifeRemaining;

        public void Launch(in Vector3 position, in Vector3 velocity, float lifetime)
        {
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(velocity.sqrMagnitude > 0f ? velocity : Vector3.forward, Vector3.up);
            Velocity = velocity;
            LifeRemaining = lifetime;
        }
    }
}
