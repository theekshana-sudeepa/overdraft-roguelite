using UnityEngine;
using Overdraft.Core;
using Overdraft.Economy;

namespace Overdraft.Player
{
    /// <summary>
    /// 3 HP, one damage event per invulnerability window (FR-104), and the charge penalty that
    /// makes every hit hurt twice — once in HP and once in the wallet (FR-105).
    /// </summary>
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField] TuningConfig tuning;
        [SerializeField] ChargeSystem charge;
        [SerializeField] MultiplierSystem multiplier;
        [SerializeField] BankSystem bank;
        [SerializeField] DashModule dash;
        [SerializeField] Renderer[] blinkRenderers;

        float invulnTimer;
        int hp;
        bool visibleLastFrame = true;

        public int CurrentHP => hp;
        public int MaxHP => tuning.Player.maxHP;
        public bool IsAlive => hp > 0;
        public bool IsInvulnerable => invulnTimer > 0f || dash.IsInvulnerable;

        /// <summary>Extra loss fraction from GREED T3, which takes chargeLossOnHit to 1.0.</summary>
        public float ChargeLossOverride { get; set; } = -1f;

        void Awake()
        {
            if (tuning == null) { tuning = ServiceLocator.Tuning; }
            if (tuning != null) { hp = tuning.Player.maxHP; }
        }

        internal void Configure(
            TuningConfig config, ChargeSystem chargeSystem, MultiplierSystem multiplierSystem,
            BankSystem bankSystem, DashModule dashModule)
        {
            tuning = config;
            charge = chargeSystem;
            multiplier = multiplierSystem;
            bank = bankSystem;
            dash = dashModule;
            ResetForRun();
        }

        public void Tick(float deltaTime)
        {
            if (invulnTimer <= 0f) { return; }
            invulnTimer -= deltaTime;
            if (invulnTimer < 0f) { invulnTimer = 0f; }
        }

        /// <summary>
        /// Blink is a visual, so it runs on unscaled frame time rather than the fixed step —
        /// otherwise it freezes solid during hitstop at exactly the moment the player needs to
        /// see that they are still invulnerable.
        /// </summary>
        void Update()
        {
            if (blinkRenderers == null || blinkRenderers.Length == 0) { return; }

            bool visible = true;
            if (invulnTimer > 0f)
            {
                float phase = invulnTimer * tuning.Player.invulnBlinkHz;
                visible = Mathf.Repeat(phase, 1f) > 0.5f;
            }

            if (visible == visibleLastFrame) { return; }
            visibleLastFrame = visible;

            for (int i = 0; i < blinkRenderers.Length; i++)
            {
                blinkRenderers[i].enabled = visible;
            }
        }

        /// <summary>
        /// The single damage entry point. Returns true if the hit actually landed.
        /// Standing inside twenty drifters costs exactly one HP per invulnerability window,
        /// because everything funnels through this guard.
        /// </summary>
        public bool TryTakeDamage(int amount)
        {
            if (!IsAlive) { return false; }
            if (IsInvulnerable) { return false; }
            if (amount <= 0) { return false; }

            hp -= amount;
            if (hp < 0) { hp = 0; }

            invulnTimer = tuning.Player.hitInvulnDuration;

            bool wasBanking = bank.IsBanking;
            if (wasBanking)
            {
                // FR-208: interrupt, normal penalty, then the lockout. Order matters — Interrupt
                // raises BankCancelled(true) before the HUD sees the new charge value.
                bank.Interrupt();
            }

            float lossFraction = ChargeLossOverride >= 0f
                ? ChargeLossOverride
                : tuning.Player.chargeLossOnHit;

            long destroyed = charge.ApplyHitPenalty(lossFraction);
            multiplier.ResetToBase();

            PlayerHitArgs args = new PlayerHitArgs(hp, destroyed, transform.position, wasBanking);
            GameEvents.RaisePlayerHit(args);

            if (hp <= 0)
            {
                bank.AbortSilently();
                GameEvents.RaisePlayerDied();
            }

            return true;
        }

        public void ResetForRun()
        {
            hp = tuning.Player.maxHP;
            invulnTimer = 0f;
            ChargeLossOverride = -1f;
            visibleLastFrame = true;

            if (blinkRenderers == null) { return; }
            for (int i = 0; i < blinkRenderers.Length; i++)
            {
                blinkRenderers[i].enabled = true;
            }
        }
    }
}
