using UnityEngine;
using Overdraft.Player;

namespace Overdraft.Core
{
    /// <summary>
    /// Orthographic top-down follow with a small aim lead and a zoom punch (docs/04 §5).
    /// <para>
    /// Hierarchy is CameraRig → ShakeChild → Camera. Shake is applied to the child so the
    /// offset never feeds back into the follow lerp; putting it on the follow transform makes
    /// the camera drift under sustained trauma.
    /// </para>
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] TuningConfig tuning;
        [SerializeField] PlayerController target;
        [SerializeField] Camera cam;

        float baseOrthoSize;
        float punchTimer;

        void Awake()
        {
            if (tuning == null) { tuning = ServiceLocator.Tuning; }
            baseOrthoSize = tuning.Arena.floorOrthoSize;
            cam.orthographic = true;
            cam.orthographicSize = baseOrthoSize;
        }

        void OnEnable()
        {
            GameEvents.BankCompleted += OnBankCompleted;
        }

        void OnDisable()
        {
            GameEvents.BankCompleted -= OnBankCompleted;
        }

        public void SetBaseOrthoSize(float size)
        {
            baseOrthoSize = size;
        }

        void LateUpdate()
        {
            // Unscaled so the camera keeps living during hitstop. A camera that freezes solid
            // makes the freeze read as a hitch instead of as impact.
            float dt = Time.unscaledDeltaTime;

            Vector3 desired = target.Position + target.AimDirection * tuning.Feel.cameraAimLead;
            desired.y = transform.position.y;

            transform.position = Utility.MathX.Damp(transform.position, desired, tuning.Feel.cameraFollowLerp, dt);

            if (punchTimer > 0f)
            {
                punchTimer -= dt;
                if (punchTimer < 0f) { punchTimer = 0f; }
            }

            float duration = tuning.Feel.zoomPunchDuration;
            float t = duration > 0f ? punchTimer / duration : 0f;
            float punch = tuning.Feel.zoomPunchAmount * Utility.MathX.EaseOutCubic(t);

            cam.orthographicSize = baseOrthoSize * (1f - punch);
        }

        void OnBankCompleted(long amount)
        {
            punchTimer = tuning.Feel.zoomPunchDuration;
        }
    }
}
