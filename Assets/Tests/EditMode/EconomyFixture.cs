using System.Collections.Generic;
using UnityEngine;
using Overdraft.Core;
using Overdraft.Economy;
using Overdraft.EditorTools;
using Overdraft.Player;

namespace Overdraft.Tests
{
    /// <summary>
    /// Builds a live economy out of real components, wired the way RunController wires them.
    /// <para>
    /// The systems are ticked explicitly rather than by Unity, which is exactly how they run in
    /// the game (RunController.StepGameplay). That means these tests exercise the shipping code
    /// path, not a parallel simulation of it.
    /// </para>
    /// </summary>
    public sealed class EconomyFixture
    {
        readonly List<GameObject> spawned = new List<GameObject>();

        public TuningConfig Tuning { get; }
        public ChargeSystem Charge { get; }
        public MultiplierSystem Multiplier { get; }
        public ScoreSystem Score { get; }
        public BankSystem Bank { get; }
        public PlayerHealth Health { get; }
        public DashModule Dash { get; }

        /// <summary>One fixed step, matching the shipping 60 Hz timestep (C8).</summary>
        public const float Step = 1f / 60f;

        public EconomyFixture()
        {
            GameEvents.ClearAll();

            Tuning = DataAssetFactory.BuildTuningConfig();

            GameObject systems = New("Systems");
            Charge = systems.AddComponent<ChargeSystem>();
            Multiplier = systems.AddComponent<MultiplierSystem>();
            Score = systems.AddComponent<ScoreSystem>();
            Bank = systems.AddComponent<BankSystem>();

            GameObject player = New("Player");
            Dash = player.AddComponent<DashModule>();
            Health = player.AddComponent<PlayerHealth>();

            Charge.ResetForRun();
            Multiplier.Configure(Tuning);
            Score.ResetForRun();
            Bank.Configure(Tuning, Charge, Multiplier, Score);
            Bank.ResetForRun();
            Dash.Configure(Tuning, Bank, Multiplier);
            Health.Configure(Tuning, Charge, Multiplier, Bank, Dash);
        }

        /// <summary>Mirrors RunController's kill handler: multiplier first, then charge (docs/01 §10).</summary>
        public void Kill(int baseValue)
        {
            Multiplier.AddKill();
            Charge.AddFromKill(baseValue, Multiplier.Current);
        }

        /// <summary>Advances the economy by seconds of fixed steps, in the shipping order.</summary>
        public void Advance(float seconds, bool bankHeld = false)
        {
            int steps = Mathf.RoundToInt(seconds / Step);
            for (int i = 0; i < steps; i++)
            {
                Bank.OnBankInputChanged(bankHeld);
                Bank.Tick(Step);
                Dash.Tick(Step);
                Health.Tick(Step);
                Multiplier.Tick(Step);
            }
        }

        public void SetRiskTier(int index)
        {
            Bank.RiskScoreMultiplier = Tuning.Risk(index).scoreMultiplier;
        }

        public void Dispose()
        {
            GameEvents.ClearAll();

            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null) { Object.DestroyImmediate(spawned[i]); }
            }
            spawned.Clear();

            if (Tuning != null) { Object.DestroyImmediate(Tuning); }
        }

        GameObject New(string name)
        {
            GameObject go = new GameObject(name);
            spawned.Add(go);
            return go;
        }
    }
}
