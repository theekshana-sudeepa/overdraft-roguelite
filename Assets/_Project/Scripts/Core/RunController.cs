using UnityEngine;
using UnityEngine.InputSystem;
using Overdraft.Economy;
using Overdraft.Enemies;
using Overdraft.Player;

namespace Overdraft.Core
{
    /// <summary>
    /// The single source of truth for "what is happening" (docs/01 §5), and the one place the
    /// fixed-step simulation is ordered.
    /// <para>
    /// Every gameplay system is ticked from here in an explicit sequence rather than from its
    /// own FixedUpdate. Unity does not guarantee an order between MonoBehaviours, and C8 requires
    /// the simulation to be deterministic from a seed — an implicit order would make that
    /// impossible to hold as the project grows.
    /// </para>
    /// </summary>
    public sealed class RunController : MonoBehaviour
    {
        [SerializeField] TuningConfig tuning;

        [Header("Player")]
        [SerializeField] PlayerInputRouter input;
        [SerializeField] PlayerController player;
        [SerializeField] PlayerHealth health;
        [SerializeField] DashModule dash;
        [SerializeField] Weapon weapon;

        [Header("Economy")]
        [SerializeField] ChargeSystem charge;
        [SerializeField] MultiplierSystem multiplier;
        [SerializeField] ScoreSystem score;
        [SerializeField] BankSystem bank;

        [Header("World")]
        [SerializeField] EnemyManager enemies;
        [SerializeField] EnemySpawner spawner;

        float stateTimer;
        float runTime;
        int kills;
        int runSeed;
        bool restartArmed;

        public RunState State { get; private set; } = RunState.Intro;
        public float RunTime => runTime;
        public int Kills => kills;
        public int RiskTierIndex { get; set; }

        /// <summary>True while the player is expected to be playing. Drives HUD visibility.</summary>
        public bool IsGameplayActive => State == RunState.WaveActive;

        public ChargeSystem Charge => charge;
        public MultiplierSystem Multiplier => multiplier;
        public ScoreSystem Score => score;
        public BankSystem Bank => bank;
        public PlayerHealth Health => health;
        public DashModule Dash => dash;
        public PlayerController Player => player;

        void Awake()
        {
            if (tuning == null) { tuning = ServiceLocator.Tuning; }
            ServiceLocator.Run = this;
        }

        void OnEnable()
        {
            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.PlayerDied += OnPlayerDied;
            GameEvents.BankCompleted += OnBankCompleted;
        }

        void OnDisable()
        {
            GameEvents.EnemyKilled -= OnEnemyKilled;
            GameEvents.PlayerDied -= OnPlayerDied;
            GameEvents.BankCompleted -= OnBankCompleted;
        }

        void OnDestroy()
        {
            if (ServiceLocator.Run == this) { ServiceLocator.Run = null; }
        }

        void Start()
        {
            BeginRun(NewSeed());
        }

        void Update()
        {
            // Restart lives in Update, not FixedUpdate, so death-to-playing is bounded by frame
            // time rather than by the fixed step. G1 puts a hard 2.0s ceiling on this and it is
            // a design requirement, not polish.
            if (State != RunState.Results || !restartArmed) { return; }
            if (!AnyRestartInputThisFrame()) { return; }

            BeginRun(NewSeed());
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            switch (State)
            {
                case RunState.Intro:
                    stateTimer -= dt;
                    if (stateTimer <= 0f) { EnterWaveActive(); }
                    return;

                case RunState.WaveActive:
                    StepGameplay(dt);
                    return;

                case RunState.Death:
                    stateTimer -= dt;
                    if (stateTimer <= 0f) { EnterResults(); }
                    return;

                default:
                    return;
            }
        }

        /// <summary>
        /// THE FIXED-STEP ORDER. Changing it changes the game — read the comments before
        /// reordering anything here.
        /// </summary>
        void StepGameplay(float dt)
        {
            runTime += dt;

            // 1. Bank first. If a bank completes this step, the player is mobile again for the
            //    rest of it; if it is still held, movement below is zeroed.
            bank.OnBankInputChanged(input.BankHeld);
            bank.Tick(dt);

            // 2. Dash requests are refused while banking, so this must follow the bank tick.
            if (input.ConsumeDash())
            {
                dash.TryDash(player.AimDirection);
            }

            dash.Tick(dt);
            health.Tick(dt);

            // 3. Player moves, then enemies move toward where the player now is.
            player.Tick(dt);
            enemies.Tick(dt, player.Position);

            // 4. Projectiles resolve against the freshly rebuilt grid.
            weapon.Tick(dt);

            // 5. Contact damage last, so a player who dashed clear this step is genuinely clear.
            //    Hazards (Spitter projectiles/puddles) and direct enemy-body contact both funnel
            //    into the same TryTakeDamage call — PlayerHealth's invuln window is what prevents
            //    a puddle plus a Drifter from stacking into more than one hit per window.
            int hazardDamage = enemies.AdvanceHazards(dt, player.Position, tuning.Player.hitboxRadius);
            int contactDamage = enemies.QueryPlayerContact(player.Position, tuning.Player.hitboxRadius);
            int totalDamage = Mathf.Max(hazardDamage, contactDamage);
            if (totalDamage > 0)
            {
                health.TryTakeDamage(totalDamage);
            }

            spawner.Tick(dt);

            // 6. MULT decay is the clock the whole game is played against. It runs last so a
            //    kill this step always refreshes grace before any decay can be applied.
            multiplier.Tick(dt);
        }

        void BeginRun(int seed)
        {
            runSeed = seed;
            runTime = 0f;
            kills = 0;
            restartArmed = false;

            RiskTier risk = tuning.Risk(RiskTierIndex);

            charge.ResetForRun();
            multiplier.ResetForRun();
            score.ResetForRun();
            bank.ResetForRun();
            bank.RiskScoreMultiplier = risk.scoreMultiplier;

            health.ResetForRun();
            dash.ResetForRun();
            player.ResetForRun();
            weapon.ResetForRun();
            weapon.SetSeed(runSeed);

            enemies.ReturnAll();
            enemies.RiskSpeedMultiplier = risk.enemySpeedMultiplier;
            spawner.ResetForRun(runSeed);
            spawner.IsSpawning = false;

            input.ClearBuffers();

            State = RunState.Intro;
            stateTimer = tuning.Wave.introDuration;
            GameEvents.RaiseRunRestarted();
        }

        void EnterWaveActive()
        {
            State = RunState.WaveActive;
            spawner.IsSpawning = true;

            // M1 runs one endless wave. Real waves land in M2-T07.
            GameEvents.RaiseWaveStarted(1);
        }

        void EnterResults()
        {
            State = RunState.Results;
            restartArmed = true;

            RunResult result = new RunResult(
                score.Current,
                charge.Current,          // FR-204: unbanked CHARGE is worth exactly zero
                runTime,
                kills,
                score.BankCount,
                multiplier.PeakThisRun,
                false);

            GameEvents.RaiseRunEnded(result);
        }

        void OnEnemyKilled(EnemyKilledArgs args)
        {
            kills++;

            // ⚠️ THE ONE DOCUMENTED EXCEPTION TO THE EVENT BUS (docs/01 §10).
            // Multiplier first, then charge. If these were two independent subscribers, the
            // subscription order would decide whether a kill is valued at the old or the new
            // multiplier — and subscription order is not guaranteed.
            multiplier.AddKill();
            charge.AddFromKill(args.BaseValue, multiplier.Current);
        }

        void OnBankCompleted(long amount)
        {
            float iframes = bank.CompletionIframes;
            if (iframes > 0f) { dash.GrantIframes(iframes); }
        }

        void OnPlayerDied()
        {
            if (State == RunState.Death || State == RunState.Results) { return; }

            State = RunState.Death;
            stateTimer = tuning.Player.deathBeatDuration;

            spawner.IsSpawning = false;
            input.ClearBuffers();
        }

        static bool AnyRestartInputThisFrame()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) { return true; }
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) { return true; }

            Gamepad pad = Gamepad.current;
            if (pad == null) { return false; }

            return pad.buttonSouth.wasPressedThisFrame
                || pad.buttonEast.wasPressedThisFrame
                || pad.startButton.wasPressedThisFrame;
        }

        /// <summary>
        /// M1 runs are unseeded from the player's point of view — ENDLESS, DAILY and CUSTOM SEED
        /// all arrive in M2-T08. The value still feeds the deterministic jitter streams so a run
        /// is internally reproducible today.
        /// </summary>
        static int NewSeed()
        {
            return System.Environment.TickCount;
        }
    }
}
