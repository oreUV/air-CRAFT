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
    /// Instrument selection: add/remove scientific instruments for the mission.
    /// </summary>
    public class InstrumentSelectionUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform instrumentListContainer;
        [SerializeField] private GameObject instrumentButtonPrefab;
        [SerializeField] private TMP_Text infoText;
        [SerializeField] private Button nextButton;
        [SerializeField] private GameObject instrumentPanel;
        [SerializeField] private GameObject launchPanel;

        private MissionManager manager => MissionManager.Instance;
        private SpacecraftDesigner designer;

        private void Awake()
        {
            designer = FindObjectOfType<SpacecraftDesigner>();
            PopulateInstruments();
            Refresh();
            if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);
        }

        private void PopulateInstruments()
        {
            if (instrumentListContainer == null || instrumentButtonPrefab == null) return;

            var instruments = GetDefaultInstruments();
            foreach (var inst in instruments)
            {
                var btnGO = Instantiate(instrumentButtonPrefab, instrumentListContainer);
                var tmp = btnGO.GetComponentInChildren<TMP_Text>();
                var btn = btnGO.GetComponentInChildren<Button>();
                if (tmp != null)
                    tmp.text = $"{inst.displayName} ({inst.massKg:F0} kg, {inst.powerDrawW:F0} W)";
                if (btn != null)
                {
                    var captured = inst;
                    btn.onClick.AddListener(() => ToggleInstrument(captured));
                }
            }
        }

        private List<ScientificInstrument> GetDefaultInstruments()
        {
            return new List<ScientificInstrument>
            {
                new ScientificInstrument("inst_camera", "High-Res Camera", 80, 120, 5.0, 15,
                    new List<string>{"Moon","Mars","LEO"}, "Surface imaging and mapping."),
                new ScientificInstrument("inst_spectrometer", "Infrared Spectrometer", 60, 90, 2.0, 10,
                    new List<string>{"Moon","Mars","LEO"}, "Mineral composition analysis."),
                new ScientificInstrument("inst_seismometer", "Seismometer", 45, 60, 0.5, 8,
                    new List<string>{"Moon","Mars"}, "Seismic activity monitoring."),
                new ScientificInstrument("inst_solar_wind", "Solar Wind Experiment", 25, 40, 1.0, 6,
                    new List<string>{"Moon","LEO"}, "Solar wind particle measurement."),
                new ScientificInstrument("inst_laser_reflector", "Laser Retroreflector", 15, 20, 0.1, 4,
                    new List<string>{"Moon"}, "Earth-Moon distance ranging."),
            };
        }

        private void ToggleInstrument(ScientificInstrument instrument)
        {
            if (manager == null || designer == null) return;
            var design = manager.SpacecraftDesign;
            bool alreadyHas = design.instruments.Contains(instrument);
            if (alreadyHas)
                designer.RemoveInstrument(instrument);
            else
                designer.AddInstrument(instrument);
            Refresh();
        }

        private void Refresh()
        {
            if (manager == null) return;
            var design = manager.SpacecraftDesign;
            if (infoText != null)
                infoText.text = $"Instruments: {design.instruments.Count}\n" +
                                $"Science/day: {design.TotalSciencePerDay:F1}\n" +
                                $"Data/day: {design.TotalDataVolumeGbPerDay:F2} GB";
            if (nextButton != null)
                nextButton.interactable = design.instruments.Count > 0;
        }

        private void OnNextClicked()
        {
            if (manager == null) return;
            if (instrumentPanel != null) instrumentPanel.SetActive(false);
            if (launchPanel != null) launchPanel.SetActive(true);
            manager.SetPhase(GamePhase.LaunchSelection);
        }
    }
}
