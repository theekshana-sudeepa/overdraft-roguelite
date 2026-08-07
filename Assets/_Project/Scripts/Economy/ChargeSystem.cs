using UnityEngine;
using Overdraft.Core;

namespace Overdraft.Economy
{
    /// <summary>
    /// The unbanked pool. CHARGE is worth exactly zero at the end of a run (FR-204) — the only
    /// thing that converts it to permanent score is a completed bank.
    /// </summary>
    public sealed class ChargeSystem : MonoBehaviour
    {
        long current;

        public long Current => current;

        /// <summary>Everything destroyed by hits and audits this run. Drives the results screen.</summary>
        public long LostThisRun { get; private set; }

        // Deliberately holds no TuningConfig reference: every fraction this class applies
        // (hit penalty, audit drain) is passed in by the caller that owns that rule. A config
        // reference here would be an unused field and an invitation to duplicate policy.

        /// <summary>
        /// Adds floor(baseValue × multiplier) to CHARGE (FR-201). Called by RunController's kill
        /// handler AFTER MultiplierSystem has been advanced — see docs/01 §10 for why these two
        /// are the one documented exception to the event bus.
        /// </summary>
        public void AddFromKill(int baseValue, float multiplier)
        {
            if (baseValue <= 0) { return; }

            // double for the product so a large value at a high multiplier does not lose
            // precision before the floor, which would make the HUD number look wrong.
            long gained = (long)System.Math.Floor(baseValue * (double)multiplier);
            if (gained <= 0) { return; }

            current += gained;
            GameEvents.RaiseChargeChanged(current);
        }

        /// <summary>Applies the hit penalty (FR-105). Returns the amount destroyed.</summary>
        public long ApplyHitPenalty(float lossFraction)
        {
            if (current <= 0) { return 0L; }

            // Floored in the player's favour: at 101 CHARGE a 50% hit costs 50, not 51.
            long destroyed = (long)System.Math.Floor(current * (double)lossFraction);
            if (destroyed <= 0) { return 0L; }

            current -= destroyed;
            LostThisRun += destroyed;
            GameEvents.RaiseChargeChanged(current);
            return destroyed;
        }

        /// <summary>
        /// Boss AUDIT drain (FR-403). Drains CHARGE only — banked SCORE is never touched, which
        /// is the entire design idea of the boss.
        /// </summary>
        public long Drain(float fractionPerSecond, float deltaTime)
        {
            if (current <= 0) { return 0L; }

            long drained = (long)System.Math.Floor(current * (double)fractionPerSecond * deltaTime);

            // At low CHARGE the floor would round the drain to zero forever, so the audit takes
            // at least one unit per step. Otherwise the boss stops being threatening at the exact
            // moment the player is poorest.
            if (drained <= 0) { drained = 1L; }
            if (drained > current) { drained = current; }

            current -= drained;
            LostThisRun += drained;
            GameEvents.RaiseChargeChanged(current);
            return drained;
        }

        /// <summary>Zeroes CHARGE and returns what it was. Only BankSystem may call this.</summary>
        internal long Consume()
        {
            long amount = current;
            if (amount == 0L) { return 0L; }

            current = 0L;
            GameEvents.RaiseChargeChanged(current);
            return amount;
        }

        public void ResetForRun()
        {
            current = 0L;
            LostThisRun = 0L;
            GameEvents.RaiseChargeChanged(current);
        }
    }
}
