using UnityEngine;

namespace Overdraft.Core
{
    /// <summary>
    /// THE VAULT's rotating laser hazard (FR-457). Deliberately NOT seeded — unlike the boss's
    /// laser sweep (which IS seeded, so a daily run is identical for everyone), the player must
    /// be able to learn this bar's rhythm purely by watching it.
    /// <para>
    /// Ticked explicitly by RunController on the fixed step, like everything else in the
    /// simulation (C8) — it does not run its own Update().
    /// </para>
    /// </summary>
    public sealed class HazardBar : MonoBehaviour
    {
        [SerializeField] TuningConfig tuning;
        [SerializeField] Transform visual;
        [SerializeField] MeshRenderer meshRenderer;

        static MaterialPropertyBlock propertyBlock;

        float phaseOffset;
        float cycleTimer;
        bool lethal;

        public bool IsLethal => lethal;

        public void Configure(float phaseOffsetSeconds)
        {
            phaseOffset = phaseOffsetSeconds;
            ResetForRun();
        }

        public void Tick(float deltaTime, float arenaRadius)
        {
            cycleTimer += deltaTime;

            float cycle = tuning.Arena.hazardCycleDuration;
            if (cycle <= 0f) { return; }

            float phase = cycleTimer % cycle;
            bool wasLethal = lethal;
            lethal = phase >= tuning.Arena.hazardTelegraphTime;

            if (lethal != wasLethal) { ApplyTelegraphColor(lethal); }

            // Continuous rotation — one full sweep per cycle — regardless of telegraph/lethal
            // phase. That continuity is what makes the pattern learnable.
            float degrees = (cycleTimer / cycle) * 360f;
            visual.localRotation = Quaternion.Euler(0f, degrees, 0f);
            visual.localScale = new Vector3(tuning.Arena.hazardBarWidth, 1f, arenaRadius);
        }

        /// <summary>
        /// Point-to-segment contact test against the bar's current sweep line. No physics or
        /// second collider type (C5) — the bar is treated as a thin rectangle from the arena
        /// centre out to its current radius.
        /// </summary>
        public bool OverlapsPlayer(in Vector3 playerPosition, float playerRadius, float arenaRadius)
        {
            if (!lethal) { return false; }

            Vector3 direction = visual.forward;
            Vector3 toPlayer = playerPosition - transform.position;
            float along = Mathf.Clamp(Vector3.Dot(toPlayer, direction), 0f, arenaRadius);

            Vector3 closest = transform.position + direction * along;
            float sqrDist = (playerPosition - closest).sqrMagnitude;

            float halfWidth = tuning.Arena.hazardBarWidth * 0.5f;
            float r = halfWidth + playerRadius;
            return sqrDist <= r * r;
        }

        public void ResetForRun()
        {
            cycleTimer = phaseOffset;
            lethal = false;
            ApplyTelegraphColor(false);
        }

        void ApplyTelegraphColor(bool isLethal)
        {
            if (meshRenderer == null) { return; }
            if (propertyBlock == null) { propertyBlock = new MaterialPropertyBlock(); }

            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ShaderIds.BaseColor, isLethal ? Palette.Hazard : Palette.Intensity(Palette.Hazard, 0.35f));
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
