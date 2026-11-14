using System;
using System.Collections.Generic;
using UnityEngine;
using SpaceMissionDesign.Data;

namespace SpaceMissionDesign.Game
{
    /// <summary>
    /// Game phase enumeration for the mission design flow.
    /// </summary>
    public enum GamePhase
    {
        MainMenu,
        MissionSetup,
        SpacecraftDesign,
        InstrumentSelection,
        LaunchSelection,
        Simulation,
        Results
    }

    /// <summary>
    /// Owns mission state, phase flow, and player turns. Single instance per game session.
    /// </summary>
    public sealed class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        /// <summary>Budget slider range (USD). The cap was raised from $500M to $2B.</summary>
        public const double MinBudgetUsd = 250_000_000;
        public const double MaxBudgetUsd = 2_000_000_000;
        public const double DefaultBudgetUsd = 1_000_000_000;

        [Header("Mission State")]
        [SerializeField] private MissionObjective currentObjective;
        [SerializeField] private MissionConstraints constraints;
        [SerializeField] private SpacecraftDesign spacecraftDesign;
        [SerializeField] private GamePhase currentPhase;
        [SerializeField] private int currentPlayerIndex;

        [Header("Multiplayer")]
        [SerializeField] private int playerCount = 1;
        [SerializeField] private List<string> playerNames = new List<string> { "Player 1" };

        [Header("Simulation Results")]
        [SerializeField] private bool simulationComplete;
        [SerializeField] private SimulationResult simulationResult;

        public event Action<GamePhase> OnPhaseChanged;
        public event Action<int> OnPlayerChanged;
        public event Action OnSimulationComplete;

        public MissionObjective CurrentObjective => currentObjective;
        public MissionConstraints Constraints => constraints;
        public SpacecraftDesign SpacecraftDesign => spacecraftDesign;
        public GamePhase CurrentPhase => currentPhase;
        public int CurrentPlayerIndex => currentPlayerIndex;
        public int PlayerCount => playerCount;
        public List<string> PlayerNames => playerNames;
        public bool SimulationComplete => simulationComplete;
        public SimulationResult SimulationResult => simulationResult;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            spacecraftDesign = new SpacecraftDesign();
            constraints = new MissionConstraints(
                DefaultBudgetUsd,
                50000,
                10000,
                1,
                1000,
                365
            );
            currentPhase = GamePhase.MainMenu;
            playerCount = 1;
            playerNames = new List<string> { "Player 1" };
            currentPlayerIndex = 0;
        }

        public void StartNewMission(MissionObjective objective)
        {
            currentObjective = objective;
            // Mission lifetime comes from the objective (Apollo 11 is ~8 days, not a full year).
            constraints.missionLifetimeDays = Mathf.Clamp((float)objective.maxMissionLifetimeDays, 1f, 365f);
            // Each new mission starts at the default budget so the slider is predictable.
            constraints.budgetUsd = DefaultBudgetUsd;
            spacecraftDesign = new SpacecraftDesign { spacecraftName = objective.missionName + " Spacecraft" };
            simulationComplete = false;
            simulationResult = null;
            currentPlayerIndex = 0;
            SetPhase(GamePhase.MissionSetup);
        }

        public void SetPlayerCount(int count)
        {
            playerCount = Mathf.Clamp(count, 1, 4);
            playerNames = new List<string>();
            for (int i = 0; i < playerCount; i++)
                playerNames.Add($"Player {i + 1}");
            currentPlayerIndex = 0;
        }

        public void SetPlayerName(int index, string name)
        {
            if (index >= 0 && index < playerNames.Count)
                playerNames[index] = string.IsNullOrWhiteSpace(name) ? $"Player {index + 1}" : name;
        }

        public void AdvancePlayer()
        {
            currentPlayerIndex = (currentPlayerIndex + 1) % playerCount;
            OnPlayerChanged?.Invoke(currentPlayerIndex);
        }

        public void SetPhase(GamePhase phase)
        {
            currentPhase = phase;
            OnPhaseChanged?.Invoke(phase);
        }

        public void CompleteSimulation(SimulationResult result)
        {
            simulationComplete = true;
            simulationResult = result;
            SetPhase(GamePhase.Results);
            OnSimulationComplete?.Invoke();
        }

        public void ResetMission()
        {
            spacecraftDesign = new SpacecraftDesign();
            simulationComplete = false;
            simulationResult = null;
            currentPlayerIndex = 0;
            SetPhase(GamePhase.MainMenu);
        }

        public bool CanAfford(double cost)
        {
            double spent = spacecraftDesign.TotalCostUsd;
            return (spent + cost) <= constraints.budgetUsd;
        }

        /// <summary>Sets the mission budget, clamped to the slider range.</summary>
        public void SetBudget(double usd)
        {
            constraints.budgetUsd = Math.Max(MinBudgetUsd, Math.Min(MaxBudgetUsd, usd));
        }

        public bool MassOK(double additionalMass)
        {
            return (spacecraftDesign.TotalMassKg + additionalMass) <= constraints.maxMassKg;
        }

        public bool PowerOK(double additionalPower)
        {
            return (spacecraftDesign.TotalPowerDrawW + additionalPower) <= constraints.maxPowerW;
        }

        public bool DataRateOK()
        {
            return spacecraftDesign.TotalDataRateKbps >= constraints.minDataRateKbps;
        }
    }

    /// <summary>
    /// Result of running the mission simulation.
    /// </summary>
    [Serializable]
    public class SimulationResult
    {
        public bool missionSuccess;
        public double totalScienceValue;
        public double totalCostUsd;
        public double totalMassKg;
        public double missionLifetimeDays;
        public double score;
        public List<string> feedbackMessages = new List<string>();
        public Dictionary<string, double> constraintMargins = new Dictionary<string, double>();

        public SimulationResult() { }
    }
}
