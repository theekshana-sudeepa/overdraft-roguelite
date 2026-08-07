using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overdraft.Core
{
    /// <summary>
    /// First object in the first scene. Sets the fixed timestep, installs services, and loads
    /// the first playable scene additively (docs/01 §8).
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        [SerializeField] TuningConfig tuning;

        /// <summary>
        /// M1 boots straight into the arena. This becomes "01_Menu" at M3 when menus exist —
        /// building a menu now would cost time the Day-3 gate needs.
        /// </summary>
        [SerializeField] string firstSceneName = "02_Arena";

        [SerializeField] int targetFrameRate = 60;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);

            // Static events survive a play session when domain reload is off, so the bus is
            // cleared on the way in as well as on the way out.
            GameEvents.ClearAll();

            Time.fixedDeltaTime = FixedTimestep;

            // Without this, a single long hitch triggers a cascade of catch-up FixedUpdates that
            // turns one dropped frame into a visible freeze.
            Time.maximumDeltaTime = MaxDeltaTime;

            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = targetFrameRate;

            ServiceLocator.Install(tuning);

            if (!SceneManager.GetSceneByName(firstSceneName).isLoaded)
            {
                SceneManager.LoadScene(firstSceneName, LoadSceneMode.Additive);
            }
        }

        void OnDestroy()
        {
            GameEvents.ClearAll();
            ServiceLocator.Clear();

            // Hitstop may have been mid-freeze when the scene tore down. Leaving timeScale at
            // 0.02 is a silent, extremely confusing bug on the next load.
            Time.timeScale = 1f;
        }

        void OnApplicationQuit()
        {
            GameEvents.ClearAll();
            Time.timeScale = 1f;
        }

        /// <summary>60 Hz. C8 makes this a contract, not a preference.</summary>
        const float FixedTimestep = 1f / 60f;
        const float MaxDeltaTime = 0.1f;
    }
}
