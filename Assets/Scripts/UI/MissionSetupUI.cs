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
    /// Mission setup: choose a mission objective from available options.
    /// </summary>
    public class MissionSetupUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform objectiveListContainer;
        [SerializeField] private GameObject objectiveButtonPrefab;
        [SerializeField] private Button nextButton;
        [SerializeField] private GameObject missionSetupPanel;
        [SerializeField] private GameObject spacecraftDesignPanel;

        [Header("Mission Objectives (JSON in Resources/MissionData)")]
        [SerializeField] private TextAsset[] missionDataFiles;

        private MissionManager manager => MissionManager.Instance;
        private SpacecraftDesigner designer;
        private List<MissionObjective> availableObjectives = new List<MissionObjective>();

        private void Awake()
        {
            designer = FindObjectOfType<SpacecraftDesigner>();
            LoadObjectives();
            PopulateList();
            if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);
            nextButton.interactable = false;
        }

        private void LoadObjectives()
        {
            availableObjectives.Clear();
            foreach (var file in missionDataFiles)
            {
                if (file == null) continue;
                try
                {
                    var obj = JsonUtility.FromJson<MissionObjective>(file.text);
                    if (obj != null) availableObjectives.Add(obj);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to parse mission data {file.name}: {e.Message}");
                }
            }
        }

        private void PopulateList()
        {
            if (objectiveListContainer == null || objectiveButtonPrefab == null) return;

            foreach (var objective in availableObjectives)
            {
                var btnGO = Instantiate(objectiveButtonPrefab, objectiveListContainer);
                var btn = btnGO.GetComponentInChildren<Button>();
                var tmp = btnGO.GetComponentInChildren<TMP_Text>();
                if (tmp != null) tmp.text = objective.missionName;
                if (btn != null)
                {
                    var captured = objective;
                    btn.onClick.AddListener(() => OnObjectiveSelected(captured));
                }
            }
        }

        private void OnObjectiveSelected(MissionObjective objective)
        {
            if (manager == null) return;
            manager.StartNewMission(objective);
            nextButton.interactable = true;
        }

        private void OnNextClicked()
        {
            if (manager == null) return;
            if (missionSetupPanel != null) missionSetupPanel.SetActive(false);
            if (spacecraftDesignPanel != null) spacecraftDesignPanel.SetActive(true);
            manager.SetPhase(GamePhase.SpacecraftDesign);
        }
    }
}
