using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Overdraft.Economy;
using Overdraft.Enemies;
using Overdraft.Generation;
using Overdraft.Player;
using Overdraft.UI;

namespace Overdraft.Core
{
    /// <summary>
    /// The single source of truth for "what is happening" (docs/01 §5), and the one place the
    /// fixed-step simulation is ordered.
    /// <para>
    /// Every gameplay system is ticked from here in an explicit sequence rather than from its
    /// own FixedUpdate — Unity does not guarantee an order between MonoBehaviours, and C8
    /// requires the simulation to be deterministic from a seed.
    /// </para>
    /// <para>
    /// ⚠️ Wave 6 clearing currently goes straight to Results. THE AUDITOR (M2-T11–T14) inserts a
    /// BossIntro → BossActive → BossDefeated sequence at that exact transition point on Day 6 —
    /// this is a sequencing choice, not a scope cut; see the session summary.
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
        [SerializeField] UpgradeSystem upgrades;

        [Header("World")]
        [SerializeField] EnemyManager enemies;
        [SerializeField] EnvironmentController environment;

        [Header("UI")]
        [SerializeField] UpgradeChoiceUI upgradeUI;

        float stateTimer;
        float runTime;
        int kills;
        int runSeed;
        bool restartArmed;

        int currentWave;
        readonly List<SpawnEvent> pendingSpawns = new List<SpawnEvent>(64);
        int nextSpawnIndex;
        float waveElapsed;

        readonly List<UpgradeOffer> currentOffers = new List<UpgradeOffer>(4);
        readonly int[] offerTierBuffer = new int[4];
        int offerRoundIndex;

        EnemyType[] rulesTypes1, rulesTypes2, rulesTypes3;
        int[] rulesWeights1, rulesWeights2, rulesWeights3;
        int[] rulesCosts1, rulesCosts2, rulesCosts3;

        public RunState State { get; private set; } = RunState.Intro;
        public float RunTime => runTime;
        public int Kills => kills;
        public int CurrentWave => currentWave;
        public int RiskTierIndex { get; set; }

        public bool IsGameplayActive => State == RunState.WaveActive || State == RunState.BossActive;

        public ChargeSystem Charge => charge;
        public MultiplierSystem Multiplier => multiplier;
        public ScoreSystem Score => score;
        public BankSystem Bank => bank;
        public PlayerHealth Health => health;
        public DashModule Dash => dash;
        public PlayerController Player => player;
        public UpgradeSystem Upgrades => upgrades;

        void Awake()
        {
            if (tuning == null) { tuning = ServiceLocator.Tuning; }
            ServiceLocator.Run = this;
            CacheWaveRuleArrays();
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
            // time rather than by the fixed step. G1 puts a hard 2.0s ceiling on this.
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
                    if (stateTimer <= 0f) { EnterWaveActive(1); }
                    return;

                case RunState.WaveActive:
                    StepGameplay(dt);
                    return;

                case RunState.WaveCleared:
                    stateTimer -= dt;
                    if (stateTimer <= 0f) { EnterUpgradeChoiceOrNextWave(); }
                    return;

                case RunState.UpgradeChoice:
                    return;   // waiting on UpgradeChoiceUI; see OnUpgradeChosen

                case RunState.Death:
                    stateTimer -= dt;
                    if (stateTimer <= 0f) { EnterResults(); }
                    return;

                default:
                    return;
            }
        }

        /// <summary>
        /// THE FIXED-STEP ORDER for live gameplay. Changing it changes the game — read the
        /// comments before reordering anything here.
        /// </summary>
        void StepGameplay(float dt)
        {
            runTime += dt;
            upgrades.Tick();

            // 1. Bank first. If a bank completes this step, the player is mobile again for the
            //    rest of it; if it is still held, movement below is zeroed.
            bank.OnBankInputChanged(input.BankHeld);
            bank.Tick(dt);

            // 2. Dash requests are refused while banking (unless LEECH T2), so this follows bank.
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

            // 5. Every damage source funnels into one TryTakeDamage call; PlayerHealth's invuln
            //    window is what prevents more than one hit per window regardless of how many
            //    hazards overlapped simultaneously.
            int hazardDamage = enemies.AdvanceHazards(dt, player.Position, tuning.Player.hitboxRadius);
            int vaultDamage = environment.TickHazards(dt);
            int contactDamage = enemies.QueryPlayerContact(player.Position, tuning.Player.hitboxRadius);
            int totalDamage = Mathf.Max(hazardDamage, Mathf.Max(vaultDamage, contactDamage));
            if (totalDamage > 0)
            {
                health.TryTakeDamage(totalDamage);
            }

            multiplier.Tick(dt);

            // 6. Wave spawn scheduling and clear/timeout check, last.
            TickWaveSpawning(dt);
        }

        void TickWaveSpawning(float dt)
        {
            waveElapsed += dt;

            while (nextSpawnIndex < pendingSpawns.Count && pendingSpawns[nextSpawnIndex].DelaySeconds <= waveElapsed)
            {
                SpawnEvent e = pendingSpawns[nextSpawnIndex];
                nextSpawnIndex++;

                float x = Mathf.Sin(e.RingAngleRad) * e.RingRadius;
                float z = Mathf.Cos(e.RingAngleRad) * e.RingRadius;
                Vector3 pos = Utility.MathX.ClampToCircleXZ(
                    new Vector3(x, 0f, z), environment.CurrentArenaRadius - tuning.Sim.maxEnemyRadius);

                // Null means the pool is exhausted. Skip silently (docs/01 §6) — never grow it.
                enemies.Spawn(e.Type, pos, e.IsElite);
            }

            // QA case 1: a bank in progress when the last enemy dies must complete FIRST — the
            // wave is not allowed to advance mid-bank.
            if (bank.IsBanking) { return; }

            bool allSpawned = nextSpawnIndex >= pendingSpawns.Count;
            bool allDead = enemies.AliveCount == 0;
            bool timedOut = waveElapsed >= tuning.Wave.waveTimeout;

            if ((allSpawned && allDead) || timedOut)
            {
                EnterWaveCleared();
            }
        }

        void BeginRun(int seed)
        {
            runSeed = seed;
            runTime = 0f;
            kills = 0;
            currentWave = 0;
            offerRoundIndex = 0;
            restartArmed = false;

            RiskTier risk = tuning.Risk(RiskTierIndex);

            charge.ResetForRun();
            multiplier.ResetForRun();
            score.ResetForRun();
            upgrades.ResetForRun();
            bank.ResetForRun();
            bank.RiskScoreMultiplier = risk.scoreMultiplier;

            health.ResetForRun();
            dash.ResetForRun();
            player.ResetForRun();
            weapon.ResetForRun();
            weapon.SetSeed(runSeed);

            enemies.ReturnAll();
            enemies.RiskSpeedMultiplier = risk.enemySpeedMultiplier;

            environment.ResetForRun();
            upgradeUI.Hide();

            input.ClearBuffers();

            State = RunState.Intro;
            stateTimer = tuning.Wave.introDuration;
            GameEvents.RaiseRunRestarted();
        }

        void EnterWaveActive(int wave)
        {
            currentWave = wave;

            // FR-451: environment swap happens on entry to WaveActive(4). No scene load.
            if (wave == 4)
            {
                environment.SwitchTo(EnvironmentId.Vault);
            }

            RiskTier risk = tuning.Risk(RiskTierIndex);
            WaveRules rules = BuildWaveRules(wave);
            WaveGenerator.Generate(runSeed, wave, risk.enemyBudgetMultiplier, rules, pendingSpawns);

            nextSpawnIndex = 0;
            waveElapsed = 0f;

            State = RunState.WaveActive;
            GameEvents.RaiseWaveStarted(wave);
        }

        void EnterWaveCleared()
        {
            State = RunState.WaveCleared;
            stateTimer = tuning.Wave.interWaveDelay;
            GameEvents.RaiseWaveCleared(currentWave);
        }

        void EnterUpgradeChoiceOrNextWave()
        {
            if (currentWave >= 6)
            {
                // TODO(M2-T11–T14): THE AUDITOR belongs here — BossIntro -> BossActive ->
                // BossDefeated -> forced free bank -> Results. Until Day 6 lands, a cleared
                // wave 6 ends the run directly so the loop stays honestly playable end to end.
                EnterResults();
                return;
            }

            upgrades.CopyTiersInto(offerTierBuffer);
            UpgradeOfferGenerator.Generate(runSeed, offerRoundIndex, offerTierBuffer, currentOffers, tuning.Wave.upgradeOffersShown);
            offerRoundIndex++;

            if (currentOffers.Count == 0)
            {
                // Every line maxed — nothing to offer (FR-472: "offer what exists").
                EnterWaveActive(currentWave + 1);
                return;
            }

            State = RunState.UpgradeChoice;
            upgradeUI.Show(currentOffers, OnUpgradeChosen);
        }

        void OnUpgradeChosen(int offerIndex)
        {
            if (offerIndex < 0 || offerIndex >= currentOffers.Count) { return; }

            UpgradeOffer offer = currentOffers[offerIndex];
            upgrades.Take(offer.Line, offer.Tier);

            EnterWaveActive(currentWave + 1);
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
            // Multiplier first, then charge — subscription order is not guaranteed, so these two
            // cannot be independent bus subscribers. GREED T3's value multiplier is applied here
            // too, since RunController already owns this special-cased kill handling.
            multiplier.AddKill();
            int scaledValue = Mathf.RoundToInt(args.BaseValue * upgrades.GreedValueMultiplier);
            charge.AddFromKill(scaledValue, multiplier.Current);
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

            upgradeUI.Hide();
            input.ClearBuffers();
        }

        WaveRules BuildWaveRules(int wave)
        {
            bool elitesAllowed = wave >= 5;

            EnemyType[] types;
            int[] weights;
            int[] costs;

            if (wave <= 1) { types = rulesTypes1; weights = rulesWeights1; costs = rulesCosts1; }
            else if (wave == 2) { types = rulesTypes2; weights = rulesWeights2; costs = rulesCosts2; }
            else { types = rulesTypes3; weights = rulesWeights3; costs = rulesCosts3; }

            return new WaveRules(
                wave, types, weights, costs, elitesAllowed, tuning.Wave.eliteWeightPercent,
                tuning.Wave.waveTimeout, tuning.Wave.spawnRingMin, tuning.Wave.spawnRingMax,
                tuning.Elite.budgetMultiplier,
                tuning.Wave.budgetBase, tuning.Wave.budgetExponent, tuning.Wave.budgetScale);
        }

        void CacheWaveRuleArrays()
        {
            rulesTypes1 = new[] { EnemyType.Drifter };
            rulesWeights1 = new[] { tuning.Wave.drifterWeight };
            rulesCosts1 = new[] { tuning.Wave.drifterCost };

            rulesTypes2 = new[] { EnemyType.Drifter, EnemyType.Spitter };
            rulesWeights2 = new[] { tuning.Wave.drifterWeight, tuning.Wave.spitterWeight };
            rulesCosts2 = new[] { tuning.Wave.drifterCost, tuning.Wave.spitterCost };

            rulesTypes3 = new[] { EnemyType.Drifter, EnemyType.Spitter, EnemyType.Splitter };
            rulesWeights3 = new[] { tuning.Wave.drifterWeight, tuning.Wave.spitterWeight, tuning.Wave.splitterWeight };
            rulesCosts3 = new[] { tuning.Wave.drifterCost, tuning.Wave.spitterCost, tuning.Wave.splitterCost };
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
        /// M2 runs are still unseeded from the player's point of view — ENDLESS/DAILY/CUSTOM
        /// SEED mode selection is M2-T15. The value already drives WaveGenerator, so a run is
        /// internally reproducible today; only the "pick a mode" layer is missing.
        /// </summary>
        static int NewSeed()
        {
            return System.Environment.TickCount;
        }
    }
}
