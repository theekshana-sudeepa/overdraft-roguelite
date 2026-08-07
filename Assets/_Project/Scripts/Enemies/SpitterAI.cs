using UnityEngine;
using Overdraft.Utility;

namespace Overdraft.Enemies
{
    /// <summary>
    /// SPITTER (FR-302): holds a preferred distance band from the player, strafing, and fires an
    /// aimed projectile on an interval. Never closes to melee by design. Leaves a damaging
    /// puddle where it died.
    /// </summary>
    public sealed class SpitterAI : EnemyBase
    {
        float strafeSign;
        float fireTimer;

        protected override void OnSpawned()
        {
            // A cheap positional hash rather than UnityEngine.Random: deterministic per spawn,
            // zero shared-state dependency, and avoids drawing from any seeded stream that
            // belongs to the generator.
            float hash = Mathf.Sin(transform.position.x * 12.9898f + transform.position.z * 78.233f) * 43758.5453f;
            strafeSign = hash - Mathf.Floor(hash) >= 0.5f ? 1f : -1f;

            // Stagger the first shot rather than firing the instant it spawns.
            fireTimer = definition.fireInterval * 0.5f;
        }

        public override void TickAI(float deltaTime, in Vector3 playerPosition)
        {
            Vector3 position = transform.position;
            Vector3 toPlayer = playerPosition - position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            Vector3 radial = distance > 1e-4f ? toPlayer / distance : Vector3.zero;
            Vector3 tangent = new Vector3(-radial.z, 0f, radial.x) * strafeSign;

            float error = distance - definition.preferredRange;
            Vector3 desired = radial * Mathf.Clamp(error, -1f, 1f) + tangent;
            Vector3 direction = MathX.NormalizeXZ(desired);

            float speed = definition.moveSpeed * speedMultiplier;
            transform.position = position + direction * (speed * deltaTime);

            if (visual != null && direction != Vector3.zero)
            {
                visual.rotation = Quaternion.Euler(0f, MathX.AngleFromForwardXZ(direction), 0f);
            }

            fireTimer -= deltaTime;
            if (fireTimer <= 0f)
            {
                Fire(playerPosition);
                fireTimer = definition.fireInterval;
            }
        }

        internal override void OnDeath(in Vector3 position)
        {
            if (Owner == null) { return; }
            Owner.SpawnPuddle(position, definition.puddleDuration, definition.puddleRadius, definition.contactDamage);
        }

        void Fire(in Vector3 playerPosition)
        {
            if (Owner == null) { return; }

            Vector3 direction = MathX.NormalizeXZ(playerPosition - transform.position);
            if (direction == Vector3.zero) { direction = Vector3.forward; }

            Vector3 velocity = direction * definition.projectileSpeed;
            Owner.FireProjectile(
                transform.position, velocity,
                tuning.EnemyCombat.projectileLifetime, tuning.EnemyCombat.projectileRadius,
                definition.contactDamage);
        }
    }
}
