using UnityEngine;

namespace Overdraft.Core
{
    /// <summary>
    /// The single source of truth for every tunable number in the game (CLAUDE.md C9).
    /// Exactly one asset instance exists: Assets/_Project/Data/TuningConfig.asset.
    /// Values and their rationale live in docs/00_TUNING.md — change them there too.
    /// </summary>
    [CreateAssetMenu(fileName = "TuningConfig", menuName = "Overdraft/Tuning Config")]
    public sealed class TuningConfig : ScriptableObject
    {
        public PlayerTuning Player;
        public WeaponTuning Weapon;
        public DashTuning Dash;
        public EconomyTuning Economy;
        public BankTuning Bank;
        public WaveTuning Wave;
        public BossTuning Boss;
        public ArenaTuning Arena;
        public EliteTuning Elite;
        public PoolTuning Pools;
        public SimTuning Sim;
        public EnemyCombatTuning EnemyCombat;
        public FeelTuning Feel;
        public RiskTier[] RiskTiers = new RiskTier[5];
        public long[] CreditRankThresholds = new long[12];

        /// <summary>Safe indexed access to a risk tier; clamps rather than throwing mid-run.</summary>
        public RiskTier Risk(int tierIndex)
        {
            if (RiskTiers == null || RiskTiers.Length == 0)
            {
                return RiskTier.Identity;
            }
            int i = Mathf.Clamp(tierIndex, 0, RiskTiers.Length - 1);
            return RiskTiers[i];
        }
    }

    [System.Serializable]
    public struct PlayerTuning
    {
        public float moveSpeed;
        public float acceleration;
        public float deceleration;
        public float facingTurnSpeed;
        public float hitboxRadius;
        public float visualRadius;
        public int maxHP;
        public float hitInvulnDuration;
        public float invulnBlinkHz;
        public float chargeLossOnHit;
        public float deathBeatDuration;
    }

    [System.Serializable]
    public struct WeaponTuning
    {
        public float fireInterval;
        public float projectileSpeed;
        public float projectileRadius;
        public float projectileLifetime;
        public int projectileDamage;
        public float baseSpreadDegrees;
        public int poolSize;
    }

    [System.Serializable]
    public struct DashTuning
    {
        public float distance;
        public float duration;
        public float iframeDuration;
        public float chargeCooldown;
        public int maxCharges;
        public float perfectWindow;
        public float perfectMultBonus;
        public float perfectHitstop;
    }

    [System.Serializable]
    public struct EconomyTuning
    {
        public float multStart;
        public float multPerKill;
        public float multMax;
        public float multDecayGrace;
        public float multDecayRate;
        public float multWarnThreshold;
        public float multMin;
    }

    [System.Serializable]
    public struct BankTuning
    {
        public float duration;
        public float fireRateMultiplier;
        public float interruptCooldown;
        public float completeHitstop;
        public float leechT1Duration;
        public float leechT2Duration;
        public float leechT3Duration;
        public float leechT3IframeDuration;
        public float leechT2DashRefund;
    }

    [System.Serializable]
    public struct WaveTuning
    {
        public float budgetBase;
        public float budgetExponent;
        public float budgetScale;
        public float waveTimeout;
        public float spawnRingMin;
        public float spawnRingMax;
        public float interWaveDelay;
        public float introDuration;
        public int upgradeOffersShown;

        // Weighted-selection inputs for WaveGenerator (FR-453). Cost mirrors the matching
        // EnemyDefinition.budgetCost asset — keep the two in sync when either changes.
        public int drifterWeight;
        public int spitterWeight;
        public int splitterWeight;
        public int drifterCost;
        public int spitterCost;
        public int splitterCost;

        /// <summary>Chance (0-100) that an eligible pick becomes an elite. Wave 5+ only (FR-304).</summary>
        public int eliteWeightPercent;

        // M1 only: the endless trickle that stands in for waves until M2-T04 lands.
        public float m1SpawnInterval;
        public float m1SpawnIntervalMin;
        public float m1SpawnRampPerSecond;
        public int m1MaxAlive;
    }

    [System.Serializable]
    public struct BossTuning
    {
        public int maxHP;
        public float phase2Threshold;
        public float phase3Threshold;
        public float phaseTransitionInvuln;
        public float laserRotationSpeed;
        public int laserSpokes;
        public float laserWidth;
        public float drifterSpawnInterval;
        public float auditDrainRate;
        public float auditWindowDuration;
        public float auditInterval;
        public int fragmentCount;
        public int fragmentHP;
        public float fragmentOrbitSpeed;
        public float fragmentOrbitRadius;
        public float victorySlowMoDuration;
    }

    [System.Serializable]
    public struct ArenaTuning
    {
        public float floorRadius;
        public float vaultRadius;
        public float floorOrthoSize;
        public float vaultOrthoSize;
        public float environmentLerpTime;
        public float hazardCycleDuration;
        public int hazardBarCount;
        public float hazardBarWidth;
        public float hazardTelegraphTime;
    }

    /// <summary>
    /// Pool capacities. These are hard ceilings, not hints — a pool that is exhausted returns
    /// null and the spawn is skipped, which is far cheaper to debug than a pool that grows.
    /// </summary>
    [System.Serializable]
    public struct PoolTuning
    {
        public int drifter;
        public int spitter;
        public int splitter;
        public int enemyProjectile;
        public int puddle;
    }

    /// <summary>Broad-phase and crowd-shaping constants for the fixed-step simulation.</summary>
    [System.Serializable]
    public struct SimTuning
    {
        public float gridCellSize;
        public float separationRadius;
        public float separationStrength;

        /// <summary>Query padding. Must be >= the largest possible enemy hitbox radius.</summary>
        public float maxEnemyRadius;
    }

    /// <summary>
    /// Cross-enemy combat constants that aren't specific to one EnemyDefinition — currently just
    /// the shared projectile hitbox/lifetime SPITTER fires with (docs/02's EnemyDefinition schema
    /// has projectileSpeed but no lifetime/radius; both are needed to pool an enemy projectile).
    /// </summary>
    [System.Serializable]
    public struct EnemyCombatTuning
    {
        public float projectileLifetime;
        public float projectileRadius;
    }

    /// <summary>
    /// Elite modifiers. Elite is a flag on a pooled instance, never a separate prefab or asset
    /// (FR-304), so its multipliers live here rather than in an EnemyDefinition.
    /// </summary>
    [System.Serializable]
    public struct EliteTuning
    {
        public float hpMultiplier;
        public float speedMultiplier;
        public float valueMultiplier;
        public float budgetMultiplier;
        public float scaleMultiplier;
        public float outlineWidth;
        public float emissionBoost;
    }

    [System.Serializable]
    public struct FeelTuning
    {
        public float hitstopOnKill;
        public float hitstopOnPlayerHit;
        public float hitstopScale;

        public float traumaOnKill;
        public float traumaOnPlayerHit;
        public float traumaOnBankComplete;
        public float traumaOnBossPhase;
        public float traumaDecayRate;
        public float shakeMaxOffset;
        public float shakeMaxRotation;
        public float shakeFrequency;

        public float zoomPunchAmount;
        public float zoomPunchDuration;
        public float cameraFollowLerp;
        public float cameraAimLead;

        public float caBase;
        public float caOnHit;
        public float caDuringBank;
        public float vignetteBase;
        public float vignetteDuringBank;
        public float bankDesaturation;
    }

    [System.Serializable]
    public struct RiskTier
    {
        public string id;
        public float enemySpeedMultiplier;
        public float enemyBudgetMultiplier;
        public float scoreMultiplier;

        public static RiskTier Identity => new RiskTier
        {
            id = "RISK_I",
            enemySpeedMultiplier = 1f,
            enemyBudgetMultiplier = 1f,
            scoreMultiplier = 1f
        };
    }
}
