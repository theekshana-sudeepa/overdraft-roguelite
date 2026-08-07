using UnityEngine;
using Overdraft.Core;

namespace Overdraft.UI
{
    /// <summary>
    /// The bank progress ring, drawn in world space AROUND the player (docs/04 §6).
    /// <para>
    /// This is not a stylistic choice. During the 1.2-second hold the player's eye has to be on
    /// the exact spot where the danger is arriving. A corner progress bar pulls the eye off the
    /// character and breaks the entire tension design — do not move it into the HUD.
    /// </para>
    /// </summary>
    public sealed class BankRing : MonoBehaviour
    {
        [SerializeField] Transform followTarget;
        [SerializeField] MeshRenderer ringRenderer;

        MaterialPropertyBlock propertyBlock;
        float fill;

        void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            SetVisible(false);
        }

        void OnEnable()
        {
            GameEvents.BankStarted += OnBankStarted;
            GameEvents.BankProgress += OnBankProgress;
            GameEvents.BankCancelled += OnBankCancelled;
            GameEvents.BankCompleted += OnBankCompleted;
        }

        void OnDisable()
        {
            GameEvents.BankStarted -= OnBankStarted;
            GameEvents.BankProgress -= OnBankProgress;
            GameEvents.BankCancelled -= OnBankCancelled;
            GameEvents.BankCompleted -= OnBankCompleted;
        }

        void LateUpdate()
        {
            if (followTarget == null) { return; }

            Vector3 p = followTarget.position;
            p.y = RingHeight;
            transform.position = p;
        }

        void OnBankStarted()
        {
            fill = 0f;
            ApplyFill();
            SetVisible(true);
        }

        void OnBankProgress(float progress01)
        {
            fill = progress01;
            ApplyFill();
        }

        void OnBankCancelled(bool interrupted)
        {
            SetVisible(false);
        }

        void OnBankCompleted(long amount)
        {
            SetVisible(false);
        }

        void ApplyFill()
        {
            if (ringRenderer == null) { return; }

            ringRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(ShaderIds.Fill, fill);
            propertyBlock.SetColor(ShaderIds.BaseColor, Palette.Bank);
            ringRenderer.SetPropertyBlock(propertyBlock);
        }

        void SetVisible(bool visible)
        {
            if (ringRenderer == null) { return; }
            ringRenderer.enabled = visible;
        }

        /// <summary>Sits just above the floor plane so it never z-fights with the grid.</summary>
        const float RingHeight = 0.02f;
    }
}
