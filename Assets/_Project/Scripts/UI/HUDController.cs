using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Overdraft.Core;

namespace Overdraft.UI
{
    /// <summary>
    /// The M1 HUD (FR-210, FR-542). Raw TMP, no styling — the art pass is M3.
    /// <para>
    /// Two things here are load-bearing for the Day-3 gate and are already correct: CHARGE is
    /// the largest element on screen and the only gold thing, and it pulses at a rate
    /// proportional to MULT. If the player's eye is on their character instead of on the gold
    /// number, the compulsion loop is not working and no amount of later polish fixes it.
    /// </para>
    /// </summary>
    public sealed class HUDController : MonoBehaviour
    {
        [SerializeField] TuningConfig tuning;

        [Header("Charge")]
        [SerializeField] FastNumberText chargeNumber;
        [SerializeField] RectTransform chargePulseRoot;

        [Header("Multiplier")]
        [SerializeField] TMP_Text multiplierText;
        [SerializeField] RectTransform decayBarFill;
        [SerializeField] Image decayBarImage;

        [Header("Score")]
        [SerializeField] FastNumberText scoreNumber;

        [Header("Wave")]
        [SerializeField] TMP_Text waveText;

        [Header("Status")]
        [SerializeField] Image[] hpPips;
        [SerializeField] Image[] dashPips;
        [SerializeField] TMP_Text stateBanner;

        // Pre-baked so the multiplier readout never builds a string at runtime. Covers 1.0..99.9
        // in 0.1 steps; anything above falls back to the top entry, which is a display cap only
        // and never touches the value used for scoring.
        string[] multiplierStrings;

        // Pre-baked wave labels — waves 1..6 only exist, so this is exhaustive, not a cap.
        string[] waveStrings;

        float pulsePhase;
        long lastCharge;

        void Awake()
        {
            if (tuning == null) { tuning = ServiceLocator.Tuning; }
            BuildMultiplierStrings();
            BuildWaveStrings();
        }

        void OnEnable()
        {
            GameEvents.ChargeChanged += OnChargeChanged;
            GameEvents.ScoreChanged += OnScoreChanged;
            GameEvents.MultiplierChanged += OnMultiplierChanged;
            GameEvents.WaveStarted += OnWaveStarted;
            GameEvents.RunEnded += OnRunEnded;
            GameEvents.RunRestarted += OnRunRestarted;
        }

        void OnDisable()
        {
            GameEvents.ChargeChanged -= OnChargeChanged;
            GameEvents.ScoreChanged -= OnScoreChanged;
            GameEvents.MultiplierChanged -= OnMultiplierChanged;
            GameEvents.WaveStarted -= OnWaveStarted;
            GameEvents.RunEnded -= OnRunEnded;
            GameEvents.RunRestarted -= OnRunRestarted;
        }

        void Update()
        {
            RunController run = ServiceLocator.Run;
            if (run == null) { return; }

            // Unscaled: the HUD must keep breathing during hitstop, or every kill looks like a
            // dropped frame.
            float dt = Time.unscaledDeltaTime;

            UpdateChargePulse(run, dt);
            UpdateDecayBar(run);
            UpdatePips(run);
        }

        void UpdateChargePulse(RunController run, float dt)
        {
            if (chargePulseRoot == null) { return; }

            // 1 + MULT/20 Hz, scale oscillating ±4% (docs/04 §6). The number physically speeds
            // up as the run heats up — that acceleration is the pressure.
            float hz = 1f + run.Multiplier.Current * PulseHzPerMultiplier;
            pulsePhase += dt * hz;

            float scale = 1f + Mathf.Sin(pulsePhase * Mathf.PI * 2f) * PulseAmplitude;
            chargePulseRoot.localScale = new Vector3(scale, scale, 1f);
        }

        void UpdateDecayBar(RunController run)
        {
            if (decayBarFill == null) { return; }

            // Scaled rather than Image.fillAmount: a filled Image needs a sprite asset, and this
            // project ships no textures at all (docs/04 §2). The pivot is at the left edge.
            float grace = tuning.Economy.multDecayGrace;
            float fill = grace > 0f ? Mathf.Clamp01(run.Multiplier.GraceLeft / grace) : 0f;
            decayBarFill.localScale = new Vector3(fill, 1f, 1f);

            if (decayBarImage != null)
            {
                decayBarImage.color = run.Multiplier.IsWarning ? Palette.Warn : Palette.Ion;
            }
        }

        void UpdatePips(RunController run)
        {
            if (hpPips != null)
            {
                int hp = run.Health.CurrentHP;
                for (int i = 0; i < hpPips.Length; i++)
                {
                    bool filled = i < hp;
                    hpPips[i].color = filled ? Palette.Ion : Palette.Grid;
                }
            }

            if (dashPips == null) { return; }

            int charges = run.Dash.ChargesAvailable;
            for (int i = 0; i < dashPips.Length; i++)
            {
                bool filled = i < charges;
                dashPips[i].color = filled ? Palette.Ion : Palette.Grid;
            }
        }

        void OnChargeChanged(long value)
        {
            lastCharge = value;
            if (chargeNumber != null) { chargeNumber.SetValue(value); }
        }

        void OnScoreChanged(long value)
        {
            if (scoreNumber != null) { scoreNumber.SetValue(value); }
        }

        void OnWaveStarted(int wave)
        {
            if (waveText == null) { return; }
            int index = Mathf.Clamp(wave - 1, 0, waveStrings.Length - 1);
            waveText.SetText(waveStrings[index]);
        }

        void OnMultiplierChanged(float value)
        {
            if (multiplierText == null) { return; }

            int index = Mathf.Clamp(Mathf.RoundToInt(value * 10f), 0, multiplierStrings.Length - 1);
            multiplierText.SetText(multiplierStrings[index]);
        }

        void OnRunEnded(RunResult result)
        {
            if (stateBanner == null) { return; }

            // M1 has no results screen (M2-T17). This banner exists only so the Day-3 tester
            // can see that unbanked CHARGE was worth nothing.
            stateBanner.gameObject.SetActive(true);
            stateBanner.SetText(result.UnbankedChargeLost > 0L
                ? "DEAD — PRESS ANY KEY\nUNBANKED LOST"
                : "DEAD — PRESS ANY KEY");
        }

        void OnRunRestarted()
        {
            if (stateBanner != null) { stateBanner.gameObject.SetActive(false); }
            if (chargeNumber != null) { chargeNumber.Invalidate(); }
            if (scoreNumber != null) { scoreNumber.Invalidate(); }
            if (waveText != null) { waveText.SetText(waveStrings[0]); }
            lastCharge = 0L;
            pulsePhase = 0f;
        }

        void BuildMultiplierStrings()
        {
            multiplierStrings = new string[MultiplierStringCount];
            for (int i = 0; i < MultiplierStringCount; i++)
            {
                multiplierStrings[i] = "x" + (i * 0.1f).ToString("0.0");
            }
        }

        void BuildWaveStrings()
        {
            waveStrings = new string[TotalWaves];
            for (int i = 0; i < TotalWaves; i++)
            {
                waveStrings[i] = "WAVE " + (i + 1) + "/" + TotalWaves;
            }
        }

        /// <summary>Exposed for the HUD readability check in the Day-3 gate notes.</summary>
        public long DisplayedCharge => lastCharge;

        const float PulseHzPerMultiplier = 1f / 20f;
        const float PulseAmplitude = 0.04f;
        const int MultiplierStringCount = 1000;
        const int TotalWaves = 6;
    }
}
