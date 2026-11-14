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
    /// Self-bootstrapping entry point. Creates a DontDestroyOnLoad GameObject holding
    /// MissionManager, SpacecraftDesigner, SimulationEngine, ScoreManager, MultiplayerManager,
    /// then loads/builds the main menu scene entirely from code.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        [Header("Scenes (build order)")]
        [SerializeField] private int mainMenuBuildIndex = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureManagers();
            BuildMainMenuUI();
        }

        private void EnsureManagers()
        {
            if (FindObjectOfType<MissionManager>() == null)
                new GameObject("Mission Manager").AddComponent<MissionManager>();
            if (FindObjectOfType<SpacecraftDesigner>() == null)
                new GameObject("Spacecraft Designer").AddComponent<SpacecraftDesigner>();
            if (FindObjectOfType<SimulationEngine>() == null)
                new GameObject("Simulation Engine").AddComponent<SimulationEngine>();
            if (FindObjectOfType<ScoreManager>() == null)
                new GameObject("Score Manager").AddComponent<ScoreManager>();
            if (FindObjectOfType<MultiplayerManager>() == null)
                new GameObject("Multiplayer Manager").AddComponent<MultiplayerManager>();
        }

        private void BuildMainMenuUI()
        {
            var canvasGO = new GameObject("Main Menu Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<GraphicRaycaster>();
            canvasGO.AddComponent<MainMenuUIBootstrap>();
        }
    }

    public sealed class MainMenuUIBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            var canvas = GetComponentInParent<Canvas>();
            var panel = MakePanel(canvas.transform, "MenuPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(400, 300));

            var title = MakeText(panel.transform, "Title", "Space Mission Design", 24, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.85f), new Vector2(380, 40));
            title.alignment = TextAlignmentOptions.Center;

            var countLabel = MakeText(panel.transform, "CountLabel", "Players (1-4):", 16, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.55f), new Vector2(380, 20));
            countLabel.alignment = TextAlignmentOptions.Center;

            var countInput = MakeInputField(panel.transform, "PlayerCountInput", "1", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.42f), new Vector2(120, 30));

            var startBtn = MakeButton(panel.transform, "StartButton", "Start Mission", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.25f), new Vector2(200, 40));
            startBtn.onClick.AddListener(() =>
            {
                int n = 1;
                if (countInput != null && int.TryParse(countInput.text, out int p)) n = Mathf.Clamp(p, 1, 4);
                var mm = MissionManager.Instance;
                if (mm != null)
                {
                    mm.SetPlayerCount(n);
                    LoadMissionSetup();
                }
            });
        }

        private static TextMeshProUGUI MakeText(Transform parent, string name, string text, int size, Vector2 pivot, Vector2 anchor, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.pivot = pivot;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = sizeDelta;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            return tmp;
        }

        private static Button MakeButton(Transform parent, string name, string label, Vector2 pivot, Vector2 anchor, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.pivot = pivot;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = sizeDelta;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.45f, 0.75f, 1f);
            var btn = go.AddComponent<Button>();
            var txtGO = new GameObject("Label");
            txtGO.transform.SetParent(go.transform, false);
            var trt = txtGO.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tt = txtGO.AddComponent<TextMeshProUGUI>();
            tt.text = label;
            tt.fontSize = 14;
            tt.alignment = TextAlignmentOptions.Center;
            return btn;
        }

        private static TMP_InputField MakeInputField(Transform parent, string name, string text, Vector2 pivot, Vector2 anchor, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.pivot = pivot;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = sizeDelta;
            var img = go.AddComponent<Image>();
            img.color = Color.white;
            var input = go.AddComponent<TMP_InputField>();
            input.text = text;
            return input;
        }

        private static GameObject MakePanel(Transform parent, string name, Vector2 pivot, Vector2 anchor, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.pivot = pivot;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = sizeDelta;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.05f, 0.08f, 0.12f, 0.95f);
            return go;
        }

        private void LoadMissionSetup()
        {
            // Build mission setup UI by replacing this canvas's children.
            foreach (Transform child in transform)
                Destroy(child.gameObject);
            var canvas = GetComponentInParent<Canvas>();

            var mm = MissionManager.Instance;
            var title = MakeText(transform, "Title", "Select Mission", 20, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.85f), new Vector2(400, 30));
            title.alignment = TextAlignmentOptions.Center;

            var scroll = MakeScrollView(transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(380, 200));
            var content = scroll.GetComponentInChildren<VerticalLayoutGroup>()?.transform;
            if (content == null) content = scroll.transform;

            var objectives = LoadObjectives();
            foreach (var obj in objectives)
            {
                var btn = MakeButton(transform, "Btn_" + obj.missionName, obj.missionName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(360, 30));
                btn.transform.SetParent(content, false);
                var captured = obj;
                btn.onClick.AddListener(() =>
                {
                    mm.StartNewMission(captured);
                    LoadDesignUI();
                });
            }

            var backBtn = MakeButton(transform, "BackButton", "Back", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.1f), new Vector2(120, 30));
            backBtn.onClick.AddListener(() => BuildMainMenuUI());
        }

        private List<MissionObjective> LoadObjectives()
        {
            var list = new List<MissionObjective>();
            var assets = Resources.LoadAll<TextAsset>("MissionData");
            foreach (var a in assets)
            {
                try
                {
                    var obj = JsonUtility.FromJson<MissionObjective>(a.text);
                    if (obj != null) list.Add(obj);
                }
                catch { }
            }
            if (list.Count == 0)
            {
                list.Add(new MissionObjective
                {
                    missionName = "Apollo 11",
                    destination = "Moon",
                    description = "First crewed lunar landing.",
                    targetScienceValue = 120,
                    scienceTargets = new List<string> { "lunar_samples", "seismic_data", "laser_ranging" },
                    minMissionLifetimeDays = 6,
                    maxMissionLifetimeDays = 10
                });
            }
            return list;
        }

        private static ScrollRect MakeScrollView(Transform parent, Vector2 pivot, Vector2 anchor, Vector2 sizeDelta)
        {
            var go = new GameObject("ScrollView");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.pivot = pivot;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = sizeDelta;
            var sr = go.AddComponent<ScrollRect>();
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(go.transform, false);
            var vrt = viewport.AddComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;
            viewport.AddComponent<Image>().color = new Color(0.1f, 0.13f, 0.18f, 1f);
            var mask = viewport.AddComponent<Mask>();
            sr.viewport = vrt;
            sr.content = vrt;
            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var crt = content.AddComponent<RectTransform>();
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = new Vector2(1, 0);
            crt.pivot = new Vector2(0.5f, 1);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(0, 50);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 8;
            vlg.childForceExpandWidth = true;
            vr_layout_rebuild(content);
            return sr;
        }

        private static void vr_layout_rebuild(Transform t) { }

        private void LoadDesignUI()
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);
            var mm = MissionManager.Instance;

            var title = MakeText(transform, "Title", "Spacecraft Design — " + mm.CurrentObjective.missionName, 18, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.9f), new Vector2(400, 30));
            title.alignment = TextAlignmentOptions.Center;

            var stats = MakeText(transform, "Stats", "", 14, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.82f), new Vector2(400, 40));
            stats.alignment = TextAlignmentOptions.Center;

            var scroll = MakeScrollView(transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(380, 220));
            var content = scroll.GetComponentInChildren<VerticalLayoutGroup>()?.transform ?? scroll.transform;

            var types = new[] { ComponentType.Hull, ComponentType.Propulsion, ComponentType.Power, ComponentType.Communications, ComponentType.Structure, ComponentType.Thermal };
            foreach (var t in types)
            {
                var btn = MakeButton(transform, "Add_" + t, "+ " + t.ToString(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(360, 28));
                btn.transform.SetParent(content, false);
                var captured = t;
                btn.onClick.AddListener(() =>
                {
                    var designer = FindObjectOfType<SpacecraftDesigner>();
                    designer?.AddComponentByType(captured);
                    RefreshStats(stats);
                });
            }

            var viol = MakeText(transform, "Violations", "", 13, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.45f), new Vector2(380, 60));
            viol.alignment = TextAlignmentOptions.Center;

            var nextBtn = MakeButton(transform, "NextButton", "Next: Instruments", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.15f), new Vector2(200, 40));
            nextBtn.onClick.AddListener(() => LoadInstrumentUI());

            RefreshStats(stats);
            RefreshViolations(viol, nextBtn);
        }

        private void RefreshStats(TMP_Text stats)
        {
            if (stats == null) return;
            var mm = MissionManager.Instance;
            if (mm == null) return;
            var d = mm.SpacecraftDesign;
            stats.text = $"Mass: {d.TotalMassKg:F0} kg / {mm.Constraints.maxMassKg:F0} kg   " +
                         $"Power: {d.TotalPowerDrawW:F0} W / {mm.Constraints.maxPowerW:F0} W   " +
                         $"Cost: ${d.TotalCostUsd:F0} / ${mm.Constraints.budgetUsd:F0}";
        }

        private void RefreshViolations(TMP_Text viol, Button nextBtn)
        {
            var designer = FindObjectOfType<SpacecraftDesigner>();
            var v = designer == null ? new List<string>() : designer.GetConstraintViolations();
            viol.text = v.Count > 0 ? "Violations:\n• " + string.Join("\n• ", v) : "All constraints satisfied.";
            viol.color = v.Count > 0 ? Color.red : Color.green;
            if (nextBtn != null) nextBtn.interactable = v.Count == 0;
        }

        private void LoadInstrumentUI()
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);
            var mm = MissionManager.Instance;

            var title = MakeText(transform, "Title", "Instruments — " + mm.CurrentObjective.destination, 18, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.9f), new Vector2(400, 30));
            title.alignment = TextAlignmentOptions.Center;

            var info = MakeText(transform, "Info", "", 14, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.83f), new Vector2(400, 40));
            info.alignment = TextAlignmentOptions.Center;

            var scroll = MakeScrollView(transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(380, 220));
            var content = scroll.GetComponentInChildren<VerticalLayoutGroup>()?.transform ?? scroll.transform;

            var instruments = GetDefaultInstruments(mm.CurrentObjective.destination);
            foreach (var inst in instruments)
            {
                var row = new GameObject("Row_" + inst.instrumentId);
                row.transform.SetParent(content, false);
                var rrt = row.AddComponent<RectTransform>();
                rrt.anchorMin = Vector2.zero;
                rrt.anchorMax = new Vector2(1, 0);
                rrt.pivot = new Vector2(0.5f, 1);
                rrt.sizeDelta = new Vector2(0, 30);
                var hl = row.AddComponent<HorizontalLayoutGroup>();
                hl.childForceExpandHeight = true;
                hl.spacing = 8;
                var label = MakeText(row.transform, "Label", inst.displayName + " (" + inst.massKg + " kg, " + inst.powerDrawW + " W)", 13, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(200, 20));
                var btn = MakeButton(row.transform, "Toggle", "+", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40, 22));
                btn.transform.SetParent(row.transform, false);
                var captured = inst;
                btn.onClick.AddListener(() =>
                {
                    var designer = FindObjectOfType<SpacecraftDesigner>();
                    var design = mm.SpacecraftDesign;
                    bool has = design.instruments.Contains(captured);
                    if (has) designer.RemoveInstrument(captured);
                    else designer.AddInstrument(captured);
                    RefreshInstrumentInfo(info);
                    // rebuild toggle text
                    var btntxt = btn.GetComponentInChildren<TextMeshProUGUI>();
                    if (btntxt != null) btntxt.text = has ? "+" : "–";
                });
            }

            var nextBtn = MakeButton(transform, "NextButton", "Next: Launch", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.1f), new Vector2(200, 40));
            nextBtn.onClick.AddListener(() => LoadLaunchUI());

            RefreshInstrumentInfo(info);
        }

        private List<ScientificInstrument> GetDefaultInstruments(string destination)
        {
            var all = new List<ScientificInstrument>
            {
                new ScientificInstrument("inst_camera", "High-Res Camera", 80, 120, 5.0, 15, new List<string>{"Moon","Mars","LEO"}, "Surface imaging."),
                new ScientificInstrument("inst_spectrometer", "Infrared Spectrometer", 60, 90, 2.0, 10, new List<string>{"Moon","Mars","LEO"}, "Mineral analysis."),
                new ScientificInstrument("inst_seismometer", "Seismometer", 45, 60, 0.5, 8, new List<string>{"Moon","Mars"}, "Seismic monitoring."),
                new ScientificInstrument("inst_solar_wind", "Solar Wind Experiment", 25, 40, 1.0, 6, new List<string>{"Moon","LEO"}, "Solar wind measurement."),
                new ScientificInstrument("inst_laser_reflector", "Laser Retroreflector", 15, 20, 0.1, 4, new List<string>{"Moon"}, "Earth-Moon ranging."),
            };
            var result = new List<ScientificInstrument>();
            foreach (var i in all)
                if (i.compatibleDestinations.Contains(destination)) result.Add(i);
            return result;
        }

        private void RefreshInstrumentInfo(TMP_Text info)
        {
            if (info == null) return;
            var mm = MissionManager.Instance;
            var d = mm.SpacecraftDesign;
            info.text = $"Instruments: {d.instruments.Count}   Science/day: {d.TotalSciencePerDay:F1}   Data/day: {d.TotalDataVolumeGbPerDay:F2} GB";
        }

        private void LoadLaunchUI()
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);
            var mm = MissionManager.Instance;

            var title = MakeText(transform, "Title", "Launch Vehicle & Orbit", 18, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.9f), new Vector2(400, 30));
            title.alignment = TextAlignmentOptions.Center;

            var scroll = MakeScrollView(transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(380, 300));
            var content = scroll.GetComponentInChildren<VerticalLayoutGroup>()?.transform ?? scroll.transform;

            var vehicles = GetDefaultVehicles();
            var selectedVehicle = new LaunchVehicle();
            var orbits = GetDefaultOrbits();
            var selectedOrbit = new OrbitDefinition();

            var sum = MakeText(transform, "Summary", "", 13, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.28f), new Vector2(380, 60));
            sum.alignment = TextAlignmentOptions.Center;

            void RefreshSummary()
            {
                var d = mm.SpacecraftDesign;
                var vtxt = selectedVehicle.vehicleId == null ? "(none)" : selectedVehicle.displayName;
                var otxt = selectedOrbit.orbitId == null ? "(none)" : selectedOrbit.displayName;
                sum.text = $"Launch: {vtxt}\nOrbit: {otxt}\nMass: {d.TotalMassKg:F0} kg (payload cap: {(selectedVehicle.maxPayloadKg>0?selectedVehicle.maxPayloadKg:0):F0} kg)";
            }

            var vLabel = MakeText(transform, "VLabel", "Launch Vehicle", 14, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.83f), new Vector2(380, 20));
            vLabel.alignment = TextAlignmentOptions.Center;
            vLabel.transform.SetParent(content, false);

            foreach (var v in vehicles)
            {
                var btn = MakeButton(transform, "V_" + v.vehicleId, v.displayName + " — $" + v.costUsd.ToString("F0") + " — " + v.maxPayloadKg.ToString("F0") + " kg", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(350, 26));
                btn.transform.SetParent(content, false);
                var captured = v;
                btn.onClick.AddListener(() =>
                {
                    selectedVehicle = captured;
                    mm.SpacecraftDesign.launchVehicle = captured;
                    RefreshSummary();
                });
            }

            var oLabel = MakeText(transform, "OLabel", "Orbit", 14, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.55f), new Vector2(380, 20));
            oLabel.alignment = TextAlignmentOptions.Center;
            oLabel.transform.SetParent(content, false);

            foreach (var o in orbits)
            {
                var btn = MakeButton(transform, "O_" + o.orbitId, o.displayName + " — " + o.deltaVRequiredKmS.ToString("F0") + " km/s", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(350, 26));
                btn.transform.SetParent(content, false);
                var captured = o;
                btn.onClick.AddListener(() =>
                {
                    selectedOrbit = captured;
                    mm.SpacecraftDesign.orbit = captured;
                    RefreshSummary();
                });
            }

            var nextBtn = MakeButton(transform, "NextButton", "Simulate Mission", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.1f), new Vector2(200, 40));
            nextBtn.onClick.AddListener(() =>
            {
                var designer = FindObjectOfType<SpacecraftDesigner>();
                var v = designer.GetConstraintViolations();
                if (v.Count > 0)
                {
                    sum.text = "Fix violations first:\n• " + string.Join("\n• ", v);
                    sum.color = Color.red;
                    return;
                }
                var engine = FindObjectOfType<SimulationEngine>();
                engine?.RunSimulation();
                LoadResultsUI();
            });
        }

        private List<LaunchVehicle> GetDefaultVehicles()
        {
            return new List<LaunchVehicle>
            {
                new LaunchVehicle("lv_saturn_v", "Saturn V", 185000000, 140000, 12000, new List<string>{"LEO","Lunar"}, 0.96, ""),
                new LaunchVehicle("lv_falcon_heavy", "Falcon Heavy", 150000000, 63800, 10000, new List<string>{"LEO","GTO","Lunar"}, 0.94, ""),
                new LaunchVehicle("lv_atlas_v", "Atlas V", 110000000, 18800, 6000, new List<string>{"LEO","GTO"}, 0.97, ""),
                new LaunchVehicle("lv_delta_iv", "Delta IV Heavy", 350000000, 28790, 11000, new List<string>{"LEO","GTO","Lunar"}, 0.95, ""),
            };
        }

        private List<OrbitDefinition> GetDefaultOrbits()
        {
            return new List<OrbitDefinition>
            {
                new OrbitDefinition("LEO", "Low Earth Orbit", 9400, 1.0, 0.1, ""),
                new OrbitDefinition("GTO", "Geostationary Transfer", 10500, 1.2, 0.25, ""),
                new OrbitDefinition("Lunar", "Lunar Orbit", 12000, 2.0, 2.5, ""),
            };
        }

        private void LoadResultsUI()
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);
            var mm = MissionManager.Instance;
            var result = mm.SimulationResult;

            var title = MakeText(transform, "Title", "Mission Results", 20, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.88f), new Vector2(400, 30));
            title.alignment = TextAlignmentOptions.Center;

            var status = MakeText(transform, "Status", result.missionSuccess ? "MISSION SUCCESS" : "MISSION FAILED", 20, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.75f), new Vector2(400, 30));
            status.alignment = TextAlignmentOptions.Center;
            status.color = result.missionSuccess ? Color.green : Color.red;

            var score = MakeText(transform, "Score", "Score: " + result.score.ToString("F1") + " / 100", 16, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.65f), new Vector2(400, 25));
            score.alignment = TextAlignmentOptions.Center;

            var feedback = MakeText(transform, "Feedback", string.Join("\n", result.feedbackMessages), 12, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.4f), new Vector2(380, 100));
            feedback.alignment = TextAlignmentOptions.Center;

            var restart = MakeButton(transform, "Restart", "Design New Mission", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.1f), new Vector2(220, 40));
            restart.onClick.AddListener(() =>
            {
                mm.ResetMission();
                BuildMainMenuUI();
            });
        }
    }
}
