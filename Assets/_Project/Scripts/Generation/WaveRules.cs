namespace Overdraft.Generation
{
    /// <summary>
    /// The inputs a wave needs in order to be generated, flattened out of TuningConfig by
    /// RunController. Generation/ never reads a ScriptableObject — that is what keeps it pure
    /// and what makes the daily seed reproducible on every platform (docs/02 §3).
    /// </summary>
    public readonly struct WaveRules
    {
        public readonly int WaveIndex;
        public readonly EnemyType[] AllowedTypes;
        public readonly int[] Weights;
        public readonly int[] Costs;
        public readonly bool ElitesAllowed;
        public readonly int EliteWeightPercent;
        public readonly float DurationSeconds;
        public readonly float RingMin;
        public readonly float RingMax;
        public readonly float EliteCostMultiplier;

        /// <summary>budget = (BudgetBase + waveIndex^BudgetExponent * BudgetScale) * riskBudgetMultiplier (FR-453).</summary>
        public readonly float BudgetBase;
        public readonly float BudgetExponent;
        public readonly float BudgetScale;

        public WaveRules(
            int waveIndex,
            EnemyType[] allowedTypes,
            int[] weights,
            int[] costs,
            bool elitesAllowed,
            int eliteWeightPercent,
            float durationSeconds,
            float ringMin,
            float ringMax,
            float eliteCostMultiplier,
            float budgetBase,
            float budgetExponent,
            float budgetScale)
        {
            WaveIndex = waveIndex;
            AllowedTypes = allowedTypes;
            Weights = weights;
            Costs = costs;
            ElitesAllowed = elitesAllowed;
            EliteWeightPercent = eliteWeightPercent;
            DurationSeconds = durationSeconds;
            RingMin = ringMin;
            RingMax = ringMax;
            EliteCostMultiplier = eliteCostMultiplier;
            BudgetBase = budgetBase;
            BudgetExponent = budgetExponent;
            BudgetScale = budgetScale;
        }
    }
}
