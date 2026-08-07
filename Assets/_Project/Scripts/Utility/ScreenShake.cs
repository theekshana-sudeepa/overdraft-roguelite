using UnityEngine;
using Overdraft.Core;

namespace Overdraft.Utility
{
    /// <summary>
    /// Trauma-based screen shake (docs/04 §5). shake = trauma², so small hits stay subtle and
    /// big ones land hard.
    /// <para>
    /// Lives on a CHILD of the camera rig, never on the follow transform — otherwise the shake
    /// offset feeds back into the follow lerp and the camera drifts.
    /// </para>
    /// </summary>
    public sealed class ScreenShake : MonoBehaviour
    {
        [SerializeField] TuningConfig tuning;

        float trauma;
        float seed;

        /// <summary>0..1 user accessibility scale. Motion-sensitive players will otherwise refund.</summary>
        public float UserScale { get; set; } = 1f;

        void Awake()
        {
            // Per-session offset so two runs never shake identically. Cosmetic only — this must
            // never touch anything that feeds generation.
            seed = Random.value * 1000f;
        }

        void OnEnable()
        {
            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.PlayerHit += OnPlayerHit;
            GameEvents.BankCompleted += OnBankCompleted;
            GameEvents.BossPhaseChanged += OnBossPhaseChanged;
        }

        void OnDisable()
        {
            GameEvents.EnemyKilled -= OnEnemyKilled;
            GameEvents.PlayerHit -= OnPlayerHit;
            GameEvents.BankCompleted -= OnBankCompleted;
            GameEvents.BossPhaseChanged -= OnBossPhaseChanged;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        public void AddTrauma(float amount)
        {
            trauma = Mathf.Clamp01(trauma + amount);
        }

        void LateUpdate()
        {
            // Unscaled so shake keeps moving during hitstop — that is what sells the freeze.
            float dt = Time.unscaledDeltaTime;

            if (trauma > 0f)
            {
                trauma = Mathf.Max(0f, trauma - tuning.Feel.traumaDecayRate * dt);
            }

            if (trauma <= 0f)
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                return;
            }

            float shake = trauma * trauma * UserScale;
            float t = Time.unscaledTime * tuning.Feel.shakeFrequency;

            float ox = (Mathf.PerlinNoise(seed, t) * 2f - 1f) * tuning.Feel.shakeMaxOffset * shake;
            float oz = (Mathf.PerlinNoise(seed + 13.7f, t) * 2f - 1f) * tuning.Feel.shakeMaxOffset * shake;
            float rot = (Mathf.PerlinNoise(seed + 7f, t) * 2f - 1f) * tuning.Feel.shakeMaxRotation * shake;

            // Roll is about Y because this transform sits ABOVE the camera's 90-degree
            // top-down pitch: rotating about Y here reads as a camera roll on screen.
            transform.localPosition = new Vector3(ox, 0f, oz);
            transform.localRotation = Quaternion.Euler(0f, rot, 0f);
        }

        void OnEnemyKilled(EnemyKilledArgs args) => AddTrauma(tuning.Feel.traumaOnKill);
        void OnPlayerHit(PlayerHitArgs args) => AddTrauma(tuning.Feel.traumaOnPlayerHit);
        void OnBankCompleted(long amount) => AddTrauma(tuning.Feel.traumaOnBankComplete);
        void OnBossPhaseChanged(int phase) => AddTrauma(tuning.Feel.traumaOnBossPhase);
    }
}
