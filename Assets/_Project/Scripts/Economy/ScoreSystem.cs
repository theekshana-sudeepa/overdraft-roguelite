using UnityEngine;
using Overdraft.Core;

namespace Overdraft.Economy
{
    /// <summary>
    /// Permanent, banked score. Stored as long because a RISK V run can exceed int.MaxValue
    /// (FR-209) — an int here is a shipped bug waiting for the first good player.
    /// </summary>
    public sealed class ScoreSystem : MonoBehaviour
    {
        long current;

        public long Current => current;

        /// <summary>Number of completed banks this run. Feeds RunProof and the achievements.</summary>
        public int BankCount { get; private set; }

        /// <summary>The single largest completed bank this run, post-risk-multiplier.</summary>
        public long LargestBank { get; private set; }

        /// <summary>
        /// Credits a completed bank, applying the RISK tier score multiplier (FR-209).
        /// Returns the amount actually credited.
        /// </summary>
        public long AddBank(long rawAmount, float riskMultiplier)
        {
            if (rawAmount <= 0L)
            {
                // A zero-charge bank still counts as a bank: it completes harmlessly and must
                // not divide by zero or fire a stinger (QA case 6).
                BankCount++;
                return 0L;
            }

            // Done in double and clamped before the cast: converting an out-of-range double to
            // long is unchecked in C# and silently yields long.MinValue, which would turn the
            // best run anyone ever has into a negative leaderboard entry.
            double scaled = rawAmount * (double)riskMultiplier;
            long credited;

            if (scaled >= long.MaxValue) { credited = long.MaxValue; }
            else if (scaled <= 0d) { credited = 0L; }
            else { credited = (long)System.Math.Floor(scaled); }

            // Saturating add. Ten hours of RISK V will not reach this, but a clamp is one line
            // and an overflow here silently inverts a leaderboard entry.
            if (credited > long.MaxValue - current)
            {
                current = long.MaxValue;
            }
            else
            {
                current += credited;
            }

            BankCount++;
            if (credited > LargestBank) { LargestBank = credited; }

            GameEvents.RaiseScoreChanged(current);
            return credited;
        }

        public void ResetForRun()
        {
            current = 0L;
            BankCount = 0;
            LargestBank = 0L;
            GameEvents.RaiseScoreChanged(current);
        }
    }
}
