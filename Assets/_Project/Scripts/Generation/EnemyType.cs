namespace Overdraft.Generation
{
    /// <summary>
    /// The three base enemy archetypes (PRD NG7 caps this list at three).
    /// Lives in the pure assembly because wave generation is expressed in terms of it, and this
    /// assembly must never take an engine dependency (ARCH §1).
    /// </summary>
    public enum EnemyType
    {
        Drifter = 0,
        Spitter = 1,
        Splitter = 2
    }
}
