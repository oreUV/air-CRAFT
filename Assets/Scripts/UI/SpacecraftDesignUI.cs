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
    /// Spacecraft design: add/remove hull, propulsion, power, comms components.
    /// Shows constraint violations and cost/mass/power totals.
    /// </summary>
    public class SpacecraftDesignUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform componentListContainer;
        [SerializeField] private GameObject componentButtonPrefab;
        [SerializeField] private TMP_Text totalMassText;
        [SerializeField] private TMP_Text totalPowerText;
        [SerializeField] private TMP_Text totalCostText;
        [SerializeField] private TMP_Text violationsText;
        [SerializeField] private Button nextButton;
        [SerializeField] private GameObject spacecraftDesignPanel;
        [SerializeField] private GameObject instrumentPanel;

        private MissionManager manager => MissionManager.Instance;
        private SpacecraftDesigner designer;

        private void Awake()
        {
            designer = FindObjectOfType<SpacecraftDesigner>();
            if (designer != null) designer.OnDesignChanged += Refresh;
            if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);
            Refresh();
        }

        private void OnDestroy()
        {
            if (designer != null) designer.OnDesignChanged -= Refresh;
        }

        public void AddComponentByType(ComponentType type)
        {
            if (manager == null || designer == null) return;
            var comp = GetDefaultComponent(type);
            if (comp != null) designer.AddComponent(comp);
        }

        private SpacecraftComponent GetDefaultComponent(ComponentType type)
        {
            // Default components for rapid prototyping; replace with ScriptableObject assets later.
            switch (type)
            {
                case ComponentType.Hull:
                    return new SpacecraftComponent("hull_apollo_cmd", "Apollo Command Module Hull",
                        ComponentType.Hull, 5500, 500, 45_000_000, 0, 0.98, "Command module pressure hull.");
                case ComponentType.Propulsion:
                    return new SpacecraftComponent("prop_service_module", "Service Module Propulsion",
                        ComponentType.Propulsion, 12000, 200, 80_000_000, 0, 0.95, "Main engine and RCS.");
                case ComponentType.Power:
                    return new SpacecraftComponent("power_fuel_cells", "Fuel Cell Power System",
                        ComponentType.Power, 1200, 50, 35_000_000, 0, 0.97, "Electrical power generation.");
                case ComponentType.Communications:
                    return new SpacecraftComponent("comm_dish", "High-Gain Antenna",
                        ComponentType.Communications, 300, 300, 25_000_000, 64, 0.96, "Deep-space communications.");
                case ComponentType.Structure:
                    return new SpacecraftComponent("struct_truss", "Structural Truss",
                        ComponentType.Structure, 2000, 0, 15_000_000, 0, 0.99, "Primary structure.");
                case ComponentType.Thermal:
                    return new SpacecraftComponent("thermal_radiators", "Thermal Radiators",
                        ComponentType.Thermal, 800, 100, 20_000_000, 0, 0.97, "Heat rejection system.");
                default:
                    return null;
            }
        }

        private void Refresh()
        {
            if (manager == null) return;

            var design = manager.SpacecraftDesign;
            if (totalMassText != null) totalMassText.text = $"Mass: {design.TotalMassKg:F0} kg";
            if (totalPowerText != null) totalPowerText.text = $"Power: {design.TotalPowerDrawW:F0} W";
            if (totalCostText != null) totalCostText.text = $"Cost: ${design.TotalCostUsd:F0}";

            var violations = designer.GetConstraintViolations();
            if (violationsText != null)
                violationsText.text = violations.Count > 0
                    ? "Violations:\n• " + string.Join("\n• ", violations)
                    : "All constraints satisfied.";
            violationsText.color = violations.Count > 0 ? Color.red : Color.green;

            if (nextButton != null)
                nextButton.interactable = designer.IsDesignValid();
        }

        private void OnNextClicked()
        {
            if (manager == null) return;
            if (spacecraftDesignPanel != null) spacecraftDesignPanel.SetActive(false);
            if (instrumentPanel != null) instrumentPanel.SetActive(true);
            manager.SetPhase(GamePhase.InstrumentSelection);
        }
    }
}
