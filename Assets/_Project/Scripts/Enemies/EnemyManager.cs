using UnityEngine;
using Overdraft.Core;
using Overdraft.Generation;
using Overdraft.Utility;

namespace Overdraft.Enemies
{
    /// <summary>
    /// Owns every enemy pool, the enemy hazard pools (projectiles, puddles), the broad-phase
    /// grid, and every enemy-facing query in the game. Nothing else touches enemy instances
    /// directly.
    /// <para>
    /// Three enemy pools (Drifter/Spitter/Splitter) are addressed through one flat index space
    /// so the grid, AI tick, and separation pass can all iterate "every enemy" with one loop
    /// instead of three.
    /// </para>
    /// </summary>
    public sealed class EnemyManager : MonoBehaviour
    {
        [SerializeField] TuningConfig tuning;

        [SerializeField] DrifterAI drifterPrefab;
        [SerializeField] SpitterAI spitterPrefab;
        [SerializeField] SplitterAI splitterPrefab;

        /// <summary>Indexed by Generation.EnemyType: 0 Drifter, 1 Spitter, 2 Splitter.</summary>
        [SerializeField] EnemyDefinition[] definitions;

        [SerializeField] EnemyProjectile projectilePrefab;
        [SerializeField] Puddle puddlePrefab;
        [SerializeField] Transform poolParent;

        ObjectPool<EnemyBase>[] pools;
        int[] poolOffsets;   // length 4; poolOffsets[3] == flat capacity

        ObjectPool<EnemyProjectile> projectilePool;
        ObjectPool<Puddle> puddlePool;

        SpatialGrid grid;
        int[] queryBuffer;
        float maxEnemyRadius;

        public int AliveCount => pools[0].ActiveCount + pools[1].ActiveCount + pools[2].ActiveCount;

        /// <summary>Set from the RISK tier at run start (FR-483).</summary>
        public float RiskSpeedMultiplier { get; set; } = 1f;

        void Awake()
        {
            if (tuning == null) { tuning = ServiceLocator.Tuning; }

            maxEnemyRadius = tuning.Sim.maxEnemyRadius;

            pools = new ObjectPool<EnemyBase>[3];
            pools[(int)EnemyType.Drifter] = new ObjectPool<EnemyBase>(drifterPrefab, poolParent, tuning.Pools.drifter);
            pools[(int)EnemyType.Spitter] = new ObjectPool<EnemyBase>(spitterPrefab, poolParent, tuning.Pools.spitter);
            pools[(int)EnemyType.Splitter] = new ObjectPool<EnemyBase>(splitterPrefab, poolParent, tuning.Pools.splitter);

            poolOffsets = new int[4];
            poolOffsets[1] = poolOffsets[0] + pools[0].Capacity;
            poolOffsets[2] = poolOffsets[1] + pools[1].Capacity;
            poolOffsets[3] = poolOffsets[2] + pools[2].Capacity;

            projectilePool = new ObjectPool<EnemyProjectile>(projectilePrefab, poolParent, tuning.Pools.enemyProjectile);
            puddlePool = new ObjectPool<Puddle>(puddlePrefab, poolParent, tuning.Pools.puddle);

            grid = new SpatialGrid(tuning.Arena.floorRadius, tuning.Sim.gridCellSize, poolOffsets[3]);
            queryBuffer = new int[poolOffsets[3]];
        }

        /// <summary>Returns null when the target pool is exhausted. Callers must handle it (docs/01 §6).</summary>
        public EnemyBase Spawn(EnemyType type, Vector3 position, bool elite)
        {
            int poolIndex = (int)type;
            if (poolIndex < 0 || poolIndex >= pools.Length) { return null; }

            EnemyBase enemy = pools[poolIndex].Get();
            if (enemy == null) { return null; }

            enemy.Spawn(definitions[poolIndex], tuning, position, elite, RiskSpeedMultiplier);
            enemy.Owner = this;
            return enemy;
        }

        /// <summary>
        /// Runs AI, applies separation, then rebuilds the broad-phase grid. The grid is rebuilt
        /// AFTER movement so every query in the same fixed step sees current positions.
        /// </summary>
        public void Tick(float deltaTime, in Vector3 playerPosition)
        {
            int capacity = poolOffsets[3];

            for (int i = 0; i < capacity; i++)
            {
                if (!IsActiveAt(i)) { continue; }
                ItemAt(i).TickAI(deltaTime, playerPosition);
            }

            RebuildGrid();
            ApplySeparation(deltaTime);
            RebuildGrid();
        }

        /// <summary>
        /// Moves enemy projectiles and ages puddles, checking both against the player. Returns
        /// the contact damage the player should take this step, or 0. Loops the small hazard
        /// pools directly rather than through the enemy grid — there is only ever one target.
        /// </summary>
        public int AdvanceHazards(float deltaTime, in Vector3 playerPosition, float playerRadius)
        {
            int damage = 0;

            int projectileCapacity = projectilePool.Capacity;
            for (int i = 0; i < projectileCapacity; i++)
            {
                if (!projectilePool.IsActiveAt(i)) { continue; }

                EnemyProjectile projectile = projectilePool.ItemAt(i);
                projectile.LifeRemaining -= deltaTime;

                if (projectile.LifeRemaining <= 0f)
                {
                    projectilePool.Return(projectile);
                    continue;
                }

                Vector3 next = projectile.transform.position + projectile.Velocity * deltaTime;
                next.y = 0f;
                projectile.transform.position = next;

                if (CircleOverlap.Overlaps(next, projectile.Radius, playerPosition, playerRadius))
                {
                    if (projectile.Damage > damage) { damage = projectile.Damage; }
                    projectilePool.Return(projectile);
                }
            }

            int puddleCapacity = puddlePool.Capacity;
            for (int i = 0; i < puddleCapacity; i++)
            {
                if (!puddlePool.IsActiveAt(i)) { continue; }

                Puddle puddle = puddlePool.ItemAt(i);
                puddle.TimeRemaining -= deltaTime;

                if (puddle.TimeRemaining <= 0f)
                {
                    puddlePool.Return(puddle);
                    continue;
                }

                if (CircleOverlap.Overlaps(puddle.transform.position, puddle.Radius, playerPosition, playerRadius))
                {
                    if (puddle.Damage > damage) { damage = puddle.Damage; }
                }
            }

            return damage;
        }

        /// <summary>
        /// Damages the first enemy overlapping the circle and returns true if anything was hit —
        /// whether or not that hit killed it. A projectile consumes on the first overlap so it
        /// cannot punch through and damage an entire clump in one pass.
        /// </summary>
        public bool DamageCircle(in Vector3 center, float radius, int damage, bool fromExplosion)
        {
            int found = grid.Query(center.x, center.z, radius + maxEnemyRadius, queryBuffer);

            for (int q = 0; q < found; q++)
            {
                int flatIndex = queryBuffer[q];
                if (!IsActiveAt(flatIndex)) { continue; }

                EnemyBase enemy = ItemAt(flatIndex);
                if (!enemy.IsAlive) { continue; }

                Vector3 enemyPosition = enemy.transform.position;
                if (!CircleOverlap.Overlaps(center, radius, enemyPosition, enemy.HitboxRadius)) { continue; }

                if (enemy.TakeDamage(damage))
                {
                    Kill(enemy, enemyPosition, fromExplosion);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the contact damage the player should take from touching an enemy BODY this
        /// step, or 0. Deliberately does not de-duplicate across overlapping enemies — standing
        /// in a crowd still costs exactly one HP per invulnerability window, because that window
        /// lives in PlayerHealth, not here (FR-104).
        /// </summary>
        public int QueryPlayerContact(in Vector3 playerPosition, float playerRadius)
        {
            int found = grid.Query(playerPosition.x, playerPosition.z, playerRadius + maxEnemyRadius, queryBuffer);

            for (int q = 0; q < found; q++)
            {
                int flatIndex = queryBuffer[q];
                if (!IsActiveAt(flatIndex)) { continue; }

                EnemyBase enemy = ItemAt(flatIndex);
                if (!enemy.IsAlive) { continue; }

                if (CircleOverlap.Overlaps(playerPosition, playerRadius, enemy.transform.position, enemy.HitboxRadius))
                {
                    return enemy.ContactDamage;
                }
            }

            return 0;
        }

        /// <summary>Called by SpitterAI.TickAI. Silently no-ops if the projectile pool is full.</summary>
        public void FireProjectile(in Vector3 position, in Vector3 velocity, float lifetime, float radius, int damage)
        {
            EnemyProjectile projectile = projectilePool.Get();
            if (projectile == null) { return; }
            projectile.Launch(position, velocity, lifetime, radius, damage);
        }

        /// <summary>Called by SpitterAI.OnDeath. Silently no-ops if the puddle pool is full.</summary>
        public void SpawnPuddle(in Vector3 position, float duration, float radius, int damage)
        {
            Puddle puddle = puddlePool.Get();
            if (puddle == null) { return; }
            puddle.Activate(position, duration, radius, damage);
        }

        public void ReturnAll()
        {
            pools[0].ReturnAll();
            pools[1].ReturnAll();
            pools[2].ReturnAll();
            projectilePool.ReturnAll();
            puddlePool.ReturnAll();
        }

        void Kill(EnemyBase enemy, in Vector3 position, bool fromExplosion)
        {
            EnemyKilledArgs args = new EnemyKilledArgs(enemy.Type, enemy.IsElite, enemy.ScoreValue, position, fromExplosion);

            enemy.OnDeath(position);   // Spitter -> puddle, Splitter -> spawn Drifters
            pools[(int)enemy.Type].Return(enemy);
            GameEvents.RaiseEnemyKilled(args);
        }

        EnemyBase ItemAt(int flatIndex)
        {
            if (flatIndex < poolOffsets[1]) { return pools[0].ItemAt(flatIndex - poolOffsets[0]); }
            if (flatIndex < poolOffsets[2]) { return pools[1].ItemAt(flatIndex - poolOffsets[1]); }
            return pools[2].ItemAt(flatIndex - poolOffsets[2]);
        }

        bool IsActiveAt(int flatIndex)
        {
            if (flatIndex < poolOffsets[1]) { return pools[0].IsActiveAt(flatIndex - poolOffsets[0]); }
            if (flatIndex < poolOffsets[2]) { return pools[1].IsActiveAt(flatIndex - poolOffsets[1]); }
            return pools[2].IsActiveAt(flatIndex - poolOffsets[2]);
        }

        void RebuildGrid()
        {
            grid.BeginBuild();
            int capacity = poolOffsets[3];

            for (int i = 0; i < capacity; i++)
            {
                if (!IsActiveAt(i)) { continue; }
                Vector3 p = ItemAt(i).transform.position;
                grid.Add(i, p.x, p.z);
            }

            grid.EndBuild();
        }

        /// <summary>
        /// Cheap O(neighbours) push so a clump reads as a crowd rather than as one sprite.
        /// This is the "cheap overlap push" FR-301 permits — it is not steering and it must
        /// never become steering.
        /// </summary>
        void ApplySeparation(float deltaTime)
        {
            int capacity = poolOffsets[3];
            float twoR = tuning.Sim.separationRadius * 2f;
            float strengthScale = tuning.Sim.separationStrength;

            for (int i = 0; i < capacity; i++)
            {
                if (!IsActiveAt(i)) { continue; }

                Transform selfTransform = ItemAt(i).transform;
                Vector3 self = selfTransform.position;

                int found = grid.Query(self.x, self.z, twoR, queryBuffer);
                float pushX = 0f;
                float pushZ = 0f;

                for (int q = 0; q < found; q++)
                {
                    int other = queryBuffer[q];
                    if (other == i) { continue; }
                    if (!IsActiveAt(other)) { continue; }

                    Vector3 o = ItemAt(other).transform.position;
                    float dx = self.x - o.x;
                    float dz = self.z - o.z;
                    float sqr = dx * dx + dz * dz;

                    if (sqr >= twoR * twoR || sqr < 1e-6f) { continue; }

                    float dist = Mathf.Sqrt(sqr);
                    float strength = (twoR - dist) / twoR;
                    pushX += dx / dist * strength;
                    pushZ += dz / dist * strength;
                }

                if (pushX == 0f && pushZ == 0f) { continue; }

                selfTransform.position = new Vector3(
                    self.x + pushX * strengthScale * deltaTime,
                    0f,
                    self.z + pushZ * strengthScale * deltaTime);
            }
        }
    }
}
