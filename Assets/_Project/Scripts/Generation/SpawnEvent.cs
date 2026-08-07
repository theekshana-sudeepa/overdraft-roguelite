namespace Overdraft.Generation
{
    /// <summary>
    /// One scheduled enemy spawn, produced by WaveGenerator (M2-T04) and consumed by EnemySpawner.
    /// Position is expressed as polar coordinates on the spawn ring so the struct carries no
    /// engine vector type and stays platform-independent.
    /// </summary>
    public readonly struct SpawnEvent
    {
        public readonly EnemyType Type;
        public readonly bool IsElite;
        public readonly float DelaySeconds;
        public readonly float RingAngleRad;
        public readonly float RingRadius;

        public SpawnEvent(EnemyType type, bool isElite, float delaySeconds, float ringAngleRad, float ringRadius)
        {
            Type = type;
            IsElite = isElite;
            DelaySeconds = delaySeconds;
            RingAngleRad = ringAngleRad;
            RingRadius = ringRadius;
        }
    }
}
