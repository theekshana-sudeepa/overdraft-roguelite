using UnityEngine;
using Overdraft.Core;
using Overdraft.Economy;
using Overdraft.Enemies;
using Overdraft.Utility;

namespace Overdraft.Player
{
    /// <summary>
    /// Auto-fire (FR-103). There is no fire button and no fire binding — the player's only
    /// weapon decisions are where to stand and where to point.
    /// <para>
    /// Owns both firing and projectile simulation. One loop over a pooled array beats 512
    /// individual Update callbacks by roughly an order of magnitude.
    /// </para>
    /// </summary>
    public sealed class Weapon : MonoBehaviour
    {
        [SerializeField] TuningConfig tuning;
        [SerializeField] PlayerProjectile projectilePrefab;
        [SerializeField] Transform poolParent;
        [SerializeField] PlayerController player;
        [SerializeField] BankSystem bank;
        [SerializeField] EnemyManager enemies;

        ObjectPool<PlayerProjectile> pool;
        DeterministicRandom rng;
        float cooldown;
        float arenaRadius;

        /// <summary>SPREAD upgrades raise this. One projectile per shot until then.</summary>
        public int ProjectilesPerShot { get; set; } = 1;

        /// <summary>SPREAD cone in degrees. Zero means a single aimed shot.</summary>
        public float ConeDegrees { get; set; }

        /// <summary>SPREAD T3 pays for its volume with a damage cut.</summary>
        public float DamageMultiplier { get; set; } = 1f;

        void Awake()
        {
            if (tuning == null) { tuning = ServiceLocator.Tuning; }

            pool = new ObjectPool<PlayerProjectile>(projectilePrefab, poolParent, tuning.Weapon.poolSize);
            arenaRadius = tuning.Arena.floorRadius;
        }

        public void SetSeed(int seed)
        {
            rng = new DeterministicRandom(seed);
        }

        public void SetArenaRadius(float radius)
        {
            arenaRadius = radius;
        }

        public void Tick(float deltaTime)
        {
            AdvanceProjectiles(deltaTime);

            cooldown -= deltaTime;
            if (cooldown > 0f) { return; }

            // FR-206: firing continues while banking, just slower. Stopping entirely would make
            // banking a purely defensive act; keeping it at half rate means the player is still
            // trading damage for safety, which is the interesting version.
            float interval = tuning.Weapon.fireInterval;
            if (bank.IsBanking)
            {
                float rate = tuning.Bank.fireRateMultiplier;
                interval = rate > 0f ? interval / rate : interval;
            }

            Fire();
            cooldown += interval;

            // A single frame must never queue a burst if the game hitched.
            if (cooldown < 0f) { cooldown = 0f; }
        }

        void Fire()
        {
            // Fired from the player's centre. A muzzle offset would need its own tunable and
            // is invisible at projectileSpeed 22 anyway.
            Vector3 origin = player.Position;
            origin.y = 0f;

            Vector3 aim = player.AimDirection;
            float baseAngle = MathX.AngleFromForwardXZ(aim);
            int count = Mathf.Max(1, ProjectilesPerShot);

            for (int i = 0; i < count; i++)
            {
                float coneOffset = 0f;
                if (count > 1)
                {
                    float t = i / (float)(count - 1);
                    coneOffset = Mathf.Lerp(-ConeDegrees, ConeDegrees, t);
                }

                float jitter = rng.Range(-tuning.Weapon.baseSpreadDegrees, tuning.Weapon.baseSpreadDegrees);
                float angle = baseAngle + coneOffset + jitter;

                float rad = angle * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

                PlayerProjectile projectile = pool.Get();
                if (projectile == null) { return; }

                projectile.Launch(origin, direction * tuning.Weapon.projectileSpeed, tuning.Weapon.projectileLifetime);
            }
        }

        void AdvanceProjectiles(float deltaTime)
        {
            int capacity = pool.Capacity;
            int damage = Mathf.Max(1, Mathf.RoundToInt(tuning.Weapon.projectileDamage * DamageMultiplier));
            float radius = tuning.Weapon.projectileRadius;
            float boundarySqr = arenaRadius * arenaRadius;

            for (int i = 0; i < capacity; i++)
            {
                if (!pool.IsActiveAt(i)) { continue; }

                PlayerProjectile projectile = pool.ItemAt(i);
                projectile.LifeRemaining -= deltaTime;

                if (projectile.LifeRemaining <= 0f)
                {
                    pool.Return(projectile);
                    continue;
                }

                Transform t = projectile.transform;
                Vector3 next = t.position + projectile.Velocity * deltaTime;
                next.y = 0f;
                t.position = next;

                if (next.x * next.x + next.z * next.z > boundarySqr)
                {
                    pool.Return(projectile);
                    continue;
                }

                if (enemies.DamageCircle(next, radius, damage, false))
                {
                    pool.Return(projectile);
                }
            }
        }

        public void ResetForRun()
        {
            pool.ReturnAll();
            cooldown = 0f;
            ProjectilesPerShot = 1;
            ConeDegrees = 0f;
            DamageMultiplier = 1f;
        }
    }
}
