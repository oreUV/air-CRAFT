using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceMissionDesign.Data
{
    /// <summary>
    /// Defines a mission objective: destination, science targets, and success criteria.
    /// </summary>
    [Serializable]
    public class MissionObjective
    {
        public string missionName;
        public string destination; // e.g. "Moon", "Mars", "LEO"
        public string description;
        public double targetScienceValue; // how much science is needed for a "success"
        public List<string> scienceTargets = new List<string>(); // e.g. "lunar_samples", "seismic_data"
        public double minMissionLifetimeDays;
        public double maxMissionLifetimeDays;

        public MissionObjective() { }

        public MissionObjective(string name, string dest, string desc, double targetScience,
            List<string> targets, double minDays, double maxDays)
        {
            missionName = name;
            destination = dest;
            description = desc;
            targetScienceValue = targetScience;
            scienceTargets = targets;
            minMissionLifetimeDays = minDays;
            maxMissionLifetimeDays = maxDays;
        }
    }

    /// <summary>
    /// A spacecraft component (hull, propulsion, power, comms, etc.) with mass, power, cost, and data impacts.
    /// </summary>
    [Serializable]
    public class SpacecraftComponent
    {
        public string componentId;
        public string displayName;
        public ComponentType type;
        public double massKg;
        public double powerDrawW;
        public double costUsd;
        public double dataRateKbps; // comms data rate contribution (0 for non-comms)
        public double reliabilityFactor; // 0-1, multiplier on mission success chance
        public string description;

        public SpacecraftComponent() { }

        public SpacecraftComponent(string id, string name, ComponentType t, double mass, double power,
            double cost, double dataRate, double reliability, string desc)
        {
            componentId = id;
            displayName = name;
            type = t;
            massKg = mass;
            powerDrawW = power;
            costUsd = cost;
            dataRateKbps = dataRate;
            reliabilityFactor = reliability;
            description = desc;
        }
    }

    public enum ComponentType { Hull, Propulsion, Power, Communications, Structure, Thermal }

    /// <summary>
    /// A scientific instrument the player can add to the spacecraft.
    /// </summary>
    [Serializable]
    public class ScientificInstrument
    {
        public string instrumentId;
        public string displayName;
        public double massKg;
        public double powerDrawW;
        public double dataVolumeGbPerDay;
        public double scienceValue; // science points per day when operating
        public List<string> compatibleDestinations = new List<string>();
        public bool isOperational = true;
        public string description;

        public ScientificInstrument() { }

        public ScientificInstrument(string id, string name, double mass, double power, double dataGb,
            double science, List<string> compat, string desc)
        {
            instrumentId = id;
            displayName = name;
            massKg = mass;
            powerDrawW = power;
            dataVolumeGbPerDay = dataGb;
            scienceValue = science;
            compatibleDestinations = compat;
            description = desc;
        }
    }

    /// <summary>
    /// A launch vehicle option with cost, payload capacity, and performance.
    /// </summary>
    [Serializable]
    public class LaunchVehicle
    {
        public string vehicleId;
        public string displayName;
        public double costUsd;
        public double maxPayloadKg;
        public double deltaVKmS; // total delta-v the vehicle can provide
        public List<string> compatibleOrbits = new List<string>();
        public double reliabilityFactor; // 0-1 launch success chance multiplier
        public string description;

        public LaunchVehicle() { }

        public LaunchVehicle(string id, string name, double cost, double payload, double deltaV,
            List<string> orbits, double reliability, string desc)
        {
            vehicleId = id;
            displayName = name;
            costUsd = cost;
            maxPayloadKg = payload;
            deltaVKmS = deltaV;
            compatibleOrbits = orbits;
            reliabilityFactor = reliability;
            description = desc;
        }
    }

    /// <summary>
    /// Orbit/destination definition with delta-v requirement and science multiplier.
    /// </summary>
    [Serializable]
    public class OrbitDefinition
    {
        public string orbitId;
        public string displayName;
        public double deltaVRequiredKmS;
        public double scienceMultiplier;
        public double commsDelaySeconds;
        public string description;

        public OrbitDefinition() { }

        public OrbitDefinition(string id, string name, double deltaV, double scienceMult,
            double commsDelay, string desc)
        {
            orbitId = id;
            displayName = name;
            deltaVRequiredKmS = deltaV;
            scienceMultiplier = scienceMult;
            commsDelaySeconds = commsDelay;
            description = desc;
        }
    }

    /// <summary>
    /// All mission constraints the player must respect.
    /// </summary>
    [Serializable]
    public class MissionConstraints
    {
        public double budgetUsd;
        public double maxMassKg;
        public double maxPowerW;
        public double minDataRateKbps;
        public double maxDataStorageGb;
        public double missionLifetimeDays;

        public MissionConstraints() { }

        public MissionConstraints(double budget, double maxMass, double maxPower, double minDataRate,
            double maxDataStorage, double lifetimeDays)
        {
            budgetUsd = budget;
            maxMassKg = maxMass;
            maxPowerW = maxPower;
            minDataRateKbps = minDataRate;
            maxDataStorageGb = maxDataStorage;
            missionLifetimeDays = lifetimeDays;
        }
    }

    /// <summary>
    /// The assembled spacecraft design chosen by the player(s).
    /// </summary>
    [Serializable]
    public class SpacecraftDesign
    {
        public List<SpacecraftComponent> components = new List<SpacecraftComponent>();
        public List<ScientificInstrument> instruments = new List<ScientificInstrument>();
        public LaunchVehicle launchVehicle;
        public OrbitDefinition orbit;
        public string spacecraftName;

        public double TotalMassKg
        {
            get
            {
                double m = 0;
                foreach (var c in components) m += c.massKg;
                foreach (var i in instruments) m += i.massKg;
                return m;
            }
        }

        public double TotalPowerDrawW
        {
            get
            {
                double p = 0;
                foreach (var c in components) p += c.powerDrawW;
                foreach (var i in instruments) if (i.isOperational) p += i.powerDrawW;
                return p;
            }
        }

        public double TotalCostUsd
        {
            get
            {
                double c = launchVehicle != null ? launchVehicle.costUsd : 0;
                foreach (var comp in components) c += comp.costUsd;
                return c;
            }
        }

        public double TotalDataRateKbps
        {
            get
            {
                double d = 0;
                foreach (var c in components) d += c.dataRateKbps;
                return d;
            }
        }

        public double TotalSciencePerDay
        {
            get
            {
                double s = 0;
                foreach (var i in instruments) if (i.isOperational) s += i.scienceValue;
                return s;
            }
        }

        public double TotalDataVolumeGbPerDay
        {
            get
            {
                double d = 0;
                foreach (var i in instruments) if (i.isOperational) d += i.dataVolumeGbPerDay;
                return d;
            }
        }
    }
}
