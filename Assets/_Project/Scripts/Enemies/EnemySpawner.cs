using UnityEngine;
using Overdraft.Core;
using Overdraft.Utility;

namespace Overdraft.Enemies
{
    /// <summary>
    /// M1 spawner: an endless ramping trickle of Drifters on the spawn ring.
    /// <para>
    /// This is scaffolding for the Day-3 gate, not the shipping system. In M2 it is replaced by
    /// the seeded WaveGenerator (M2-T04), which produces a list of SpawnEvents with authored
    /// clump patterns — uniform spawning is explicitly forbidden in the real thing (FR-454)
    /// because clumps are what create the multiplier spikes.
    /// </para>
    /// </summary>
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] TuningConfig tuning;
        [SerializeField] EnemyManager enemies;

        DeterministicRandom rng;
        float timer;
        float interval;
        float elapsed;

        public bool IsSpawning { get; set; }

        void Awake()
        {
            if (tuning == null) { tuning = ServiceLocator.Tuning; }
        }

        public void ResetForRun(int seed)
        {
            rng = new DeterministicRandom(seed);
            interval = tuning.Wave.m1SpawnInterval;
            timer = interval;
            elapsed = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (!IsSpawning) { return; }

            elapsed += deltaTime;

            float floorInterval = tuning.Wave.m1SpawnIntervalMin;
            interval = tuning.Wave.m1SpawnInterval - tuning.Wave.m1SpawnRampPerSecond * elapsed;
            if (interval < floorInterval) { interval = floorInterval; }

            timer -= deltaTime;
            if (timer > 0f) { return; }

            timer += interval;
            if (timer < 0f) { timer = 0f; }

            if (enemies.AliveCount >= tuning.Wave.m1MaxAlive) { return; }

            SpawnOne();
        }

        void SpawnOne()
        {
            float angle = rng.Range(0f, Mathf.PI * 2f);
            float radius = rng.Range(tuning.Wave.spawnRingMin, tuning.Wave.spawnRingMax);

            // Clamped inside the arena so a spawn on the ring is never born outside the
            // boundary the player is clamped to.
            float maxRadius = tuning.Arena.floorRadius - tuning.Sim.maxEnemyRadius;
            if (radius > maxRadius) { radius = maxRadius; }

            Vector3 position = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);

            // Null means the pool is exhausted. Skip silently — a growing pool would hide the
            // bug until it became a framerate bug (docs/01 §6).
            enemies.Spawn(Overdraft.Generation.EnemyType.Drifter, position, false);
        }
    }
}
