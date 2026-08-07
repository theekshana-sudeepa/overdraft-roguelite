using System;
using System.Collections.Generic;

namespace Overdraft.Generation
{
    /// <summary>
    /// Deterministic wave composition and spawn timing (FR-452, FR-453, FR-454).
    /// <para>
    /// Same seed + same wave index + same rules always produces an identical spawn list, on
    /// every platform, forever — this is what makes the daily leaderboard valid. Draws only from
    /// System.Random via RunSeed, carries no engine dependency of any kind, and allocates
    /// nothing beyond that once the caller's output list has warmed up to its steady-state
    /// capacity.
    /// </para>
    /// </summary>
    public static class WaveGenerator
    {
        /// <summary>
        /// Fills <paramref name="output"/> (cleared first) with every spawn for one wave.
        /// <paramref name="output"/> is caller-owned and reused across waves — Generate never
        /// allocates a List or array of its own.
        /// </summary>
        public static void Generate(
            int seed, int waveIndex, float riskBudgetMultiplier, in WaveRules rules, List<SpawnEvent> output)
        {
            output.Clear();

            if (rules.AllowedTypes == null || rules.AllowedTypes.Length == 0) { return; }
            if (rules.Weights == null || rules.Costs == null) { return; }

            RunSeed runSeed = new RunSeed(seed);
            Random compositionRng = runSeed.Stream(RunSeed.ChannelWaveComposition, waveIndex);
            Random timingRng = runSeed.Stream(RunSeed.ChannelSpawnTiming, waveIndex);

            double budget = rules.BudgetBase + Math.Pow(waveIndex, rules.BudgetExponent) * rules.BudgetScale;
            budget *= riskBudgetMultiplier;

            int minCost = int.MaxValue;
            for (int i = 0; i < rules.Costs.Length; i++)
            {
                if (rules.Costs[i] > 0 && rules.Costs[i] < minCost) { minCost = rules.Costs[i]; }
            }
            if (minCost == int.MaxValue) { return; }   // every cost was <= 0 — malformed rules, nothing to spend

            ClumpPattern pattern = (ClumpPattern)timingRng.Next(ClumpPatternCount);

            double remaining = budget;
            int guard = 0;

            // --- Pass 1: budget-spend selection. Angle/radius are order-independent and rolled
            //     immediately; delay depends on the final spawn count, so it is deferred to Pass 2.
            while (remaining >= minCost && guard < MaxSpawnsPerWave)
            {
                guard++;

                int totalWeight = 0;
                for (int i = 0; i < rules.AllowedTypes.Length; i++)
                {
                    if (rules.Costs[i] <= remaining) { totalWeight += rules.Weights[i]; }
                }
                if (totalWeight <= 0) { break; }   // nothing affordable left

                int roll = compositionRng.Next(totalWeight);
                int chosenIndex = -1;
                int cumulative = 0;
                for (int i = 0; i < rules.AllowedTypes.Length; i++)
                {
                    if (rules.Costs[i] > remaining) { continue; }
                    cumulative += rules.Weights[i];
                    if (roll < cumulative) { chosenIndex = i; break; }
                }
                if (chosenIndex < 0) { break; }   // unreachable given the roll range; defensive only

                EnemyType type = rules.AllowedTypes[chosenIndex];
                int baseCost = rules.Costs[chosenIndex];

                bool elite = false;
                double spendCost = baseCost;

                if (rules.ElitesAllowed)
                {
                    int eliteRoll = compositionRng.Next(100);
                    if (eliteRoll < rules.EliteWeightPercent)
                    {
                        double eliteCost = baseCost * (double)rules.EliteCostMultiplier;
                        if (eliteCost <= remaining)
                        {
                            elite = true;
                            spendCost = eliteCost;
                        }
                        // else: the surcharge doesn't fit. baseCost already does (the affordability
                        // filter above guarantees it), so the pick still lands, just non-elite —
                        // the earned roll isn't discarded.
                    }
                }

                remaining -= spendCost;

                float radius = PickRadius(rules, timingRng);
                float angle = (float)(timingRng.NextDouble() * TwoPi);

                // Delay is a placeholder until Pass 2 knows the final count.
                output.Add(new SpawnEvent(type, elite, 0f, angle, radius));
            }

            // --- Pass 2: assign clump-shaped delays now that the spawn count is known.
            int count = output.Count;
            for (int i = 0; i < count; i++)
            {
                float delay = PickDelay(pattern, i, count, rules.DurationSeconds, timingRng);
                SpawnEvent e = output[i];
                output[i] = new SpawnEvent(e.Type, e.IsElite, delay, e.RingAngleRad, e.RingRadius);
            }
        }

        /// <summary>
        /// Re-derives the clump pattern Generate() would pick for this seed/wave, as its own
        /// independent stream construction. Exists so the test suite can assert pattern
        /// selection directly. Reconstructing the stream here always reproduces the same first
        /// draw Generate() makes internally, since both come from identical Hash(seed, channel,
        /// wave) inputs — there is no shared mutable state to diverge.
        /// </summary>
        internal static ClumpPattern PickPattern(int seed, int waveIndex)
        {
            RunSeed runSeed = new RunSeed(seed);
            Random timingRng = runSeed.Stream(RunSeed.ChannelSpawnTiming, waveIndex);
            return (ClumpPattern)timingRng.Next(ClumpPatternCount);
        }

        static float PickRadius(in WaveRules rules, Random rng)
        {
            float min = rules.RingMin;
            float max = rules.RingMax;
            if (max <= min) { return min; }
            return (float)(min + rng.NextDouble() * (max - min));
        }

        /// <summary>
        /// Maps a spawn's ordinal position to a delay within the wave, shaped by the clump
        /// pattern. A uniform distribution is explicitly forbidden (FR-454) — clumps are what
        /// create the multiplier spikes that make the bank decision interesting.
        /// </summary>
        static float PickDelay(ClumpPattern pattern, int ordinal, int total, float duration, Random jitterRng)
        {
            if (duration <= 0f) { return 0f; }

            float t = total > 1 ? ordinal / (float)(total - 1) : 0f;
            float f = Shape(pattern, t);

            // Small deterministic jitter so spawns inside a cluster don't land on a suspiciously
            // perfect curve. Bounded well below the cluster width of any pattern.
            float jitter = (float)((jitterRng.NextDouble() * 2.0 - 1.0) * JitterFraction);

            float delay = (f + jitter) * duration;
            if (delay < 0f) { delay = 0f; }
            if (delay > duration) { delay = duration; }
            return delay;
        }

        /// <summary>Ordinal fraction t in [0,1] -> time fraction in [0,1], per docs/00 clump table.</summary>
        internal static float Shape(ClumpPattern pattern, float t)
        {
            switch (pattern)
            {
                case ClumpPattern.Steady:
                    // Even spawns across 70% of duration, then a 30% pause.
                    return t * 0.70f;

                case ClumpPattern.FrontLoad:
                    // 60% of spawns crammed into the first 25% of duration.
                    return t < 0.60f
                        ? (t / 0.60f) * 0.25f
                        : 0.25f + ((t - 0.60f) / 0.40f) * 0.75f;

                case ClumpPattern.Crescendo:
                    // Continuous ramp; final 20% of spawns land in roughly the final 45% of duration.
                    return (float)Math.Pow(t, CrescendoExponent);

                case ClumpPattern.TwinPeak:
                    // Two dense bursts centred at 15% and 65% of duration.
                    if (t < 0.5f)
                    {
                        float local = t / 0.5f;
                        return 0.15f + (local - 0.5f) * TwinPeakBurstWidth;
                    }
                    else
                    {
                        float local = (t - 0.5f) / 0.5f;
                        return 0.65f + (local - 0.5f) * TwinPeakBurstWidth;
                    }

                default:
                    return t;
            }
        }

        const double TwoPi = Math.PI * 2.0;
        const float JitterFraction = 0.02f;
        const float CrescendoExponent = 2.7f;
        const float TwinPeakBurstWidth = 0.12f;
        const int ClumpPatternCount = 4;
        const int MaxSpawnsPerWave = 4096;
    }
}
