using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using Overdraft.Generation;

namespace Overdraft.Tests
{
    /// <summary>
    /// The generator is what makes the daily leaderboard valid (FR-452). Everything here runs
    /// against the pure Overdraft.Generation assembly with no Unity dependency in the code under
    /// test — only the test harness itself uses UnityEngine.
    /// </summary>
    public sealed class GenerationTests
    {
        [Test]
        public void SameSeed_ProducesIdenticalWaves()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                for (int wave = 1; wave <= 6; wave++)
                {
                    WaveRules rules = BuildRules(wave, wave >= 5);

                    List<SpawnEvent> a = new List<SpawnEvent>();
                    List<SpawnEvent> b = new List<SpawnEvent>();
                    WaveGenerator.Generate(seed, wave, 1f, rules, a);
                    WaveGenerator.Generate(seed, wave, 1f, rules, b);

                    Assert.IsTrue(AreEqual(a, b), $"seed={seed} wave={wave} diverged on a repeat call");
                }
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentWaves()
        {
            WaveRules rules = BuildRules(6, true);
            int collisions = 0;
            const int pairs = 500;

            for (int i = 0; i < pairs; i++)
            {
                List<SpawnEvent> a = new List<SpawnEvent>();
                List<SpawnEvent> b = new List<SpawnEvent>();
                WaveGenerator.Generate(i, 6, 1f, rules, a);
                WaveGenerator.Generate(i + 100_000, 6, 1f, rules, b);

                if (AreEqual(a, b)) { collisions++; }
            }

            Assert.Less(collisions, pairs / 100, "collision rate must stay under 1%");
        }

        [Test]
        public void WaveBudget_NeverExceeded()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                for (int wave = 1; wave <= 6; wave++)
                {
                    WaveRules rules = BuildRules(wave, wave >= 5);
                    List<SpawnEvent> output = new List<SpawnEvent>();
                    WaveGenerator.Generate(seed, wave, 1f, rules, output);

                    double budget = Budget(rules, wave, 1f);
                    double spent = SpentBudget(rules, output);

                    Assert.LessOrEqual(spent, budget + 0.01, $"seed={seed} wave={wave}");
                }
            }
        }

        [Test]
        public void WaveBudget_NearlyFullySpent()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                for (int wave = 1; wave <= 6; wave++)
                {
                    WaveRules rules = BuildRules(wave, wave >= 5);
                    List<SpawnEvent> output = new List<SpawnEvent>();
                    WaveGenerator.Generate(seed, wave, 1f, rules, output);

                    double budget = Budget(rules, wave, 1f);
                    double spent = SpentBudget(rules, output);
                    double maxSingleCost = MaxPossibleSingleCost(rules);

                    Assert.GreaterOrEqual(spent, budget - maxSingleCost - 0.01, $"seed={seed} wave={wave}");
                }
            }
        }

        [Test]
        public void Wave1_ContainsOnlyDrifters()
        {
            WaveRules rules = BuildRules(1, false);

            for (int seed = 0; seed < 200; seed++)
            {
                List<SpawnEvent> output = new List<SpawnEvent>();
                WaveGenerator.Generate(seed, 1, 1f, rules, output);

                for (int i = 0; i < output.Count; i++)
                {
                    Assert.AreEqual(EnemyType.Drifter, output[i].Type, $"seed={seed}");
                }
            }
        }

        [Test]
        public void Elites_NeverAppear_WhenNotAllowed()
        {
            for (int wave = 1; wave <= 4; wave++)
            {
                WaveRules rules = BuildRules(wave, elitesAllowed: false);

                for (int seed = 0; seed < 200; seed++)
                {
                    List<SpawnEvent> output = new List<SpawnEvent>();
                    WaveGenerator.Generate(seed, wave, 1f, rules, output);

                    for (int i = 0; i < output.Count; i++)
                    {
                        Assert.IsFalse(output[i].IsElite, $"wave={wave} seed={seed}");
                    }
                }
            }
        }

        [Test]
        public void Elites_CanAppear_WhenAllowed()
        {
            WaveRules rules = BuildRules(6, elitesAllowed: true);
            bool sawElite = false;

            for (int seed = 0; seed < 500 && !sawElite; seed++)
            {
                List<SpawnEvent> output = new List<SpawnEvent>();
                WaveGenerator.Generate(seed, 6, 1f, rules, output);

                for (int i = 0; i < output.Count; i++)
                {
                    if (output[i].IsElite) { sawElite = true; break; }
                }
            }

            Assert.IsTrue(sawElite, "elites should appear across 500 seeds at a 20% roll chance");
        }

        [Test]
        public void SpawnDelays_AreWithinWaveDuration()
        {
            WaveRules rules = BuildRules(6, true);

            for (int seed = 0; seed < 200; seed++)
            {
                List<SpawnEvent> output = new List<SpawnEvent>();
                WaveGenerator.Generate(seed, 6, 1f, rules, output);

                for (int i = 0; i < output.Count; i++)
                {
                    Assert.GreaterOrEqual(output[i].DelaySeconds, 0f);
                    Assert.LessOrEqual(output[i].DelaySeconds, rules.DurationSeconds);
                }
            }
        }

        [Test]
        public void ClumpShapes_AreNotLinear_AndDifferFromEachOther()
        {
            float[] sample = { 0.1f, 0.3f, 0.5f, 0.7f, 0.9f };

            foreach (ClumpPattern pattern in new[]
                { ClumpPattern.Steady, ClumpPattern.FrontLoad, ClumpPattern.Crescendo, ClumpPattern.TwinPeak })
            {
                bool anyDiffersFromLinear = false;
                for (int i = 0; i < sample.Length; i++)
                {
                    float f = WaveGenerator.Shape(pattern, sample[i]);
                    if (Mathf.Abs(f - sample[i]) > 0.02f) { anyDiffersFromLinear = true; }
                }
                Assert.IsTrue(anyDiffersFromLinear, $"{pattern} must not degenerate into a uniform 1:1 spread");
            }

            // FrontLoad puts far more of its spawns early than Crescendo does.
            Assert.Greater(WaveGenerator.Shape(ClumpPattern.FrontLoad, 0.3f), WaveGenerator.Shape(ClumpPattern.Crescendo, 0.3f));

            // TwinPeak's two bursts sit at the documented centres (docs/00 clump table).
            Assert.That(WaveGenerator.Shape(ClumpPattern.TwinPeak, 0.25f), Is.EqualTo(0.15f).Within(0.01f));
            Assert.That(WaveGenerator.Shape(ClumpPattern.TwinPeak, 0.75f), Is.EqualTo(0.65f).Within(0.01f));
        }

        [Test]
        public void ClumpPattern_IsSeedDetermined()
        {
            bool sawMoreThanOnePattern = false;
            ClumpPattern first = WaveGenerator.PickPattern(0, 6);

            for (int seed = 1; seed < 100; seed++)
            {
                if (WaveGenerator.PickPattern(seed, 6) != first) { sawMoreThanOnePattern = true; break; }
            }

            Assert.IsTrue(sawMoreThanOnePattern, "100 seeds should not all roll the same clump pattern");
            Assert.AreEqual(first, WaveGenerator.PickPattern(0, 6), "the same seed must always roll the same pattern");
        }

        [Test]
        public void RunSeed_Channels_AreIndependent()
        {
            RunSeed seedA = new RunSeed(12345);
            System.Random channel0 = seedA.Stream(RunSeed.ChannelWaveComposition, 3);
            System.Random channel1 = seedA.Stream(RunSeed.ChannelSpawnTiming, 3);

            int[] fromChannel0 = new int[5];
            int[] fromChannel1 = new int[5];
            for (int i = 0; i < 5; i++)
            {
                fromChannel0[i] = channel0.Next();
                fromChannel1[i] = channel1.Next();
            }

            bool anyDifferent = false;
            for (int i = 0; i < 5; i++)
            {
                if (fromChannel0[i] != fromChannel1[i]) { anyDifferent = true; }
            }
            Assert.IsTrue(anyDifferent, "different channels must not produce the same stream");

            RunSeed seedA2 = new RunSeed(12345);
            System.Random channel0Again = seedA2.Stream(RunSeed.ChannelWaveComposition, 3);
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(fromChannel0[i], channel0Again.Next(), "reconstructing must reproduce the exact sequence");
            }

            // Drawing from an unrelated channel (2) must not perturb channel 0's rolls.
            RunSeed seedB = new RunSeed(12345);
            System.Random unrelatedChannel2 = seedB.Stream(RunSeed.ChannelUpgradeOffers, 99);
            unrelatedChannel2.Next();
            System.Random channel0Third = seedB.Stream(RunSeed.ChannelWaveComposition, 3);
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(fromChannel0[i], channel0Third.Next());
            }
        }

        [Test]
        public void Generate_BoundedAllocation_AfterWarmup()
        {
            // docs/05 names this test "Generate_AllocatesNothing_AfterWarmup". docs/01 §6 mandates
            // a System.Random-per-stream design (RunSeed.Stream constructs a fresh System.Random
            // every call) — both RunSeed and System.Random are heap classes, so a literal
            // zero-byte assertion is incompatible with the documented architecture. This asserts
            // the allocation stays small and CONSTANT rather than scaling with spawn count, which
            // is what actually protects against wave generation becoming a per-frame cost.
            // Flagged in the session summary as a deliberate deviation from the literal AC name.
            WaveRules rules = BuildRules(6, true);
            List<SpawnEvent> output = new List<SpawnEvent>(64);

            WaveGenerator.Generate(1, 6, 1f, rules, output);   // warm up: JIT, List backing growth

            long before = System.GC.GetTotalMemory(true);
            WaveGenerator.Generate(2, 6, 1f, rules, output);
            long after = System.GC.GetTotalMemory(true);

            Assert.Less(after - before, 2048L,
                "allocation grew past the RunSeed + 2 System.Random baseline — something is now " +
                "allocating per spawn rather than once per wave.");
        }

        [Test]
        public void Determinism_HashOf1000Runs_MatchesGolden()
        {
            string hash = ComputeThousandRunHash();
            string goldenPath = GoldenFilePath();

            string existing = File.Exists(goldenPath) ? File.ReadAllText(goldenPath).Trim() : "";

            if (existing.Length == 0 || existing == PlaceholderGolden)
            {
                Assert.Ignore(
                    "No verified golden hash committed yet. This run produced: " + hash + "\n" +
                    "Per docs/05_QA_AND_VERIFY.md: generate this once, confirm it matches on BOTH " +
                    "macOS and Windows, then paste it into " + goldenPath + " replacing the " +
                    "placeholder. Until then this test intentionally cannot fail closed — an " +
                    "un-cross-platform-verified hash is worse than no hash.");
                return;
            }

            Assert.AreEqual(existing, hash,
                "Determinism broke — this invalidates the daily leaderboard. Treat as P0 (docs/05 §4).");
        }

        // --- helpers -------------------------------------------------------------

        static WaveRules BuildRules(int wave, bool elitesAllowed)
        {
            const float budgetBase = 60f;
            const float budgetExponent = 1.6f;
            const float budgetScale = 22f;
            const float duration = 30f;
            const float ringMin = 12f;
            const float ringMax = 16f;
            const float eliteCostMultiplier = 2.5f;
            const int eliteWeightPercent = 20;

            EnemyType[] types;
            int[] weights;
            int[] costs;

            if (wave <= 1)
            {
                types = new[] { EnemyType.Drifter };
                weights = new[] { 70 };
                costs = new[] { 10 };
            }
            else if (wave == 2)
            {
                types = new[] { EnemyType.Drifter, EnemyType.Spitter };
                weights = new[] { 70, 20 };
                costs = new[] { 10, 40 };
            }
            else
            {
                types = new[] { EnemyType.Drifter, EnemyType.Spitter, EnemyType.Splitter };
                weights = new[] { 70, 20, 10 };
                costs = new[] { 10, 40, 25 };
            }

            return new WaveRules(
                wave, types, weights, costs, elitesAllowed, eliteWeightPercent,
                duration, ringMin, ringMax, eliteCostMultiplier,
                budgetBase, budgetExponent, budgetScale);
        }

        static double Budget(in WaveRules rules, int wave, float riskMultiplier)
        {
            return (rules.BudgetBase + System.Math.Pow(wave, rules.BudgetExponent) * rules.BudgetScale) * riskMultiplier;
        }

        static double SpentBudget(in WaveRules rules, List<SpawnEvent> output)
        {
            double spent = 0;
            for (int i = 0; i < output.Count; i++)
            {
                SpawnEvent e = output[i];
                int baseCost = CostFor(rules, e.Type);
                spent += e.IsElite ? baseCost * (double)rules.EliteCostMultiplier : baseCost;
            }
            return spent;
        }

        static double MaxPossibleSingleCost(in WaveRules rules)
        {
            double max = 0;
            for (int i = 0; i < rules.Costs.Length; i++)
            {
                double c = rules.Costs[i] * (double)rules.EliteCostMultiplier;
                if (c > max) { max = c; }
            }
            return max;
        }

        static int CostFor(in WaveRules rules, EnemyType type)
        {
            for (int i = 0; i < rules.AllowedTypes.Length; i++)
            {
                if (rules.AllowedTypes[i] == type) { return rules.Costs[i]; }
            }
            return 0;
        }

        static bool AreEqual(List<SpawnEvent> a, List<SpawnEvent> b)
        {
            if (a.Count != b.Count) { return false; }
            for (int i = 0; i < a.Count; i++)
            {
                SpawnEvent x = a[i];
                SpawnEvent y = b[i];
                if (x.Type != y.Type) { return false; }
                if (x.IsElite != y.IsElite) { return false; }
                if (!Mathf.Approximately(x.DelaySeconds, y.DelaySeconds)) { return false; }
                if (!Mathf.Approximately(x.RingAngleRad, y.RingAngleRad)) { return false; }
                if (!Mathf.Approximately(x.RingRadius, y.RingRadius)) { return false; }
            }
            return true;
        }

        static string ComputeThousandRunHash()
        {
            List<SpawnEvent> buffer = new List<SpawnEvent>(64);

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                for (int seed = 0; seed < 1000; seed++)
                {
                    for (int wave = 1; wave <= 6; wave++)
                    {
                        WaveRules rules = BuildRules(wave, wave >= 5);
                        WaveGenerator.Generate(seed, wave, 1f, rules, buffer);

                        for (int i = 0; i < buffer.Count; i++)
                        {
                            SpawnEvent e = buffer[i];
                            writer.Write((byte)e.Type);
                            writer.Write(e.IsElite);
                            writer.Write(e.DelaySeconds);
                            writer.Write(e.RingAngleRad);
                            writer.Write(e.RingRadius);
                        }
                    }
                }

                writer.Flush();
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(stream.ToArray());
                    return ToHex(hash);
                }
            }
        }

        // Convert.ToHexString is .NET 5+ and unavailable under .NET Standard 2.1 (this project's
        // API compatibility level) — hand-rolled to match the shipping save-checksum code will
        // also need (see POST_LAUNCH.md).
        static string ToHex(byte[] bytes)
        {
            char[] chars = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                int b = bytes[i];
                chars[i * 2] = HexDigit(b >> 4);
                chars[i * 2 + 1] = HexDigit(b & 0xF);
            }
            return new string(chars);
        }

        static char HexDigit(int v) => (char)(v < 10 ? '0' + v : 'A' + (v - 10));

        static string GoldenFilePath()
        {
            return Path.Combine(Application.dataPath, "Tests", "EditMode", "golden_determinism.txt");
        }

        const string PlaceholderGolden = "PENDING_CROSS_PLATFORM_VERIFICATION";
    }
}
