using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SpaceMissionDesign.Data;

namespace SpaceMissionDesign.Game
{
    /// <summary>
    /// Handles spacecraft assembly: adding/removing components and instruments, checking constraints.
    /// </summary>
    public sealed class SpacecraftDesigner : MonoBehaviour
    {
        private MissionManager manager => MissionManager.Instance;

        public event Action OnDesignChanged;

        public void AddComponent(SpacecraftComponent component)
        {
            if (manager == null || component == null) return;

            var design = manager.SpacecraftDesign;
            if (!manager.MassOK(component.massKg))
            {
                Debug.LogWarning($"Cannot add {component.displayName}: would exceed mass limit.");
                return;
            }
            if (!manager.PowerOK(component.powerDrawW))
            {
                Debug.LogWarning($"Cannot add {component.displayName}: would exceed power limit.");
                return;
            }
            if (!manager.CanAfford(component.costUsd))
            {
                Debug.LogWarning($"Cannot add {component.displayName}: would exceed budget.");
                return;
            }

            design.components.Add(component);
            OnDesignChanged?.Invoke();
        }

        public void RemoveComponent(SpacecraftComponent component)
        {
            if (manager == null) return;
            manager.SpacecraftDesign.components.Remove(component);
            OnDesignChanged?.Invoke();
        }

        public void AddInstrument(ScientificInstrument instrument)
        {
            if (manager == null || instrument == null) return;

            var design = manager.SpacecraftDesign;
            // Check destination compatibility
            if (!instrument.compatibleDestinations.Contains(manager.CurrentObjective.destination))
            {
                Debug.LogWarning($"{instrument.displayName} is not compatible with {manager.CurrentObjective.destination}.");
                return;
            }
            if (!manager.MassOK(instrument.massKg)) return;
            if (!manager.PowerOK(instrument.powerDrawW)) return;
            if (!manager.CanAfford(0)) return; // instruments are free to add but count against mass/power

            design.instruments.Add(instrument);
            OnDesignChanged?.Invoke();
        }

        public void RemoveInstrument(ScientificInstrument instrument)
        {
            if (manager == null) return;
            manager.SpacecraftDesign.instruments.Remove(instrument);
            OnDesignChanged?.Invoke();
        }

        public void SetLaunchVehicle(LaunchVehicle vehicle)
        {
            if (manager == null || vehicle == null) return;
            var design = manager.SpacecraftDesign;
            design.launchVehicle = vehicle;
            OnDesignChanged?.Invoke();
        }

        public void SetOrbit(OrbitDefinition orbit)
        {
            if (manager == null || orbit == null) return;
            var design = manager.SpacecraftDesign;
            design.orbit = orbit;
            OnDesignChanged?.Invoke();
        }

        /// <summary>
        /// Returns a list of constraint violations for the current design.
        /// </summary>
        public List<string> GetConstraintViolations()
        {
            var violations = new List<string>();
            if (manager == null) return violations;

            var design = manager.SpacecraftDesign;
            var c = manager.Constraints;

            if (design.TotalMassKg > c.maxMassKg)
                violations.Add($"Mass over limit: {design.TotalMassKg:F0} kg > {c.maxMassKg:F0} kg");
            if (design.TotalPowerDrawW > c.maxPowerW)
                violations.Add($"Power over limit: {design.TotalPowerDrawW:F0} W > {c.maxPowerW:F0} W");
            if (design.TotalCostUsd > c.budgetUsd)
                violations.Add($"Cost over budget: ${design.TotalCostUsd:F0} > ${c.budgetUsd:F0}");
            if (design.TotalDataRateKbps < c.minDataRateKbps)
                violations.Add($"Data rate too low: {design.TotalDataRateKbps:F1} kbps < {c.minDataRateKbps:F1} kbps");
            if (design.launchVehicle == null)
                violations.Add("No launch vehicle selected.");
            if (design.orbit == null)
                violations.Add("No orbit selected.");
            if (design.launchVehicle != null && design.orbit != null &&
                !design.launchVehicle.compatibleOrbits.Contains(design.orbit.orbitId))
                violations.Add("Launch vehicle is not compatible with selected orbit.");
            if (design.launchVehicle != null && design.TotalMassKg > design.launchVehicle.maxPayloadKg)
                violations.Add("Spacecraft mass exceeds launch vehicle payload capacity.");

            return violations;
        }

        public bool IsDesignValid()
        {
            return GetConstraintViolations().Count == 0;
        }
    }
}
