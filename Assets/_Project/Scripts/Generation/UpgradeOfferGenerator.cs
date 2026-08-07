using System.Collections.Generic;

namespace Overdraft.Generation
{
    /// <summary>One offer: a line plus the tier it would grant if taken (1..3).</summary>
    public readonly struct UpgradeOffer
    {
        public readonly UpgradeLine Line;
        public readonly int Tier;

        public UpgradeOffer(UpgradeLine line, int tier)
        {
            Line = line;
            Tier = tier;
        }
    }

    /// <summary>
    /// Picks up to <paramref name="maxOffers"/> upgrade lines to offer between waves
    /// (FR-471, FR-472). Deterministic from the run seed's upgrade-offers channel — same seed
    /// and same choice history always produces the same offers.
    /// <para>
    /// Unlike WaveGenerator this has no per-frame allocation budget — it runs at most 5 times a
    /// run, between waves, so a couple of small local arrays cost nothing worth guarding.
    /// </para>
    /// </summary>
    public static class UpgradeOfferGenerator
    {
        public const int MaxTier = 3;

        /// <summary>
        /// <paramref name="currentTiers"/> is indexed by UpgradeLine, values 0 (none) to 3 (maxed).
        /// A maxed line is never offered. If fewer than <paramref name="maxOffers"/> lines remain
        /// eligible, every remaining eligible line is offered instead.
        /// </summary>
        public static void Generate(int seed, int offerIndex, int[] currentTiers, List<UpgradeOffer> output, int maxOffers)
        {
            output.Clear();
            if (currentTiers == null || currentTiers.Length == 0 || maxOffers <= 0) { return; }

            System.Random rng = new RunSeed(seed).Stream(RunSeed.ChannelUpgradeOffers, offerIndex);

            int lineCount = currentTiers.Length;
            int[] eligible = new int[lineCount];
            int eligibleCount = 0;
            for (int i = 0; i < lineCount; i++)
            {
                if (currentTiers[i] < MaxTier) { eligible[eligibleCount] = i; eligibleCount++; }
            }

            int offerCount = eligibleCount < maxOffers ? eligibleCount : maxOffers;

            // Partial Fisher-Yates over the eligible set: picks offerCount distinct lines
            // without replacement, drawing only as much entropy as it actually needs.
            for (int i = 0; i < offerCount; i++)
            {
                int remaining = eligibleCount - i;
                int pick = i + rng.Next(remaining);

                int line = eligible[pick];
                eligible[pick] = eligible[i];
                eligible[i] = line;

                int nextTier = currentTiers[line] + 1;
                output.Add(new UpgradeOffer((UpgradeLine)line, nextTier));
            }
        }
    }
}
