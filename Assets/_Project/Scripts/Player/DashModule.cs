using UnityEngine;
using Overdraft.Core;
using Overdraft.Economy;
using Overdraft.Utility;

namespace Overdraft.Player
{
    /// <summary>
    /// Fixed-distance dash with i-frames and two independently recharging charges (FR-106).
    /// <para>
    /// The charges recharge on separate timers, not a shared one. That difference is what lets a
    /// player spend both to escape a clump and still have one back a moment later — a shared
    /// timer makes the second charge feel like a lie.
    /// </para>
    /// </summary>
    public sealed class DashModule : MonoBehaviour
    {
        [SerializeField] TuningConfig tuning;
        [SerializeField] BankSystem bank;
        [SerializeField] MultiplierSystem multiplier;

        float[] cooldowns;
        Vector3 startPosition;
        Vector3 direction;
        float elapsed;
        float iframeTimer;
        bool perfectAwardedThisDash;

        public bool IsDashing { get; private set; }
        public bool IsInvulnerable => iframeTimer > 0f;

        public int ChargesAvailable
        {
            get
            {
                int count = 0;
                for (int i = 0; i < cooldowns.Length; i++)
                {
                    if (cooldowns[i] <= 0f) { count++; }
                }
                return count;
            }
        }

        void Awake()
        {
            if (tuning == null) { tuning = ServiceLocator.Tuning; }
            if (tuning != null) { AllocateCharges(); }
        }

        internal void Configure(TuningConfig config, BankSystem bankSystem, MultiplierSystem multiplierSystem)
        {
            tuning = config;
            bank = bankSystem;
            multiplier = multiplierSystem;
            AllocateCharges();
        }

        void AllocateCharges()
        {
            cooldowns = new float[Mathf.Max(1, tuning.Dash.maxCharges)];
        }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < cooldowns.Length; i++)
            {
                if (cooldowns[i] <= 0f) { continue; }
                cooldowns[i] -= deltaTime;
                if (cooldowns[i] < 0f) { cooldowns[i] = 0f; }
            }

            if (iframeTimer > 0f)
            {
                iframeTimer -= deltaTime;
                if (iframeTimer < 0f) { iframeTimer = 0f; }
            }
        }

        /// <summary>
        /// Returns true if the dash started. Refused while banking (FR-106) — UNLESS LEECH T2
        /// is owned, in which case the dash is allowed and costs some bank hold progress instead
        /// of being blocked outright (QA case 5).
        /// </summary>
        public bool TryDash(in Vector3 requestedDirection)
        {
            if (IsDashing) { return false; }

            bool leechAllowsDashWhileBanking = bank.IsBanking && bank.LeechTier >= 2;
            if (bank.IsBanking && !leechAllowsDashWhileBanking) { return false; }

            int slot = -1;
            for (int i = 0; i < cooldowns.Length; i++)
            {
                if (cooldowns[i] <= 0f) { slot = i; break; }
            }
            if (slot < 0) { return false; }

            Vector3 dir = MathX.NormalizeXZ(requestedDirection);
            if (dir == Vector3.zero) { return false; }

            if (leechAllowsDashWhileBanking)
            {
                bank.RefundProgress(tuning.Bank.leechT2DashRefund);
            }

            cooldowns[slot] = tuning.Dash.chargeCooldown;
            direction = dir;
            startPosition = transform.position;
            elapsed = 0f;
            iframeTimer = tuning.Dash.iframeDuration;
            perfectAwardedThisDash = false;
            IsDashing = true;

            GameEvents.RaisePlayerDashed();
            return true;
        }

        /// <summary>
        /// Advances the dash and returns the new position. Called by PlayerController, which
        /// owns the transform — two scripts writing the same position is how movement bugs start.
        /// </summary>
        public Vector3 Advance(float deltaTime, in Vector3 currentPosition)
        {
            if (!IsDashing) { return currentPosition; }

            elapsed += deltaTime;
            float duration = tuning.Dash.duration;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

            float travelled = tuning.Dash.distance * MathX.EaseOutCubic(t);
            Vector3 next = startPosition + direction * travelled;
            next.y = 0f;

            if (t >= 1f) { IsDashing = false; }
            return next;
        }

        /// <summary>
        /// Called when an enemy projectile passes through the i-frame window (FR-107).
        /// Awards at most once per dash regardless of how many projectiles are involved.
        /// </summary>
        public void NotifyPerfectDashCandidate()
        {
            if (!IsInvulnerable) { return; }
            if (perfectAwardedThisDash) { return; }

            perfectAwardedThisDash = true;
            multiplier.AddFlat(tuning.Dash.perfectMultBonus);
            GameEvents.RaisePerfectDash();
        }

        public void ResetForRun()
        {
            for (int i = 0; i < cooldowns.Length; i++) { cooldowns[i] = 0f; }
            IsDashing = false;
            iframeTimer = 0f;
            elapsed = 0f;
            perfectAwardedThisDash = false;
        }

        /// <summary>Granted by LEECH T3 on a completed bank.</summary>
        public void GrantIframes(float duration)
        {
            if (duration > iframeTimer) { iframeTimer = duration; }
        }
    }
}
