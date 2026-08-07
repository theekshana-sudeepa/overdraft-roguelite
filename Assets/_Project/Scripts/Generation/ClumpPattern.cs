namespace Overdraft.Generation
{
    /// <summary>
    /// Spawn-timing shapes. A uniform distribution is deliberately absent: clumps are what
    /// create the multiplier spikes that make the bank decision interesting (FR-454).
    /// </summary>
    public enum ClumpPattern
    {
        Steady = 0,
        FrontLoad = 1,
        Crescendo = 2,
        TwinPeak = 3
    }
}
