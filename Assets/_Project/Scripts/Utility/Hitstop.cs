using UnityEngine;
using Overdraft.Core;

namespace Overdraft.Utility
{
    /// <summary>
    /// Freezes gameplay by collapsing Time.timeScale for an UNSCALED duration (docs/04 §5).
    /// <para>
    /// Because all gameplay lives in FixedUpdate and all VFX/audio live in Update, this freezes
    /// the world while particles and sound keep running. That contrast is the impact — it is
    /// free, and it is why M1-T06 happens now rather than in the juice milestone.
    /// </para>
    /// Requests stack by taking the MAX remaining duration, never by summing: summing turns a
    /// wave-6 kill cascade into a multi-second lockup.
    /// </summary>
    public sealed class Hitstop : MonoBehaviour
    {
        [SerializeField] TuningConfig tuning;

        float remaining;
        bool frozen;

        public bool IsFrozen => frozen;

        void OnEnable()
        {
            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.PlayerHit += OnPlayerHit;
            GameEvents.BankCompleted += OnBankCompleted;
            GameEvents.PerfectDash += OnPerfectDash;
        }

        void OnDisable()
        {
            GameEvents.EnemyKilled -= OnEnemyKilled;
            GameEvents.PlayerHit -= OnPlayerHit;
            GameEvents.BankCompleted -= OnBankCompleted;
            GameEvents.PerfectDash -= OnPerfectDash;
            Release();
        }

        void Update()
        {
            if (!frozen) { return; }

            remaining -= Time.unscaledDeltaTime;
            if (remaining <= 0f)
            {
                Release();
            }
        }

        public void Freeze(float unscaledDuration)
        {
            if (unscaledDuration <= 0f) { return; }
            if (unscaledDuration > remaining) { remaining = unscaledDuration; }

            if (!frozen)
            {
                frozen = true;
                Time.timeScale = tuning.Feel.hitstopScale;
            }
        }

        void Release()
        {
            remaining = 0f;
            if (!frozen) { return; }
            frozen = false;
            Time.timeScale = 1f;
        }

        void OnEnemyKilled(EnemyKilledArgs args) => Freeze(tuning.Feel.hitstopOnKill);
        void OnPlayerHit(PlayerHitArgs args) => Freeze(tuning.Feel.hitstopOnPlayerHit);
        void OnBankCompleted(long amount) => Freeze(tuning.Bank.completeHitstop);
        void OnPerfectDash() => Freeze(tuning.Dash.perfectHitstop);
    }
}
