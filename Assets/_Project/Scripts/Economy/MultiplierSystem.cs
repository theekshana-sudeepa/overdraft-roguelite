using UnityEngine;
using Overdraft.Core;

namespace Overdraft.Economy
{
    /// <summary>
    /// MULT: rises on every kill, decays if the player stops killing (FR-202, FR-203).
    /// The decay is the clock that makes standing still to bank expensive.
    /// </summary>
    public sealed class MultiplierSystem : MonoBehaviour
    {
        [SerializeField] TuningConfig tuning;

        float current;
        float graceLeft;
        bool capped = true;

        public float Current => current;

        /// <summary>Seconds of grace remaining before decay starts. Zero means already decaying.</summary>
        public float GraceLeft => graceLeft;

        public bool IsWarning => graceLeft > 0f && graceLeft <= tuning.Economy.multWarnThreshold;

        /// <summary>Highest value reached this run. Feeds the results screen and RunProof.</summary>
        public float PeakThisRun { get; private set; }

        /// <summary>GREED T1/T2 add to this. Zero until the upgrade system lands (M2).</summary>
        public float BonusPerKill { get; set; }

        /// <summary>GREED T1/T2 add to this — faster gain is paid for with faster decay.</summary>
        public float BonusDecayRate { get; set; }

        void Awake()
        {
            if (tuning == null) { tuning = ServiceLocator.Tuning; }
            if (tuning != null) { ResetForRun(); }
        }

        internal void Configure(TuningConfig config)
        {
            tuning = config;
            ResetForRun();
        }

        public void AddKill()
        {
            AddFlat(tuning.Economy.multPerKill + BonusPerKill);
            graceLeft = tuning.Economy.multDecayGrace;
        }

        /// <summary>Flat add, used by Perfect Dash (FR-107). Does not refresh the grace window.</summary>
        public void AddFlat(float amount)
        {
            if (amount <= 0f) { return; }

            current += amount;
            if (capped && current > tuning.Economy.multMax)
            {
                current = tuning.Economy.multMax;
            }

            if (current > PeakThisRun) { PeakThisRun = current; }
            GameEvents.RaiseMultiplierChanged(current);
        }

        /// <summary>Called on a hit and on bank completion. Never below multMin.</summary>
        public void ResetToBase()
        {
            current = tuning.Economy.multMin;
            graceLeft = 0f;
            GameEvents.RaiseMultiplierChanged(current);
        }

        /// <summary>GREED T2 removes the ceiling entirely.</summary>
        public void RemoveCap()
        {
            capped = false;
        }

        /// <summary>
        /// Advances grace and decay. Driven explicitly from RunController.FixedUpdate rather
        /// than from a private FixedUpdate so the whole economy is deterministic and so the
        /// EditMode suite can step time without entering play mode.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (graceLeft > 0f)
            {
                graceLeft -= deltaTime;
                if (graceLeft < 0f) { graceLeft = 0f; }
                return;
            }

            float floorValue = tuning.Economy.multMin;
            if (current <= floorValue) { return; }

            float rate = tuning.Economy.multDecayRate + BonusDecayRate;
            current -= rate * deltaTime;
            if (current < floorValue) { current = floorValue; }

            GameEvents.RaiseMultiplierChanged(current);
        }

        public void ResetForRun()
        {
            capped = true;
            BonusPerKill = 0f;
            BonusDecayRate = 0f;
            current = tuning.Economy.multStart;
            PeakThisRun = current;
            graceLeft = 0f;
            GameEvents.RaiseMultiplierChanged(current);
        }
    }
}
