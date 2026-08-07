using UnityEngine;
using Overdraft.Core;

namespace Overdraft.Economy
{
    /// <summary>
    /// ★ The whole game (FR-205 – FR-209).
    /// <para>
    /// Hold the bank input for EffectiveDuration seconds while completely immobile and CHARGE
    /// becomes SCORE. Release early and nothing happens — no credit, no penalty. Take a hit and
    /// you lose the normal penalty AND get locked out. Every other system in this project exists
    /// to make this 1.2 seconds feel expensive.
    /// </para>
    /// </summary>
    public sealed class BankSystem : MonoBehaviour
    {
        [SerializeField] TuningConfig tuning;
        [SerializeField] ChargeSystem charge;
        [SerializeField] MultiplierSystem multiplier;
        [SerializeField] ScoreSystem score;

        float holdTimer;
        float lockoutTimer;
        bool inputHeld;

        public bool IsBanking { get; private set; }
        public float Progress01 { get; private set; }
        public bool IsLockedOut => lockoutTimer > 0f;

        /// <summary>Set by the RISK tier at run start. Applied to every completed bank.</summary>
        public float RiskScoreMultiplier { get; set; } = 1f;

        /// <summary>0 = none, 1..3 = LEECH tier owned. Shortens the hold window.</summary>
        public int LeechTier { get; set; }

        /// <summary>Seconds of i-frames granted on completion. Non-zero only at LEECH T3.</summary>
        public float CompletionIframes => LeechTier >= 3 ? tuning.Bank.leechT3IframeDuration : 0f;

        public float EffectiveDuration
        {
            get
            {
                switch (LeechTier)
                {
                    case 1: return tuning.Bank.leechT1Duration;
                    case 2: return tuning.Bank.leechT2Duration;
                    case 3: return tuning.Bank.leechT3Duration;
                    default: return tuning.Bank.duration;
                }
            }
        }

        void Awake()
        {
            if (tuning == null) { tuning = ServiceLocator.Tuning; }
        }

        internal void Configure(TuningConfig config, ChargeSystem chargeSystem, MultiplierSystem multiplierSystem, ScoreSystem scoreSystem)
        {
            tuning = config;
            charge = chargeSystem;
            multiplier = multiplierSystem;
            score = scoreSystem;
        }

        /// <summary>
        /// Called from the input layer every frame with the current held state. The hold timer
        /// deliberately lives here rather than in an input interaction so it can be tuned,
        /// interrupted, and unit-tested.
        /// </summary>
        public void OnBankInputChanged(bool held)
        {
            inputHeld = held;
        }

        /// <summary>
        /// Advances lockout and hold. Driven from RunController.FixedUpdate so the bank window
        /// is measured in deterministic 60 Hz steps and never in variable frame time.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (lockoutTimer > 0f)
            {
                lockoutTimer -= deltaTime;
                if (lockoutTimer < 0f) { lockoutTimer = 0f; }
            }

            if (!inputHeld)
            {
                if (IsBanking) { Cancel(false); }
                return;
            }

            if (!IsBanking)
            {
                if (lockoutTimer > 0f) { return; }
                Begin();
            }

            holdTimer += deltaTime;

            float duration = EffectiveDuration;
            Progress01 = duration > 0f ? Mathf.Clamp01(holdTimer / duration) : 1f;
            GameEvents.RaiseBankProgress(Progress01);

            if (holdTimer >= duration)
            {
                Complete();
            }
        }

        /// <summary>Damage landed mid-bank (FR-208). The hit penalty itself is PlayerHealth's job.</summary>
        public void Interrupt()
        {
            if (!IsBanking) { return; }

            Cancel(true);
            lockoutTimer = tuning.Bank.interruptCooldown;
        }

        /// <summary>
        /// Boss-defeat free bank (FR-404). Completes immediately with no vulnerability window
        /// and no input required.
        /// </summary>
        public void ForceComplete()
        {
            if (!IsBanking)
            {
                IsBanking = true;
                GameEvents.RaiseBankStarted();
            }
            Complete();
        }

        /// <summary>Death and pause both need the hold dropped without a lockout.</summary>
        public void AbortSilently()
        {
            inputHeld = false;
            if (IsBanking) { Cancel(false); }
        }

        public void ResetForRun()
        {
            inputHeld = false;
            IsBanking = false;
            holdTimer = 0f;
            lockoutTimer = 0f;
            Progress01 = 0f;
            LeechTier = 0;
        }

        void Begin()
        {
            IsBanking = true;
            holdTimer = 0f;
            Progress01 = 0f;
            GameEvents.RaiseBankStarted();
        }

        void Cancel(bool interrupted)
        {
            IsBanking = false;
            holdTimer = 0f;
            Progress01 = 0f;
            GameEvents.RaiseBankCancelled(interrupted);
        }

        /// <summary>
        /// COMPLETION ORDER IS FIXED AND MUST NOT BE REORDERED (docs/01 §6).
        /// The HUD, the audio stinger and the achievement counters all read state that only
        /// exists correctly in this sequence.
        /// </summary>
        void Complete()
        {
            long amount = charge.Consume();                 // 1. take the pool
            long credited = score.AddBank(amount, RiskScoreMultiplier); // 2. bank it, risk-scaled
            multiplier.ResetToBase();                       // 3. the cost of banking

            IsBanking = false;
            holdTimer = 0f;
            Progress01 = 0f;

            GameEvents.RaiseBankCompleted(credited);        // 4. juice hangs off the event, not off here
        }
    }
}
