namespace Overdraft.Core
{
    /// <summary>
    /// The entire "dependency injection" of this project: four properties, set once at boot
    /// (docs/01 §4). Do not generalise it, do not add a registry, do not make it generic.
    /// </summary>
    public static class ServiceLocator
    {
        public static TuningConfig Tuning { get; private set; }

        /// <summary>Null outside a run. Set by RunController in Awake, cleared in OnDestroy.</summary>
        public static RunController Run { get; internal set; }

        public static bool IsInstalled => Tuning != null;

        public static void Install(TuningConfig tuning)
        {
            Tuning = tuning;
        }

        /// <summary>Called by Bootstrap on teardown so a stale ScriptableObject reference never leaks.</summary>
        public static void Clear()
        {
            Tuning = null;
            Run = null;
        }

#if UNITY_EDITOR
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticsOnEnterPlayMode()
        {
            Clear();
        }
#endif
    }
}
