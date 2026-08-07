using UnityEngine;
using Overdraft.Core;
using Overdraft.Enemies;
using Overdraft.Generation;
using Overdraft.Player;

namespace Overdraft.Economy
{
    /// <summary>
    /// Owns which upgrade tier the player has taken per line, and applies each tier's numeric
    /// effect to the systems it touches (FR-471 – FR-474).
    /// <para>
    /// Tiers are stored as ABSOLUTE values per line, not deltas stacked across tiers — the same
    /// idiom BankTuning's leechT1/2/3Duration already uses. Taking a tier re-applies that whole
    /// line's effect from scratch rather than adding to whatever the previous tier set.
    /// </para>
    /// </summary>
    public sealed class UpgradeSystem : MonoBehaviour
    {
        [SerializeField] TuningConfig tuning;
        [SerializeField] Weapon weapon;
        [SerializeField] BankSystem bank;
        [SerializeField] MultiplierSystem multiplier;
        [SerializeField] PlayerHealth health;
        [SerializeField] EnemyManager enemies;

        readonly int[] tiers = new int[LineCount];

        float volatileRadius;
        float volatileDamageFraction;
        int volatileMaxChainDepth;
        int volatileChainDepthCounter;
        int volatileIterationsThisStep;

        /// <summary>GREED T3's "enemy values ×1.50" — read by RunController's kill handler.</summary>
        public float GreedValueMultiplier { get; private set; } = 1f;

        public int TierOf(UpgradeLine line) => tiers[(int)line];

        void OnEnable()
        {
            GameEvents.EnemyKilled += OnEnemyKilled;
        }

        void OnDisable()
        {
            GameEvents.EnemyKilled -= OnEnemyKilled;
        }

        /// <summary>Call once per fixed step so the VOLATILE chain-depth safety cap is per-step, not lifetime.</summary>
        public void Tick()
        {
            volatileIterationsThisStep = 0;
        }

        public void ResetForRun()
        {
            for (int i = 0; i < tiers.Length; i++) { tiers[i] = 0; }
            GreedValueMultiplier = 1f;
            volatileRadius = 0f;
            volatileDamageFraction = 0f;
            volatileMaxChainDepth = -1;   // -1 = VOLATILE not owned at all
            volatileChainDepthCounter = 0;
            volatileIterationsThisStep = 0;

            weapon.ProjectilesPerShot = 1;
            weapon.ConeDegrees = 0f;
            weapon.DamageMultiplier = 1f;
            bank.LeechTier = 0;
            multiplier.BonusPerKill = 0f;
            multiplier.BonusDecayRate = 0f;
            health.ChargeLossOverride = -1f;
        }

        /// <summary>Fills currentTiers (indexed by UpgradeLine) for UpgradeOfferGenerator, without allocating a new array per call.</summary>
        public void CopyTiersInto(int[] destination)
        {
            for (int i = 0; i < tiers.Length && i < destination.Length; i++)
            {
                destination[i] = tiers[i];
            }
        }

        public void Take(UpgradeLine line, int tier)
        {
            tiers[(int)line] = tier;

            switch (line)
            {
                case UpgradeLine.Spread: ApplySpread(tier); break;
                case UpgradeLine.Volatile: ApplyVolatile(tier); break;
                case UpgradeLine.Leech: ApplyLeech(tier); break;
                case UpgradeLine.Greed: ApplyGreed(tier); break;
            }

            GameEvents.RaiseUpgradeTaken(new UpgradeId(line, tier));
        }

        void ApplySpread(int tier)
        {
            switch (tier)
            {
                case 1:
                    weapon.ProjectilesPerShot = tuning.Upgrades.spreadT1Projectiles;
                    weapon.ConeDegrees = tuning.Upgrades.spreadT1ConeDegrees;
                    weapon.DamageMultiplier = 1f;
                    break;
                case 2:
                    weapon.ProjectilesPerShot = tuning.Upgrades.spreadT2Projectiles;
                    weapon.ConeDegrees = tuning.Upgrades.spreadT2ConeDegrees;
                    weapon.DamageMultiplier = 1f;
                    break;
                case 3:
                    weapon.ProjectilesPerShot = tuning.Upgrades.spreadT3Projectiles;
                    weapon.ConeDegrees = tuning.Upgrades.spreadT3ConeDegrees;
                    weapon.DamageMultiplier = tuning.Upgrades.spreadT3DamageMultiplier;
                    break;
            }
        }

        void ApplyVolatile(int tier)
        {
            switch (tier)
            {
                case 1:
                    volatileRadius = tuning.Upgrades.volatileT1Radius;
                    volatileDamageFraction = tuning.Upgrades.volatileT1DamageFraction;
                    volatileMaxChainDepth = 0;   // explosions from real kills only, never chain
                    break;
                case 2:
                    volatileRadius = tuning.Upgrades.volatileT2Radius;
                    volatileDamageFraction = tuning.Upgrades.volatileT1DamageFraction;
                    volatileMaxChainDepth = tuning.Upgrades.volatileT2ChainDepth;
                    break;
                case 3:
                    volatileRadius = tuning.Upgrades.volatileT2Radius;
                    volatileDamageFraction = tuning.Upgrades.volatileT1DamageFraction;
                    volatileMaxChainDepth = int.MaxValue;   // safety-capped per step, not per line
                    break;
            }
        }

        void ApplyLeech(int tier)
        {
            bank.LeechTier = tier;
        }

        void ApplyGreed(int tier)
        {
            switch (tier)
            {
                case 1:
                    multiplier.BonusPerKill = tuning.Upgrades.greedT1MultPerKillBonus;
                    multiplier.BonusDecayRate = tuning.Upgrades.greedT1DecayRateBonus;
                    break;
                case 2:
                    multiplier.BonusPerKill = tuning.Upgrades.greedT2MultPerKillBonus;
                    multiplier.BonusDecayRate = tuning.Upgrades.greedT2DecayRateBonus;
                    multiplier.RemoveCap();
                    break;
                case 3:
                    GreedValueMultiplier = tuning.Upgrades.greedT3ValueMultiplier;
                    health.ChargeLossOverride = 1.00f;
                    break;
            }
        }

        /// <summary>
        /// VOLATILE (FR-473): a real kill triggers one explosion; at T2 a kill produced BY that
        /// explosion can trigger one more; at T3 the chain is effectively unbounded, guarded only
        /// by a per-step iteration cap (docs/00: "capped at 40 iterations per frame").
        /// </summary>
        void OnEnemyKilled(EnemyKilledArgs args)
        {
            if (volatileMaxChainDepth < 0) { return; }
            if (volatileIterationsThisStep >= MaxVolatileIterationsPerStep) { return; }
            if (volatileChainDepthCounter > volatileMaxChainDepth) { return; }

            volatileIterationsThisStep++;
            volatileChainDepthCounter++;
            try
            {
                int damage = Mathf.Max(1, Mathf.RoundToInt(tuning.Weapon.projectileDamage * volatileDamageFraction));
                enemies.DamageCircle(args.Position, volatileRadius, damage, fromExplosion: true);
            }
            finally
            {
                volatileChainDepthCounter--;
            }
        }

        const int LineCount = 4;
        const int MaxVolatileIterationsPerStep = 40;
    }
}
