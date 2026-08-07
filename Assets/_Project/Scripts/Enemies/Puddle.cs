using UnityEngine;

namespace Overdraft.Enemies
{
    /// <summary>
    /// A pooled, stationary damage hazard left behind by a dying SPITTER (FR-302). Contact
    /// damage funnels through PlayerHealth.TryTakeDamage the same as anything else, so the
    /// player's normal invulnerability window governs repeat hits — a puddle never needs its
    /// own damage-over-time cooldown.
    /// </summary>
    public sealed class Puddle : MonoBehaviour
    {
        [SerializeField] Transform visual;

        public float TimeRemaining;
        public float Radius;
        public int Damage;

        public void Activate(in Vector3 position, float duration, float radius, int damage)
        {
            transform.position = position;
            TimeRemaining = duration;
            Radius = radius;
            Damage = damage;

            // The visual must match the hitbox exactly — an invisible-radius hazard in a
            // bullet-hell is a fairness bug, not a style choice. DiscMesh is unit-radius, so
            // scale maps 1:1 onto world radius.
            if (visual != null) { visual.localScale = Vector3.one * radius; }
        }
    }
}
