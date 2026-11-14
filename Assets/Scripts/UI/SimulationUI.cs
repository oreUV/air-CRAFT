using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceMissionDesign.Data;
using SpaceMissionDesign.Game;

namespace SpaceMissionDesign.UI
{
    /// <summary>
    /// Simulation phase: run the mission simulation and view preliminary results.
    /// </summary>
    public class SimulationUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text simulationStatusText;
        [SerializeField] private Button runButton;
        [SerializeField] private Button viewResultsButton;
        [SerializeField] private GameObject simulationPanel;
        [SerializeField] private GameObject resultsPanel;

        private SimulationEngine engine;
        private MissionManager manager => MissionManager.Instance;

        private void Awake()
        {
            engine = FindObjectOfType<SimulationEngine>();
            if (runButton != null) runButton.onClick.AddListener(OnRunClicked);
            if (viewResultsButton != null)
            {
                viewResultsButton.onClick.AddListener(OnViewResultsClicked);
                viewResultsButton.interactable = false;
            }
        }

        private void OnEnable()
        {
            if (viewResultsButton != null)
                viewResultsButton.interactable = manager != null && manager.SimulationComplete;
        }

        private void OnRunClicked()
        {
            if (engine == null) return;
            engine.RunSimulation();
            if (viewResultsButton != null)
                viewResultsButton.interactable = manager != null && manager.SimulationComplete;
            if (simulationStatusText != null)
                simulationStatusText.text = "Simulation complete. View results.";
        }

        private void OnViewResultsClicked()
        {
            if (manager == null) return;
            if (simulationPanel != null) simulationPanel.SetActive(false);
            if (resultsPanel != null) resultsPanel.SetActive(true);
            manager.SetPhase(GamePhase.Results);
        }
    }
}
