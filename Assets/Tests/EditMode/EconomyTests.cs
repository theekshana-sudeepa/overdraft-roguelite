using NUnit.Framework;
using Overdraft.Core;

namespace Overdraft.Tests
{
    /// <summary>
    /// The economy is the game. Every requirement referenced here is one the Day-3 gate depends
    /// on, so a red test in this file blocks the milestone (CLAUDE.md §8).
    /// </summary>
    public sealed class EconomyTests
    {
        EconomyFixture fx;

        [SetUp]
        public void SetUp()
        {
            fx = new EconomyFixture();
        }

        [TearDown]
        public void TearDown()
        {
            fx.Dispose();
        }

        // --- CHARGE and MULT -----------------------------------------------------

        [Test]
        public void Kill_AddsValueTimesMultiplier()   // FR-201
        {
            fx.Kill(10);
            Assert.AreEqual(1.5f, fx.Multiplier.Current, 1e-4f, "first kill should take MULT to 1.5");
            Assert.AreEqual(15L, fx.Charge.Current, "10 x 1.5 = 15");

            fx.Kill(10);
            Assert.AreEqual(2.0f, fx.Multiplier.Current, 1e-4f);
            Assert.AreEqual(35L, fx.Charge.Current, "15 + (10 x 2.0)");
        }

        [Test]
        public void Kill_FloorsTheProduct_NeverRounds()   // FR-201
        {
            fx.Charge.AddFromKill(7, 1.5f);
            Assert.AreEqual(10L, fx.Charge.Current, "7 x 1.5 = 10.5 must floor to 10, not round to 11");
        }

        [Test]
        public void Multiplier_RespectsCap_UnlessUncapped()   // FR-202
        {
            for (int i = 0; i < 400; i++) { fx.Kill(1); }
            Assert.AreEqual(fx.Tuning.Economy.multMax, fx.Multiplier.Current, 1e-3f);

            fx.Multiplier.RemoveCap();
            fx.Kill(1);
            Assert.Greater(fx.Multiplier.Current, fx.Tuning.Economy.multMax, "GREED T2 removes the ceiling");
        }

        [Test]
        public void Multiplier_DecaysAfterGrace_FloorsAtOne()   // FR-203
        {
            for (int i = 0; i < 10; i++) { fx.Kill(1); }
            float peak = fx.Multiplier.Current;

            fx.Advance(fx.Tuning.Economy.multDecayGrace);
            Assert.AreEqual(peak, fx.Multiplier.Current, 1e-3f, "no decay until the grace window elapses");

            fx.Advance(2.0f);
            Assert.AreEqual(peak - 2.0f, fx.Multiplier.Current, 0.05f, "decays at multDecayRate per second");

            fx.Advance(60f);
            Assert.AreEqual(fx.Tuning.Economy.multMin, fx.Multiplier.Current, 1e-4f, "never falls below the floor");
        }

        [Test]
        public void Multiplier_DecayTimerResetsOnKill()
        {
            fx.Kill(1);
            fx.Advance(1.0f);
            fx.Kill(1);

            Assert.AreEqual(fx.Tuning.Economy.multDecayGrace, fx.Multiplier.GraceLeft, 0.05f);

            fx.Advance(1.4f);
            Assert.AreEqual(2.0f, fx.Multiplier.Current, 1e-3f, "a kill must refresh the whole grace window");
        }

        // --- Taking a hit --------------------------------------------------------

        [Test]
        public void PlayerHit_HalvesCharge_AndResetsMultiplier()   // FR-105
        {
            for (int i = 0; i < 20; i++) { fx.Kill(100); }

            long before = fx.Charge.Current;
            Assert.Greater(before, 0L);

            bool landed = fx.Health.TryTakeDamage(1);

            Assert.IsTrue(landed);
            Assert.AreEqual(before / 2L, fx.Charge.Current, "chargeLossOnHit is 0.50");
            Assert.AreEqual(fx.Tuning.Economy.multMin, fx.Multiplier.Current, 1e-4f);
            Assert.AreEqual(fx.Tuning.Player.maxHP - 1, fx.Health.CurrentHP);
        }

        [Test]
        public void PlayerHit_DuringInvulnWindow_IsIgnored()   // FR-104
        {
            fx.Health.TryTakeDamage(1);
            int hpAfterFirst = fx.Health.CurrentHP;

            for (int i = 0; i < 20; i++) { fx.Health.TryTakeDamage(1); }

            Assert.AreEqual(hpAfterFirst, fx.Health.CurrentHP,
                "standing inside a crowd must cost exactly one HP per invulnerability window");
        }

        [Test]
        public void GreedT3_DoublesChargeLossToFull()
        {
            for (int i = 0; i < 10; i++) { fx.Kill(100); }
            fx.Health.ChargeLossOverride = 1.0f;

            fx.Health.TryTakeDamage(1);

            Assert.AreEqual(0L, fx.Charge.Current, "GREED T3 takes chargeLossOnHit to 1.0");
        }

        // --- Banking -------------------------------------------------------------

        [Test]
        public void Bank_TransfersFullChargeAndResetsMultiplier()   // FR-205
        {
            for (int i = 0; i < 8; i++) { fx.Kill(50); }
            long banked = fx.Charge.Current;

            fx.Advance(fx.Tuning.Bank.duration + 0.1f, bankHeld: true);

            Assert.AreEqual(banked, fx.Score.Current);
            Assert.AreEqual(0L, fx.Charge.Current);
            Assert.AreEqual(fx.Tuning.Economy.multMin, fx.Multiplier.Current, 1e-4f, "banking costs the multiplier");
            Assert.IsFalse(fx.Bank.IsBanking);
        }

        [Test]
        public void Bank_AppliesRiskMultiplier()   // FR-209
        {
            fx.SetRiskTier(4);   // RISK V, score x3.0
            fx.Charge.AddFromKill(1000, 1f);

            fx.Advance(fx.Tuning.Bank.duration + 0.1f, bankHeld: true);

            Assert.AreEqual(3000L, fx.Score.Current);
        }

        [Test]
        public void BankCancelled_GivesNoPartialCredit()   // FR-207
        {
            fx.Charge.AddFromKill(500, 1f);

            fx.Advance(1.0f, bankHeld: true);
            Assert.IsTrue(fx.Bank.IsBanking);
            Assert.Greater(fx.Bank.Progress01, 0.5f);

            fx.Advance(0.1f, bankHeld: false);

            Assert.AreEqual(0L, fx.Score.Current, "no credit");
            Assert.AreEqual(500L, fx.Charge.Current, "and no penalty");
            Assert.AreEqual(0f, fx.Bank.Progress01, 1e-4f, "progress resets to zero");
            Assert.IsFalse(fx.Bank.IsLockedOut, "an early release must not lock the player out");
        }

        [Test]
        public void BankInterrupted_AppliesHitPenalty_AndLocksOut()   // FR-208
        {
            fx.Charge.AddFromKill(1000, 1f);
            fx.Advance(1.0f, bankHeld: true);

            fx.Health.TryTakeDamage(1);

            Assert.IsFalse(fx.Bank.IsBanking);
            Assert.AreEqual(0L, fx.Score.Current);
            Assert.AreEqual(500L, fx.Charge.Current, "the normal hit penalty still applies");
            Assert.IsTrue(fx.Bank.IsLockedOut);

            // Holding through the lockout must not sneak a bank through.
            fx.Advance(fx.Tuning.Bank.duration, bankHeld: true);
            Assert.AreEqual(0L, fx.Score.Current);
            Assert.IsFalse(fx.Bank.IsBanking);

            fx.Advance(fx.Tuning.Bank.interruptCooldown, bankHeld: false);
            fx.Advance(fx.Tuning.Bank.duration + 0.1f, bankHeld: true);
            Assert.AreEqual(500L, fx.Score.Current, "banking works again once the lockout expires");
        }

        [Test]
        public void Bank_WithZeroCharge_CompletesHarmlessly()   // QA case 6
        {
            Assert.AreEqual(0L, fx.Charge.Current);

            fx.Advance(fx.Tuning.Bank.duration + 0.1f, bankHeld: true);

            Assert.AreEqual(0L, fx.Score.Current);
            Assert.AreEqual(1, fx.Score.BankCount);
            Assert.IsFalse(fx.Bank.IsBanking);
        }

        [Test]
        public void LeechTiers_ReduceBankDuration_Correctly()
        {
            Assert.AreEqual(fx.Tuning.Bank.duration, fx.Bank.EffectiveDuration, 1e-4f);

            fx.Bank.LeechTier = 1;
            Assert.AreEqual(fx.Tuning.Bank.leechT1Duration, fx.Bank.EffectiveDuration, 1e-4f);

            fx.Bank.LeechTier = 2;
            Assert.AreEqual(fx.Tuning.Bank.leechT2Duration, fx.Bank.EffectiveDuration, 1e-4f);

            fx.Bank.LeechTier = 3;
            Assert.AreEqual(fx.Tuning.Bank.leechT3Duration, fx.Bank.EffectiveDuration, 1e-4f);
            Assert.AreEqual(fx.Tuning.Bank.leechT3IframeDuration, fx.Bank.CompletionIframes, 1e-4f);
        }

        [Test]
        public void LeechT3_BanksFasterThanBaseDuration()
        {
            fx.Bank.LeechTier = 3;
            fx.Charge.AddFromKill(100, 1f);

            // Held for less than the base 1.20s but more than the T3 0.50s.
            fx.Advance(0.6f, bankHeld: true);

            Assert.AreEqual(100L, fx.Score.Current);
        }

        // --- Score ---------------------------------------------------------------

        [Test]
        public void RunEnd_UnbankedCharge_ContributesZero()   // FR-204
        {
            for (int i = 0; i < 50; i++) { fx.Kill(200); }

            Assert.Greater(fx.Charge.Current, 0L, "there is unbanked CHARGE on the table");
            Assert.AreEqual(0L, fx.Score.Current, "and it is worth exactly nothing");
        }

        [Test]
        public void Score_UsesLong_DoesNotOverflowAtRiskV()
        {
            // ~10 hours of RISK V at a generous 2600 score/second, banked in big chunks.
            const long perBank = 2_600_000L;
            for (int i = 0; i < 40_000; i++)
            {
                fx.Score.AddBank(perBank, 3.0f);
            }

            Assert.Greater(fx.Score.Current, int.MaxValue, "an int would have wrapped long ago");
            Assert.AreEqual(40_000L * 7_800_000L, fx.Score.Current);
        }

        [Test]
        public void Score_SaturatesRatherThanWrapping()
        {
            fx.Score.AddBank(long.MaxValue, 1f);
            fx.Score.AddBank(long.MaxValue, 1f);

            Assert.AreEqual(long.MaxValue, fx.Score.Current);
            Assert.Greater(fx.Score.Current, 0L, "a wrap would make this negative");
        }

        // --- Boss audit ----------------------------------------------------------

        [Test]
        public void Audit_DrainsChargeOnly_NeverScore()   // FR-403
        {
            fx.Charge.AddFromKill(10_000, 1f);
            fx.Advance(fx.Tuning.Bank.duration + 0.1f, bankHeld: true);

            long bankedBefore = fx.Score.Current;
            Assert.AreEqual(10_000L, bankedBefore);

            fx.Charge.AddFromKill(10_000, 1f);
            long drained = fx.Charge.Drain(fx.Tuning.Boss.auditDrainRate, 1.0f);

            // Tolerance because auditDrainRate is a float: 0.08f is 0.0799999982, and the drain
            // is floored. The exact unit does not matter; draining CHARGE and only CHARGE does.
            Assert.That(drained, Is.EqualTo(800L).Within(2L), "8% of 10,000 per second");
            Assert.That(fx.Charge.Current, Is.EqualTo(9_200L).Within(2L));
            Assert.AreEqual(bankedBefore, fx.Score.Current, "banked SCORE is untouchable — that is the boss's whole idea");
        }

        // --- Event bus -----------------------------------------------------------

        [Test]
        public void BankCompleted_ReportsPostRiskAmount()
        {
            long reported = -1L;
            void Handler(long amount) { reported = amount; }

            GameEvents.BankCompleted += Handler;
            try
            {
                fx.SetRiskTier(4);
                fx.Charge.AddFromKill(100, 1f);
                fx.Advance(fx.Tuning.Bank.duration + 0.1f, bankHeld: true);
            }
            finally
            {
                GameEvents.BankCompleted -= Handler;
            }

            Assert.AreEqual(300L, reported, "the HUD and the audio stinger both read this value");
        }
    }
}
