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
    /// Main menu: start new mission, set player count (1-4).
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_InputField playerCountInput;
        [SerializeField] private Button startButton;
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject missionSetupPanel;

        private MissionManager manager => MissionManager.Instance;

        private void Awake()
        {
            if (playerCountInput != null) playerCountInput.text = "1";
            if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        }

        private void OnStartClicked()
        {
            if (manager == null) return;
            int count = 1;
            if (playerCountInput != null && int.TryParse(playerCountInput.text, out int parsed))
                count = Mathf.Clamp(parsed, 1, 4);
            manager.SetPlayerCount(count);
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (missionSetupPanel != null) missionSetupPanel.SetActive(true);
            manager.SetPhase(GamePhase.MissionSetup);
        }
    }
}
