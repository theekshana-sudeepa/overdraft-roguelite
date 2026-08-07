using Overdraft.Generation;

namespace Overdraft.Core
{
    /// <summary>The single source of truth for "what is happening" (docs/01 §5).</summary>
    public enum RunState
    {
        Intro,
        WaveActive,
        WaveCleared,
        UpgradeChoice,
        BossIntro,
        BossActive,
        BossDefeated,
        Death,
        Results
    }

    /// <summary>Which arena root is live. Swapped by SetActive, never by a scene load (FR-451).</summary>
    public enum EnvironmentId
    {
        Floor = 0,
        Vault = 1
    }

    /// <summary>A specific offer: a line plus the tier it would grant (1..3). UpgradeLine lives
    /// in Generation/ (CLAUDE.md §6) since UpgradeOfferGenerator, a pure function, needs it.</summary>
    public readonly struct UpgradeId
    {
        public readonly Generation.UpgradeLine Line;
        public readonly int Tier;

        public UpgradeId(Generation.UpgradeLine line, int tier)
        {
            Line = line;
            Tier = tier;
        }
    }

    /// <summary>What the results screen and the leaderboard blob are built from.</summary>
    public readonly struct RunResult
    {
        public readonly long BankedScore;
        public readonly long UnbankedChargeLost;
        public readonly float DurationSeconds;
        public readonly int Kills;
        public readonly int Banks;
        public readonly float HighestMultiplier;
        public readonly bool BossDefeated;

        public RunResult(
            long bankedScore,
            long unbankedChargeLost,
            float durationSeconds,
            int kills,
            int banks,
            float highestMultiplier,
            bool bossDefeated)
        {
            BankedScore = bankedScore;
            UnbankedChargeLost = unbankedChargeLost;
            DurationSeconds = durationSeconds;
            Kills = kills;
            Banks = banks;
            HighestMultiplier = highestMultiplier;
            BossDefeated = bossDefeated;
        }
    }

    /// <summary>Payload for GameEvents.EnemyKilled.</summary>
    public readonly struct EnemyKilledArgs
    {
        public readonly EnemyType Type;
        public readonly bool IsElite;
        public readonly int BaseValue;
        public readonly UnityEngine.Vector3 Position;

        /// <summary>Set when the kill came from a VOLATILE explosion, so chains cannot recurse forever.</summary>
        public readonly bool FromExplosion;

        public EnemyKilledArgs(EnemyType type, bool isElite, int baseValue, UnityEngine.Vector3 position, bool fromExplosion)
        {
            Type = type;
            IsElite = isElite;
            BaseValue = baseValue;
            Position = position;
            FromExplosion = fromExplosion;
        }
    }

    /// <summary>Payload for GameEvents.PlayerHit.</summary>
    public readonly struct PlayerHitArgs
    {
        public readonly int RemainingHP;
        public readonly long ChargeDestroyed;
        public readonly UnityEngine.Vector3 Position;
        public readonly bool InterruptedBank;

        public PlayerHitArgs(int remainingHP, long chargeDestroyed, UnityEngine.Vector3 position, bool interruptedBank)
        {
            RemainingHP = remainingHP;
            ChargeDestroyed = chargeDestroyed;
            Position = position;
            InterruptedBank = interruptedBank;
        }
    }
}
