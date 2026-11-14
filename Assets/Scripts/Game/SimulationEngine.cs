using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SpaceMissionDesign.Data;

namespace SpaceMissionDesign.Game
{
    /// <summary>
    /// Runs the mission simulation using the assembled spacecraft design and mission constraints.
    /// Produces a SimulationResult with score and feedback.
    /// </summary>
    public sealed class SimulationEngine : MonoBehaviour
    {
        private MissionManager manager => MissionManager.Instance;

        public SimulationResult RunSimulation()
        {
            if (manager == null || manager.SpacecraftDesign == null || manager.CurrentObjective == null)
                return null;

            var design = manager.SpacecraftDesign;
            var objective = manager.CurrentObjective;
            var constraints = manager.Constraints;

            var result = new SimulationResult();
            var feedback = new List<string>();
            var margins = new Dictionary<string, double>();

            double score = 0;

            // --- Science return ---
            double baseSciencePerDay = design.TotalSciencePerDay;
            double orbitMult = design.orbit != null ? design.orbit.scienceMultiplier : 1.0;
            double totalScience = baseSciencePerDay * constraints.missionLifetimeDays * orbitMult;
            result.totalScienceValue = totalScience;

            double scienceRatio = objective.targetScienceValue > 0
                ? Mathf.Clamp01((float)(totalScience / objective.targetScienceValue))
                : 0;
            double scienceScore = scienceRatio * 40; // up to 40 points
            score += scienceScore;

            feedback.Add($"Science return: {totalScience:F1} points (target {objective.targetScienceValue:F1}) — {scienceRatio:P0} of target.");
            margins["science"] = totalScience - objective.targetScienceValue;

            // --- Cost efficiency ---
            // Scored against a fixed reference rather than the chosen budget, otherwise
            // dragging the budget slider up would inflate the score for free.
            const double costReferenceUsd = 900_000_000;
            double costRatio = design.TotalCostUsd / constraints.budgetUsd;
            double costEfficiency = Mathf.Clamp01((float)(1.0 - design.TotalCostUsd / costReferenceUsd));
            double costScore = costEfficiency * 20; // up to 20 points
            score += costScore;

            feedback.Add($"Cost: ${design.TotalCostUsd:F0} of ${constraints.budgetUsd:F0} budget ({(costRatio * 100):F0}% used).");
            margins["budget"] = constraints.budgetUsd - design.TotalCostUsd;

            // --- Mass margin ---
            double massRatio = design.TotalMassKg / constraints.maxMassKg;
            double massScore = (1.0 - massRatio) * 15; // up to 15 points
            massScore = Mathf.Clamp((float)massScore, 0, 15);
            score += massScore;

            feedback.Add($"Mass: {design.TotalMassKg:F0} kg of {constraints.maxMassKg:F0} kg limit ({(massRatio * 100):F0}% used).");
            margins["mass"] = constraints.maxMassKg - design.TotalMassKg;

            // --- Power margin ---
            double powerRatio = design.TotalPowerDrawW / constraints.maxPowerW;
            double powerScore = (1.0 - powerRatio) * 10; // up to 10 points
            powerScore = Mathf.Clamp((float)powerScore, 0, 10);
            score += powerScore;

            feedback.Add($"Power: {design.TotalPowerDrawW:F0} W of {constraints.maxPowerW:F0} W limit ({(powerRatio * 100):F0}% used).");
            margins["power"] = constraints.maxPowerW - design.TotalPowerDrawW;

            // --- Data rate ---
            double dataRatio = design.TotalDataRateKbps / Mathf.Max((float)constraints.minDataRateKbps, 1f);
            double dataScore = Mathf.Clamp01((float)(dataRatio / 2.0f)) * 5; // up to 5 points
            score += dataScore;

            feedback.Add($"Data rate: {design.TotalDataRateKbps:F1} kbps (min {constraints.minDataRateKbps:F1} kbps required).");
            margins["dataRate"] = design.TotalDataRateKbps - constraints.minDataRateKbps;

            // --- Data volume vs storage ---
            double totalDataVolume = design.TotalDataVolumeGbPerDay * constraints.missionLifetimeDays;
            bool dataFits = totalDataVolume <= constraints.maxDataStorageGb;
            double dataStorageScore = dataFits ? 5 : 0;
            score += dataStorageScore;

            feedback.Add($"Data volume: {totalDataVolume:F1} GB over mission vs {constraints.maxDataStorageGb:F1} GB storage — {(dataFits ? "OK" : "EXCEEDS STORAGE")}.");
            margins["dataStorage"] = constraints.maxDataStorageGb - totalDataVolume;

            // --- Launch & orbit reliability ---
            double launchReliability = design.launchVehicle != null ? design.launchVehicle.reliabilityFactor : 0.5;
            double reliabilityScore = launchReliability * 5; // up to 5 points
            score += reliabilityScore;

            feedback.Add($"Launch reliability: {(launchReliability * 100):F0}%.");
            margins["reliability"] = launchReliability;

            // --- Mission success determination ---
            bool allValid = true;
            if (design.TotalMassKg > constraints.maxMassKg) allValid = false;
            if (design.TotalPowerDrawW > constraints.maxPowerW) allValid = false;
            if (design.TotalCostUsd > constraints.budgetUsd) allValid = false;
            if (design.TotalDataRateKbps < constraints.minDataRateKbps) allValid = false;
            if (!dataFits) allValid = false;
            if (totalScience < objective.targetScienceValue * 0.5) allValid = false; // at least 50% science
            if (design.launchVehicle == null || design.orbit == null) allValid = false;

            result.missionSuccess = allValid;
            result.totalCostUsd = design.TotalCostUsd;
            result.totalMassKg = design.TotalMassKg;
            result.missionLifetimeDays = constraints.missionLifetimeDays;
            result.score = score;
            result.feedbackMessages = feedback;
            result.constraintMargins = margins;

            manager.CompleteSimulation(result);
            return result;
        }
    }
}
