namespace Overdraft.Generation
{
    /// <summary>
    /// The four upgrade lines (FR-472). Lives here rather than in Core because
    /// UpgradeOfferGenerator (CLAUDE.md §6) needs it and must stay pure C#.
    /// </summary>
    public enum UpgradeLine
    {
        Spread = 0,
        Volatile = 1,
        Leech = 2,
        Greed = 3
    }
}
