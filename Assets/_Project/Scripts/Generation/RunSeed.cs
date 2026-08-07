namespace Overdraft.Generation
{
    /// <summary>
    /// Produces independent random streams from one master seed (docs/01_ARCHITECTURE.md §6).
    /// <para>
    /// Channels keep systems from stepping on each other: adding a new system later that draws
    /// from a new channel cannot shift the rolls of an existing one, so old daily seeds stay
    /// reproducible forever. Fixed channel assignment — do not renumber:
    /// 0 = wave composition, 1 = spawn timing, 2 = upgrade offers, 3 = hazard/boss patterns,
    /// 4 = cosmetic flavour.
    /// </para>
    /// </summary>
    public sealed class RunSeed
    {
        public const int ChannelWaveComposition = 0;
        public const int ChannelSpawnTiming = 1;
        public const int ChannelUpgradeOffers = 2;
        public const int ChannelBossPatterns = 3;
        public const int ChannelCosmetic = 4;

        readonly int master;

        public RunSeed(int master)
        {
            this.master = master;
        }

        public System.Random Stream(int channel, int index)
        {
            return new System.Random(Hash(master, channel, index));
        }

        // FNV-1a, explicit and stable across runtimes — unlike string.GetHashCode(), which is
        // randomised per process and must never be used to derive a seed (docs/01 §6 warning).
        static int Hash(int a, int b, int c)
        {
            unchecked
            {
                const int prime = 16777619;
                int h = unchecked((int)2166136261);
                h = (h ^ a) * prime;
                h = (h ^ b) * prime;
                h = (h ^ c) * prime;
                return h;
            }
        }
    }
}
