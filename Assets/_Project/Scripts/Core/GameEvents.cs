using System;

namespace Overdraft.Core
{
    /// <summary>
    /// Static, allocation-free game event bus (docs/01 §3). This plus ScriptableObjects plus
    /// pools is the entire architecture — there is no DI container and no framework.
    /// <para>
    /// Rules: subscribe in OnEnable, unsubscribe in OnDisable, always both. Handlers must not
    /// throw, must not subscribe or unsubscribe during dispatch, and must not raise other
    /// events more than one level deep.
    /// </para>
    /// </summary>
    public static class GameEvents
    {
        // --- Combat ---
        public static event Action<EnemyKilledArgs> EnemyKilled;
        public static event Action<PlayerHitArgs> PlayerHit;
        public static event Action PlayerDied;
        public static event Action PlayerDashed;
        public static event Action PerfectDash;

        // --- Economy ---
        public static event Action<float> MultiplierChanged;
        public static event Action<long> ChargeChanged;
        public static event Action<long> ScoreChanged;
        public static event Action BankStarted;
        public static event Action<float> BankProgress;
        public static event Action<long> BankCompleted;
        public static event Action<bool> BankCancelled;

        // --- Run flow ---
        public static event Action<int> WaveStarted;
        public static event Action<int> WaveCleared;
        public static event Action<EnvironmentId> EnvironmentChanged;
        public static event Action BossSpawned;
        public static event Action<int> BossPhaseChanged;
        public static event Action BossDefeated;
        public static event Action<RunResult> RunEnded;
        public static event Action RunRestarted;
        public static event Action<UpgradeId> UpgradeTaken;

        // Raise methods are the ONLY way to fire. Null-conditional, no allocation.
        public static void RaiseEnemyKilled(in EnemyKilledArgs a) => EnemyKilled?.Invoke(a);
        public static void RaisePlayerHit(in PlayerHitArgs a) => PlayerHit?.Invoke(a);
        public static void RaisePlayerDied() => PlayerDied?.Invoke();
        public static void RaisePlayerDashed() => PlayerDashed?.Invoke();
        public static void RaisePerfectDash() => PerfectDash?.Invoke();

        public static void RaiseMultiplierChanged(float value) => MultiplierChanged?.Invoke(value);
        public static void RaiseChargeChanged(long value) => ChargeChanged?.Invoke(value);
        public static void RaiseScoreChanged(long value) => ScoreChanged?.Invoke(value);
        public static void RaiseBankStarted() => BankStarted?.Invoke();
        public static void RaiseBankProgress(float progress01) => BankProgress?.Invoke(progress01);
        public static void RaiseBankCompleted(long amount) => BankCompleted?.Invoke(amount);
        public static void RaiseBankCancelled(bool interrupted) => BankCancelled?.Invoke(interrupted);

        public static void RaiseWaveStarted(int wave) => WaveStarted?.Invoke(wave);
        public static void RaiseWaveCleared(int wave) => WaveCleared?.Invoke(wave);
        public static void RaiseEnvironmentChanged(EnvironmentId id) => EnvironmentChanged?.Invoke(id);
        public static void RaiseBossSpawned() => BossSpawned?.Invoke();
        public static void RaiseBossPhaseChanged(int phase) => BossPhaseChanged?.Invoke(phase);
        public static void RaiseBossDefeated() => BossDefeated?.Invoke();
        public static void RaiseRunEnded(in RunResult result) => RunEnded?.Invoke(result);
        public static void RaiseRunRestarted() => RunRestarted?.Invoke();
        public static void RaiseUpgradeTaken(in UpgradeId id) => UpgradeTaken?.Invoke(id);

        /// <summary>
        /// Drops every subscriber. Called by Bootstrap on teardown and, critically, on domain
        /// reload in the editor: Unity 2022 ships with "Reload Domain" off by default, so static
        /// event fields survive between play sessions and will fire into destroyed objects.
        /// This is the single most common way this architecture goes wrong.
        /// </summary>
        public static void ClearAll()
        {
            EnemyKilled = null;
            PlayerHit = null;
            PlayerDied = null;
            PlayerDashed = null;
            PerfectDash = null;

            MultiplierChanged = null;
            ChargeChanged = null;
            ScoreChanged = null;
            BankStarted = null;
            BankProgress = null;
            BankCompleted = null;
            BankCancelled = null;

            WaveStarted = null;
            WaveCleared = null;
            EnvironmentChanged = null;
            BossSpawned = null;
            BossPhaseChanged = null;
            BossDefeated = null;
            RunEnded = null;
            RunRestarted = null;
            UpgradeTaken = null;
        }

#if UNITY_EDITOR
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticsOnEnterPlayMode()
        {
            // Runs before any scene loads, including when domain reload is disabled.
            ClearAll();
        }
#endif
    }
}
