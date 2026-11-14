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
    /// Results phase: display simulation results, score, feedback, and player rankings.
    /// </summary>
    public class ResultsUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text resultsText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private TMP_Text rankingsText;
        [SerializeField] private Button restartButton;
        [SerializeField] private GameObject resultsPanel;

        private MissionManager manager => MissionManager.Instance;
        private ScoreManager scoreManager;

        private void Awake()
        {
            scoreManager = FindObjectOfType<ScoreManager>();
            if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
            Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (manager == null) return;

            var result = manager.SimulationResult;
            if (result == null) return;

            if (resultsText != null)
                resultsText.text = result.missionSuccess
                    ? "MISSION SUCCESS"
                    : "MISSION FAILED";
            resultsText.color = result.missionSuccess ? Color.green : Color.red;

            if (scoreText != null)
                scoreText.text = $"Mission Score: {result.score:F1} / 100";

            if (feedbackText != null)
                feedbackText.text = string.Join("\n", result.feedbackMessages);

            if (scoreManager != null && rankingsText != null)
                rankingsText.text = scoreManager.GetRankingText();
        }

        private void OnRestartClicked()
        {
            if (manager == null) return;
            manager.ResetMission();
            if (resultsPanel != null) resultsPanel.SetActive(false);
        }
    }
}
