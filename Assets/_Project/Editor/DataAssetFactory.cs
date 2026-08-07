using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Overdraft.Core;
using Overdraft.Enemies;
using Overdraft.Generation;

namespace Overdraft.EditorTools
{
    /// <summary>
    /// Creates every data asset from the values in docs/00_TUNING.md.
    /// <para>
    /// The tuning document is the source of truth; this file is its transcription. When a value
    /// changes, it changes in the document first and here second — never the other way round.
    /// </para>
    /// </summary>
    public static class DataAssetFactory
    {
        public const string DataRoot = "Assets/_Project/Data";
        public const string ArtRoot = "Assets/_Project/Art";
        public const string InputRoot = "Assets/_Project/Input";

        public static TuningConfig CreateTuningConfig()
        {
            TuningConfig config = BuildTuningConfig();
            SaveAsset(config, DataRoot + "/TuningConfig.asset");
            return config;
        }

        /// <summary>
        /// Populates a TuningConfig in memory without touching the asset database. The EditMode
        /// suite uses this so the tests run against the real shipping numbers rather than against
        /// a second, silently drifting copy of them.
        /// </summary>
        public static TuningConfig BuildTuningConfig()
        {
            TuningConfig config = ScriptableObject.CreateInstance<TuningConfig>();

            config.Player = new PlayerTuning
            {
                moveSpeed = 8.0f,
                acceleration = 60.0f,
                deceleration = 80.0f,
                facingTurnSpeed = 900.0f,
                hitboxRadius = 0.35f,   // deliberately smaller than visualRadius — do not "fix"
                visualRadius = 0.50f,
                maxHP = 3,
                hitInvulnDuration = 1.00f,
                invulnBlinkHz = 12.0f,
                chargeLossOnHit = 0.50f,
                deathBeatDuration = 1.00f
            };

            config.Weapon = new WeaponTuning
            {
                fireInterval = 0.12f,
                projectileSpeed = 22.0f,
                projectileRadius = 0.15f,
                projectileLifetime = 1.60f,
                projectileDamage = 1,
                baseSpreadDegrees = 3.0f,
                poolSize = 512
            };

            config.Dash = new DashTuning
            {
                distance = 4.50f,
                duration = 0.18f,
                iframeDuration = 0.13f,
                chargeCooldown = 1.50f,
                maxCharges = 2,
                perfectWindow = 0.10f,
                perfectMultBonus = 2.00f,
                perfectHitstop = 0.15f
            };

            config.Economy = new EconomyTuning
            {
                multStart = 1.00f,
                multPerKill = 0.50f,
                multMax = 50.0f,
                multDecayGrace = 1.50f,
                multDecayRate = 1.00f,
                multWarnThreshold = 0.40f,
                multMin = 1.00f
            };

            config.Bank = new BankTuning
            {
                duration = 1.20f,
                fireRateMultiplier = 0.50f,
                interruptCooldown = 2.00f,
                completeHitstop = 0.12f,
                leechT1Duration = 0.95f,
                leechT2Duration = 0.70f,
                leechT3Duration = 0.50f,
                leechT3IframeDuration = 0.50f,
                leechT2DashRefund = 0.30f
            };

            config.Wave = new WaveTuning
            {
                budgetBase = 60.0f,
                budgetExponent = 1.60f,
                budgetScale = 22.0f,
                waveTimeout = 45.0f,
                spawnRingMin = 12.0f,
                spawnRingMax = 16.0f,
                interWaveDelay = 1.20f,
                introDuration = 1.50f,
                upgradeOffersShown = 3,

                m1SpawnInterval = 1.10f,
                m1SpawnIntervalMin = 0.22f,
                m1SpawnRampPerSecond = 0.008f,
                m1MaxAlive = 120,

                drifterWeight = 70,
                spitterWeight = 20,
                splitterWeight = 10,
                drifterCost = 10,
                spitterCost = 40,
                splitterCost = 25,
                eliteWeightPercent = 20
            };

            config.Boss = new BossTuning
            {
                maxHP = 900,
                phase2Threshold = 0.66f,
                phase3Threshold = 0.33f,
                phaseTransitionInvuln = 1.00f,
                laserRotationSpeed = 40.0f,
                laserSpokes = 6,
                laserWidth = 0.55f,
                drifterSpawnInterval = 4.00f,
                auditDrainRate = 0.08f,
                auditWindowDuration = 4.00f,
                auditInterval = 9.00f,
                fragmentCount = 6,
                fragmentHP = 60,
                fragmentOrbitSpeed = 55.0f,
                fragmentOrbitRadius = 5.00f,
                victorySlowMoDuration = 1.50f
            };

            config.Arena = new ArenaTuning
            {
                floorRadius = 18.0f,
                vaultRadius = 14.0f,
                floorOrthoSize = 12.0f,
                vaultOrthoSize = 10.0f,
                environmentLerpTime = 1.50f,
                hazardCycleDuration = 4.00f,
                hazardBarCount = 3,
                hazardBarWidth = 0.70f,
                hazardTelegraphTime = 0.80f
            };

            config.Elite = new EliteTuning
            {
                hpMultiplier = 2.50f,
                speedMultiplier = 1.25f,
                valueMultiplier = 2.50f,
                budgetMultiplier = 2.50f,
                scaleMultiplier = 1.40f,
                outlineWidth = 0.06f,
                emissionBoost = 1.60f
            };

            config.Pools = new PoolTuning
            {
                drifter = 220,
                spitter = 40,
                splitter = 60,
                enemyProjectile = 256,
                puddle = 24
            };

            config.EnemyCombat = new EnemyCombatTuning
            {
                // 9.0 preferredRange / 6.0 projectileSpeed = 1.5s minimum flight time; tripled
                // for margin since the player can be moving away when a shot is fired.
                projectileLifetime = 3.00f,
                projectileRadius = 0.18f
            };

            config.Sim = new SimTuning
            {
                gridCellSize = 2.00f,
                separationRadius = 0.45f,
                separationStrength = 3.50f,
                maxEnemyRadius = 0.70f
            };

            config.Feel = new FeelTuning
            {
                hitstopOnKill = 0.045f,
                hitstopOnPlayerHit = 0.090f,
                hitstopScale = 0.02f,

                traumaOnKill = 0.15f,
                traumaOnPlayerHit = 0.55f,
                traumaOnBankComplete = 0.35f,
                traumaOnBossPhase = 0.70f,
                traumaDecayRate = 1.80f,
                shakeMaxOffset = 0.60f,
                shakeMaxRotation = 3.00f,
                shakeFrequency = 22.0f,

                zoomPunchAmount = 0.04f,
                zoomPunchDuration = 0.08f,
                cameraFollowLerp = 6.00f,
                cameraAimLead = 0.25f,

                caBase = 0.08f,
                caOnHit = 0.40f,
                caDuringBank = 0.25f,
                vignetteBase = 0.32f,
                vignetteDuringBank = 0.55f,
                bankDesaturation = 0.85f
            };

            config.RiskTiers = new[]
            {
                new RiskTier { id = "RISK_I",   enemySpeedMultiplier = 1.00f, enemyBudgetMultiplier = 1.00f, scoreMultiplier = 1.0f },
                new RiskTier { id = "RISK_II",  enemySpeedMultiplier = 1.20f, enemyBudgetMultiplier = 1.15f, scoreMultiplier = 1.3f },
                new RiskTier { id = "RISK_III", enemySpeedMultiplier = 1.35f, enemyBudgetMultiplier = 1.30f, scoreMultiplier = 1.7f },
                new RiskTier { id = "RISK_IV",  enemySpeedMultiplier = 1.50f, enemyBudgetMultiplier = 1.50f, scoreMultiplier = 2.2f },
                new RiskTier { id = "RISK_V",   enemySpeedMultiplier = 1.70f, enemyBudgetMultiplier = 1.75f, scoreMultiplier = 3.0f }
            };

            config.CreditRankThresholds = new long[]
            {
                0, 25_000, 75_000, 150_000, 280_000, 480_000,
                800_000, 1_300_000, 2_100_000, 3_400_000, 5_500_000, 9_000_000
            };

            return config;
        }

        public static EnemyDefinition CreateDrifterDefinition()
        {
            EnemyDefinition def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.type = EnemyType.Drifter;
            def.maxHP = 1;
            def.moveSpeed = 3.2f;
            def.hitboxRadius = 0.40f;
            def.scoreValue = 10;
            def.budgetCost = 10;
            def.contactDamage = 1;
            def.bodyColor = Palette.Threat;

            SaveAsset(def, DataRoot + "/Enemies/Drifter.asset");
            return def;
        }

        public static EnemyDefinition CreateSpitterDefinition()
        {
            EnemyDefinition def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.type = EnemyType.Spitter;
            def.maxHP = 3;
            def.moveSpeed = 2.0f;   // strafe speed
            def.hitboxRadius = 0.45f;
            def.scoreValue = 40;
            def.budgetCost = 40;
            def.contactDamage = 1;
            def.bodyColor = Palette.Threat;

            def.preferredRange = 9.0f;
            def.fireInterval = 2.20f;
            def.projectileSpeed = 6.0f;
            def.puddleDuration = 2.0f;
            def.puddleRadius = 1.2f;

            SaveAsset(def, DataRoot + "/Enemies/Spitter.asset");
            return def;
        }

        public static EnemyDefinition CreateSplitterDefinition()
        {
            EnemyDefinition def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.type = EnemyType.Splitter;
            def.maxHP = 2;
            def.moveSpeed = 6.5f;   // charge speed
            def.hitboxRadius = 0.40f;
            def.scoreValue = 25;
            def.budgetCost = 25;
            def.contactDamage = 1;
            def.bodyColor = Palette.Splitter;

            def.approachSpeed = 1.5f;
            def.windupDuration = 0.40f;
            def.chargeDuration = 0.90f;
            def.chargeCooldown = 1.60f;
            def.splitCount = 2;
            def.splitScatterRadius = 1.0f;

            SaveAsset(def, DataRoot + "/Enemies/Splitter.asset");
            return def;
        }

        public static Material CreateMaterial(string shaderName, string assetName, Color color, float emissionStrength)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[Overdraft] Shader '{shaderName}' not found. Did the .shader files fail to compile?");
                return null;
            }

            Material material = new Material(shader) { name = assetName };
            material.enableInstancing = true;

            if (material.HasProperty(ShaderIds.BaseColor)) { material.SetColor(ShaderIds.BaseColor, color); }
            if (material.HasProperty("_EmissionStrength")) { material.SetFloat("_EmissionStrength", emissionStrength); }

            SaveAsset(material, ArtRoot + "/Materials/" + assetName + ".mat");
            return material;
        }

        /// <summary>
        /// Builds the Gameplay and UI maps in memory and writes them as a .inputactions file.
        /// Generating the JSON avoids hand-maintaining a file full of GUIDs, which is the single
        /// most merge-hostile asset type in a Unity project.
        /// </summary>
        public static InputActionAsset CreateInputActions()
        {
            InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "OverdraftControls";

            InputActionMap gameplay = new InputActionMap("Gameplay");

            InputAction move = gameplay.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick").WithProcessor("stickDeadzone(min=0.15,max=0.95)");

            // Two bindings, one action (docs/01 §9). The router decides how to interpret the
            // value from the last-used device — the player never picks a mode.
            InputAction aim = gameplay.AddAction("Aim", InputActionType.Value, expectedControlLayout: "Vector2");
            aim.AddBinding("<Mouse>/position");
            aim.AddBinding("<Gamepad>/rightStick").WithProcessor("stickDeadzone(min=0.20,max=0.95)");

            InputAction dash = gameplay.AddAction("Dash", InputActionType.Button);
            dash.AddBinding("<Keyboard>/leftShift");
            dash.AddBinding("<Mouse>/rightButton");
            dash.AddBinding("<Gamepad>/rightShoulder");

            // Polled as "held", never as a hold interaction — the timer belongs in BankSystem
            // where it can be tuned, interrupted and unit-tested.
            InputAction bank = gameplay.AddAction("Bank", InputActionType.Button);
            bank.AddBinding("<Keyboard>/space");
            bank.AddBinding("<Mouse>/leftButton");
            bank.AddBinding("<Gamepad>/buttonSouth");

            InputAction pause = gameplay.AddAction("Pause", InputActionType.Button);
            pause.AddBinding("<Keyboard>/escape");
            pause.AddBinding("<Gamepad>/start");

            InputActionMap ui = new InputActionMap("UI");
            InputAction submit = ui.AddAction("Submit", InputActionType.Button);
            submit.AddBinding("<Keyboard>/enter");
            submit.AddBinding("<Gamepad>/buttonSouth");
            InputAction cancel = ui.AddAction("Cancel", InputActionType.Button);
            cancel.AddBinding("<Keyboard>/escape");
            cancel.AddBinding("<Gamepad>/buttonEast");

            asset.AddActionMap(gameplay);
            asset.AddActionMap(ui);

            Directory.CreateDirectory(InputRoot);
            string path = InputRoot + "/OverdraftControls.inputactions";
            File.WriteAllText(path, asset.ToJson());

            Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }

        public static void SaveAsset(Object asset, string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) { Directory.CreateDirectory(directory); }

            Object existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null) { AssetDatabase.DeleteAsset(path); }

            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
