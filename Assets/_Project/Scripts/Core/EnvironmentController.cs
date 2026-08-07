using UnityEngine;
using Overdraft.Player;

namespace Overdraft.Core
{
    /// <summary>
    /// Swaps THE FLOOR for THE VAULT on entry to wave 4 (FR-451). SetActive + a radius/ortho-size
    /// change — no scene load, no loading screen. The colour-grade crossfade in docs/04 §3
    /// requires a Volume/post-processing stack that doesn't exist until M3; this is the
    /// mechanically complete swap that stack will animate on top of later.
    /// </summary>
    public sealed class EnvironmentController : MonoBehaviour
    {
        [SerializeField] TuningConfig tuning;
        [SerializeField] GameObject floorRoot;
        [SerializeField] GameObject vaultRoot;
        [SerializeField] HazardBar[] hazardBars;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] PlayerController player;
        [SerializeField] Weapon weapon;

        public EnvironmentId Current { get; private set; } = EnvironmentId.Floor;

        public float CurrentArenaRadius => Current == EnvironmentId.Vault ? tuning.Arena.vaultRadius : tuning.Arena.floorRadius;

        public void SwitchTo(EnvironmentId id)
        {
            if (Current == id) { return; }
            Current = id;
            Apply();
            GameEvents.RaiseEnvironmentChanged(id);
        }

        public void ResetForRun()
        {
            Current = EnvironmentId.Floor;
            Apply();

            if (hazardBars == null) { return; }
            for (int i = 0; i < hazardBars.Length; i++) { hazardBars[i].ResetForRun(); }
        }

        /// <summary>
        /// Ticks the Vault's hazard bars and returns the contact damage the player should take
        /// this step, or 0. A no-op while THE FLOOR is active.
        /// </summary>
        public int TickHazards(float deltaTime)
        {
            if (Current != EnvironmentId.Vault || hazardBars == null) { return 0; }

            float radius = CurrentArenaRadius;
            int damage = 0;

            for (int i = 0; i < hazardBars.Length; i++)
            {
                HazardBar bar = hazardBars[i];
                bar.Tick(deltaTime, radius);

                if (bar.OverlapsPlayer(player.Position, tuning.Player.hitboxRadius, radius))
                {
                    damage = 1;
                }
            }

            return damage;
        }

        void Apply()
        {
            bool vault = Current == EnvironmentId.Vault;
            floorRoot.SetActive(!vault);
            vaultRoot.SetActive(vault);

            float radius = vault ? tuning.Arena.vaultRadius : tuning.Arena.floorRadius;
            float orthoSize = vault ? tuning.Arena.vaultOrthoSize : tuning.Arena.floorOrthoSize;

            player.SetArenaRadius(radius);
            weapon.SetArenaRadius(radius);
            cameraRig.SetBaseOrthoSize(orthoSize);
        }
    }
}
