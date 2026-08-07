using UnityEngine;
using Overdraft.Generation;

namespace Overdraft.Enemies
{
    /// <summary>
    /// Every enemy stat lives here, never in an AI script (FR-305). Elite is NOT an asset — it
    /// is a bool on the pooled instance, with the multipliers coming from TuningConfig
    /// (docs/02 §2).
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Overdraft/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        public EnemyType type;
        public int maxHP;
        public float moveSpeed;
        public float hitboxRadius;
        public int scoreValue;
        public int budgetCost;
        public int contactDamage;
        public Color bodyColor = Color.white;

        [Header("Spitter")]
        public float preferredRange;
        public float fireInterval;
        public float projectileSpeed;
        public float puddleDuration;
        public float puddleRadius;

        [Header("Splitter")]
        public float approachSpeed;      // NEW — docs/00_TUNING.md lists two speeds for Splitter
                                          // ("6.5 charge / 1.5 approach"); moveSpeed above is charge speed
        public float windupDuration;
        public float chargeDuration;
        public float chargeCooldown;     // NEW — pause between approach cycles after a charge ends
        public int splitCount;
        public float splitScatterRadius;
    }
}
