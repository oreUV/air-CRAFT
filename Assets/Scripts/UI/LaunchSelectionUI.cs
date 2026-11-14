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
    /// Launch selection: choose launch vehicle and orbit for the mission.
    /// </summary>
    public class LaunchSelectionUI : MonoBehaviour
    {
        [Header("Launch Vehicles")]
        [SerializeField] private Transform vehicleListContainer;
        [SerializeField] private GameObject vehicleButtonPrefab;

        [Header("Orbits")]
        [SerializeField] private Transform orbitListContainer;
        [SerializeField] private GameObject orbitButtonPrefab;

        [Header("Navigation")]
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private Button simulateButton;
        [SerializeField] private GameObject launchPanel;
        [SerializeField] private GameObject simulationPanel;

        private MissionManager manager => MissionManager.Instance;
        private SpacecraftDesigner designer;

        private LaunchVehicle selectedVehicle;
        private OrbitDefinition selectedOrbit;

        private void Awake()
        {
            designer = FindObjectOfType<SpacecraftDesigner>();
            PopulateVehicles();
            PopulateOrbits();
            if (simulateButton != null) simulateButton.onClick.AddListener(OnSimulateClicked);
            RefreshSummary();
        }

        private void PopulateVehicles()
        {
            if (vehicleListContainer == null || vehicleButtonPrefab == null) return;
            var vehicles = GetDefaultVehicles();
            foreach (var v in vehicles)
            {
                var btnGO = Instantiate(vehicleButtonPrefab, vehicleListContainer);
                var tmp = btnGO.GetComponentInChildren<TMP_Text>();
                var btn = btnGO.GetComponentInChildren<Button>();
                if (tmp != null)
                    tmp.text = $"{v.displayName} — ${v.costUsd:F0} — {v.maxPayloadKg:F0} kg payload";
                if (btn != null)
                {
                    var captured = v;
                    btn.onClick.AddListener(() => SelectVehicle(captured));
                }
            }
        }

        private void PopulateOrbits()
        {
            if (orbitListContainer == null || orbitButtonPrefab == null) return;
            var orbits = GetDefaultOrbits();
            foreach (var o in orbits)
            {
                var btnGO = Instantiate(orbitButtonPrefab, orbitListContainer);
                var tmp = btnGO.GetComponentInChildren<TMP_Text>();
                var btn = btnGO.GetComponentInChildren<Button>();
                if (tmp != null)
                    tmp.text = $"{o.displayName} — {o.deltaVRequiredKmS:F1} km/s delta-v";
                if (btn != null)
                {
                    var captured = o;
                    btn.onClick.AddListener(() => SelectOrbit(captured));
                }
            }
        }

        private List<LaunchVehicle> GetDefaultVehicles()
        {
            return new List<LaunchVehicle>
            {
                new LaunchVehicle("lv_saturn_v", "Saturn V", 185_000_000, 140000, 12000,
                    new List<string>{"LEO","Lunar"}, 0.96, "Apollo-era heavy lift launch vehicle."),
                new LaunchVehicle("lv_falcon_heavy", "Falcon Heavy", 150_000_000, 63800, 10000,
                    new List<string>{"LEO","GTO","Lunar"}, 0.94, "Modern heavy lift vehicle."),
                new LaunchVehicle("lv_atlas_v", "Atlas V", 110_000_000, 18800, 6000,
                    new List<string>{"LEO","GTO"}, 0.97, "Medium-lift launch vehicle."),
                new LaunchVehicle("lv_delta_iv_heavy", "Delta IV Heavy", 350_000_000, 28790, 11000,
                    new List<string>{"LEO","GTO","Lunar"}, 0.95, "Heavy lift vehicle."),
            };
        }

        private List<OrbitDefinition> GetDefaultOrbits()
        {
            return new List<OrbitDefinition>
            {
                new OrbitDefinition("LEO", "Low Earth Orbit", 9400, 1.0, 0.1, "Orbit ~400 km above Earth."),
                new OrbitDefinition("GTO", "Geostationary Transfer Orbit", 10500, 1.2, 0.25, "Transfer to geostationary."),
                new OrbitDefinition("Lunar", "Lunar Orbit", 12000, 2.0, 2.5, "Orbit around the Moon."),
            };
        }

        private void SelectVehicle(LaunchVehicle vehicle)
        {
            selectedVehicle = vehicle;
            designer.SetLaunchVehicle(vehicle);
            RefreshSummary();
        }

        private void SelectOrbit(OrbitDefinition orbit)
        {
            selectedOrbit = orbit;
            designer.SetOrbit(orbit);
            RefreshSummary();
        }

        private void RefreshSummary()
        {
            if (manager == null) return;
            var design = manager.SpacecraftDesign;
            var text = $"Spacecraft: {design.spacecraftName}\n" +
                       $"Mass: {design.TotalMassKg:F0} kg | Power: {design.TotalPowerDrawW:F0} W | Cost: ${design.TotalCostUsd:F0}\n";
            if (selectedVehicle != null)
                text += $"Launch: {selectedVehicle.displayName} ({selectedVehicle.maxPayloadKg:F0} kg payload)\n";
            else
                text += "Launch: (none selected)\n";
            if (selectedOrbit != null)
                text += $"Orbit: {selectedOrbit.displayName} ({selectedOrbit.deltaVRequiredKmS:F0} km/s)";
            else
                text += "Orbit: (none selected)";
            if (summaryText != null) summaryText.text = text;

            if (simulateButton != null)
                simulateButton.interactable = selectedVehicle != null && selectedOrbit != null;
        }

        private void OnSimulateClicked()
        {
            if (manager == null) return;
            var engine = FindObjectOfType<SimulationEngine>();
            if (engine != null)
                engine.RunSimulation();
            if (launchPanel != null) launchPanel.SetActive(false);
            if (simulationPanel != null) simulationPanel.SetActive(true);
            manager.SetPhase(GamePhase.Simulation);
        }
    }
}
