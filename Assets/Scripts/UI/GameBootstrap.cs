using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SpaceMissionDesign.Data;
using SpaceMissionDesign.Game;

namespace SpaceMissionDesign.UI
{
    /// <summary>
    /// Single self-contained entry point. Uses only legacy uGUI (no TMP dependency).
    /// Auto-spawns itself in any scene via RuntimeInitializeOnLoadMethod, so pressing Play
    /// works even in an empty default scene.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private static GameBootstrap instance;
        private static Font uiFont;

        private Canvas canvas;
        private Text statsText;
        private Text budgetValueText;
        private Text violationsText;
        private Button nextButton;
        private MissionManager manager => MissionManager.Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (UnityEngine.Object.FindFirstObjectByType<GameBootstrap>() != null) return;
            var go = new GameObject("Game Bootstrap");
            go.AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            uiFont = GetFont();
            EnsureManagers();
            EnsureEventSystem();
            new GameObject("3D Mission View").AddComponent<Mission3DView>().Initialize();
            BuildCanvas();
            ShowTitle();
        }

        // ---------------- infrastructure ----------------

        private static Font GetFont()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            if (f == null) f = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI", "Helvetica", "Liberation Sans" }, 16);
            return f;
        }

        private void EnsureManagers()
        {
            if (FindFirstObjectByType<MissionManager>() == null) new GameObject("Mission Manager").AddComponent<MissionManager>();
            if (FindFirstObjectByType<SpacecraftDesigner>() == null) new GameObject("Spacecraft Designer").AddComponent<SpacecraftDesigner>();
            if (FindFirstObjectByType<SimulationEngine>() == null) new GameObject("Simulation Engine").AddComponent<SimulationEngine>();
            if (FindFirstObjectByType<ScoreManager>() == null) new GameObject("Score Manager").AddComponent<ScoreManager>();
            if (FindFirstObjectByType<MultiplayerManager>() == null) new GameObject("Multiplayer Manager").AddComponent<MultiplayerManager>();
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("Event System");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(go);
        }

        private void EnsureCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.02f, .03f, .06f);
            go.AddComponent<AudioListener>();
            DontDestroyOnLoad(go);
        }

        private void BuildCanvas()
        {
            var go = new GameObject("Game UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            DontDestroyOnLoad(go);
        }

        private void Clear()
        {
            var raycaster = canvas != null ? canvas.GetComponent<GraphicRaycaster>() : null;
            if (raycaster != null) raycaster.enabled = true;
            for (int i = canvas.transform.childCount - 1; i >= 0; i--)
                Destroy(canvas.transform.GetChild(i).gameObject);
        }

        // ---------------- UI primitives ----------------

        private static RectTransform NewRect(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static Text NewText(Transform parent, string text, int size, Vector2 pos, Vector2 box,
            TextAnchor anchor = TextAnchor.MiddleCenter, Color? color = null, bool bold = false)
        {
            var rt = NewRect(parent, "Text", pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = uiFont;
            t.fontSize = size;
            t.text = text;
            t.alignment = anchor;
            t.color = color ?? new Color(.92f, .95f, 1f);
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static Button NewButton(Transform parent, string label, Vector2 pos, Vector2 size, Action onClick,
            Color? tint = null, int fontSize = 20)
        {
            var rt = NewRect(parent, "Button", pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = tint ?? new Color(.16f, .34f, .58f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            NewText(rt, label, fontSize, Vector2.zero, size, TextAnchor.MiddleCenter, new Color(.95f, .97f, 1f));
            return btn;
        }

        private static InputField NewInput(Transform parent, string value, Vector2 pos, Vector2 size)
        {
            var rt = NewRect(parent, "Input", pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, .96f);
            var input = rt.gameObject.AddComponent<InputField>();
            var text = NewText(rt, "", 24, Vector2.zero, size - new Vector2(16, 8), TextAnchor.MiddleCenter, Color.black);
            text.supportRichText = false;
            input.textComponent = text;
            input.targetGraphic = img;
            input.text = value;
            return input;
        }

        private static Slider NewSlider(Transform parent, Vector2 pos, Vector2 size, float min, float max,
            float value, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var rt = NewRect(parent, "Slider", pos, size);
            var slider = rt.gameObject.AddComponent<Slider>();

            // Track.
            var track = NewRect(rt, "Background", Vector2.zero, Vector2.zero);
            track.anchorMin = new Vector2(0f, .5f);
            track.anchorMax = new Vector2(1f, .5f);
            track.offsetMin = new Vector2(0f, -5f);
            track.offsetMax = new Vector2(0f, 5f);
            var trackImg = track.gameObject.AddComponent<Image>();
            trackImg.color = new Color(1f, 1f, 1f, .14f);

            // Filled portion.
            var fillArea = NewRect(rt, "Fill Area", Vector2.zero, Vector2.zero);
            fillArea.anchorMin = new Vector2(0f, .5f);
            fillArea.anchorMax = new Vector2(1f, .5f);
            fillArea.offsetMin = new Vector2(12f, -5f);
            fillArea.offsetMax = new Vector2(-12f, 5f);

            var fill = NewRect(fillArea, "Fill", Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = new Color(.24f, .60f, .95f);

            // Handle.
            var handleArea = NewRect(rt, "Handle Slide Area", Vector2.zero, Vector2.zero);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(12f, 0f);
            handleArea.offsetMax = new Vector2(-12f, 0f);

            var handle = NewRect(handleArea, "Handle", Vector2.zero, new Vector2(24f, 24f));
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = new Color(.93f, .96f, 1f);

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            slider.value = Mathf.Clamp(value, min, max);
            if (onChanged != null) slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        private static string Money(double usd)
        {
            return usd >= 1_000_000_000
                ? $"${usd / 1_000_000_000:F2}B"
                : $"${usd / 1_000_000:N0}M";
        }

        private static void Background(Transform parent)
        {
            // Light full-screen tint: keeps the 3D scene visible.
            var tint = NewRect(parent, "Background Tint", Vector2.zero, Vector2.zero);
            tint.anchorMin = Vector2.zero;
            tint.anchorMax = Vector2.one;
            tint.offsetMin = Vector2.zero;
            tint.offsetMax = Vector2.zero;
            var tintImg = tint.gameObject.AddComponent<Image>();
            tintImg.color = new Color(.02f, .03f, .06f, .16f);
            tintImg.raycastTarget = false;

            // Darker panel behind the left-half UI so text stays readable over the 3D view.
            var panel = NewRect(parent, "Background Panel", Vector2.zero, new Vector2(960f, 1080f));
            panel.anchorMin = panel.anchorMax = new Vector2(0f, .5f);
            panel.pivot = new Vector2(0f, .5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(960f, 1080f);
            var panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.color = new Color(.02f, .035f, .07f, .55f);
            panelImg.raycastTarget = false;

            // Camera control hint on the 3D side of the screen.
            NewText(parent, "Left-drag to move  ·  Right-drag to orbit  ·  Scroll to zoom  ·  Double-click to recentre", 15,
                new Vector2(480f, -470f), new Vector2(960f, 30f), TextAnchor.MiddleCenter,
                new Color(.55f, .68f, .86f));
        }

        private void BeginScreen(string title, string subtitle = null)
        {
            Clear();
            Background(canvas.transform);
            NewText(canvas.transform, title, 40, new Vector2(-500, 400), new Vector2(880, 60), TextAnchor.MiddleCenter, null, true);
            if (!string.IsNullOrEmpty(subtitle))
                NewText(canvas.transform, subtitle, 19, new Vector2(-500, 352), new Vector2(880, 36), TextAnchor.MiddleCenter, new Color(.62f, .72f, .85f));
        }

        // ---------------- screens ----------------

        private void ShowTitle()
        {
            Mission3DView.Instance?.Refresh(null);
            BeginScreen("SPACE MISSION DESIGN", "Apollo 11 — Interactive Mission Design Game");
            NewText(canvas.transform, "Design a spacecraft, choose instruments, pick a launch vehicle and orbit,\nand see how your engineering trade-offs affect mission success.", 17,
                new Vector2(-500, 270), new Vector2(880, 70), TextAnchor.MiddleCenter, new Color(.70f, .78f, .90f));

            NewText(canvas.transform, "Players (1–4)", 21, new Vector2(-500, 130), new Vector2(300, 30), TextAnchor.MiddleCenter, null, true);
            var input = NewInput(canvas.transform, "1", new Vector2(-500, 80), new Vector2(140, 48));

            NewButton(canvas.transform, "BEGIN MISSION", new Vector2(-500, -20), new Vector2(340, 60), () =>
            {
                int n = 1;
                if (input != null && int.TryParse(input.text, out int parsed)) n = Mathf.Clamp(parsed, 1, 4);
                if (manager != null) manager.SetPlayerCount(n);
                ShowMissionSelect();
            });

            NewText(canvas.transform, "Budget • Mass • Power • Data • Lifetime", 15, new Vector2(-500, -160), new Vector2(880, 30), TextAnchor.MiddleCenter, new Color(.45f, .58f, .75f));
        }

        private void ShowMissionSelect()
        {
            BeginScreen("SELECT MISSION", "Each mission sets a science target and lifetime window");
            var missions = LoadMissions();
            for (int i = 0; i < missions.Count; i++)
            {
                var m = missions[i];
                NewButton(canvas.transform, $"{m.missionName}  ·  {m.destination}  ·  target science {m.targetScienceValue:F0}",
                    new Vector2(-500, 240 - i * 80), new Vector2(880, 58), () =>
                    {
                        if (manager != null) manager.StartNewMission(m);
                        ShowDesign();
                    });
            }
            NewButton(canvas.transform, "Back", new Vector2(-500, -330), new Vector2(220, 50), ShowTitle, new Color(.25f, .28f, .34f));
        }

        private List<MissionObjective> LoadMissions()
        {
            var list = new List<MissionObjective>();
            foreach (var asset in Resources.LoadAll<TextAsset>("MissionData"))
            {
                try
                {
                    var obj = JsonUtility.FromJson<MissionObjective>(asset.text);
                    if (obj != null && !string.IsNullOrEmpty(obj.missionName)) list.Add(obj);
                }
                catch (Exception e) { Debug.LogWarning("Mission parse failed: " + e.Message); }
            }
            if (list.Count == 0)
                list.Add(new MissionObjective("Apollo 11", "Moon", "First crewed lunar landing.", 120,
                    new List<string> { "lunar_samples", "seismic_data", "laser_ranging" }, 6, 10));
            return list;
        }

        private void ShowDesign()
        {
            if (manager == null || manager.CurrentObjective == null) { ShowTitle(); return; }
            RefreshDesignStats();
        }

        private void RefreshDesignStats()
        {
            Mission3DView.Instance?.Refresh(manager.SpacecraftDesign);
            Clear();
            Background(canvas.transform);
            NewText(canvas.transform, "SPACECRAFT DESIGN", 38, new Vector2(-500, 412), new Vector2(860, 56), TextAnchor.MiddleCenter, null, true);
            NewText(canvas.transform, manager.CurrentObjective.missionName + " — add subsystems, then continue", 19,
                new Vector2(-500, 366), new Vector2(880, 34), TextAnchor.MiddleCenter, new Color(.62f, .72f, .85f));

            var d = manager.SpacecraftDesign;
            var c = manager.Constraints;

            statsText = NewText(canvas.transform, "", 19, new Vector2(-500, 306), new Vector2(880, 84), TextAnchor.MiddleCenter,
                new Color(.85f, .90f, .98f));

            // Budget slider ($250M – $2B).
            NewText(canvas.transform, "MISSION BUDGET", 16, new Vector2(-780, 220), new Vector2(200, 28), TextAnchor.MiddleLeft,
                new Color(.55f, .72f, .95f), true);
            budgetValueText = NewText(canvas.transform, Money(c.budgetUsd), 17, new Vector2(-60, 220), new Vector2(110, 30),
                TextAnchor.MiddleRight, new Color(.90f, .94f, 1f), true);
            NewSlider(canvas.transform, new Vector2(-370, 220), new Vector2(500, 26),
                (float)(MissionManager.MinBudgetUsd / 1_000_000.0),
                (float)(MissionManager.MaxBudgetUsd / 1_000_000.0),
                (float)(c.budgetUsd / 1_000_000.0),
                millions =>
                {
                    if (manager == null) return;
                    manager.SetBudget(millions * 1_000_000.0);
                    UpdateDesignReadouts();
                });

            var types = new[] { ComponentType.Hull, ComponentType.Propulsion, ComponentType.Power,
                                ComponentType.Communications, ComponentType.Structure, ComponentType.Thermal };
            for (int i = 0; i < types.Length; i++)
            {
                int col = i % 2, row = i / 2;
                float x = col == 0 ? -680f : -360f;
                float y = 146f - row * 72f;
                var type = types[i];
                NewButton(canvas.transform, "+  " + type, new Vector2(x, y), new Vector2(300, 58), () => AddDefaultComponent(type), null, 18);
            }

            violationsText = NewText(canvas.transform, "", 16, new Vector2(-500, -95), new Vector2(900, 110), TextAnchor.MiddleCenter,
                Color.white);

            NewButton(canvas.transform, "Back", new Vector2(-680, -330), new Vector2(220, 50), ShowMissionSelect, new Color(.25f, .28f, .34f));
            nextButton = NewButton(canvas.transform, "Next: Instruments", new Vector2(-360, -330), new Vector2(280, 50), ShowInstruments);

            UpdateDesignReadouts();
        }

        /// <summary>Refreshes only the text/validation of the design screen (used while dragging the budget slider).</summary>
        private void UpdateDesignReadouts()
        {
            if (manager == null || manager.SpacecraftDesign == null) return;
            var d = manager.SpacecraftDesign;
            var c = manager.Constraints;

            if (statsText != null)
                statsText.text = $"Mass {d.TotalMassKg:N0} / {c.maxMassKg:N0} kg\n" +
                                 $"Power {d.TotalPowerDrawW:N0} / {c.maxPowerW:N0} W\n" +
                                 $"Cost ${d.TotalCostUsd:N0} / ${c.budgetUsd:N0}";
            if (budgetValueText != null)
                budgetValueText.text = Money(c.budgetUsd);

            var spaceDesigner = designer;
            var violations = spaceDesigner != null ? spaceDesigner.GetDesignViolations() : new List<string>();
            if (violationsText != null)
            {
                violationsText.text = violations.Count == 0
                    ? "All design constraints satisfied. Continue to instruments."
                    : "Design violations (fix before continuing):\n• " + string.Join("\n• ", violations);
                violationsText.color = violations.Count == 0
                    ? new Color(.45f, .95f, .60f)
                    : new Color(1f, .45f, .42f);
            }
            if (nextButton != null)
                nextButton.interactable = violations.Count == 0;
        }

        private void AddDefaultComponent(ComponentType type)
        {
            var comp = MakeDefaultComponent(type);
            var designer = FindFirstObjectByType<SpacecraftDesigner>();
            if (designer != null && comp != null) designer.AddComponent(comp);
            RefreshDesignStats();
        }

        private static SpacecraftComponent MakeDefaultComponent(ComponentType type)
        {
            switch (type)
            {
                case ComponentType.Hull: return new SpacecraftComponent("hull_cmd", "Command Module Hull", type, 5500, 500, 45_000_000, 0, .98, "Pressure hull.");
                case ComponentType.Propulsion: return new SpacecraftComponent("prop_sm", "Service Module Propulsion", type, 12000, 200, 80_000_000, 0, .95, "Main engine and RCS.");
                case ComponentType.Power: return new SpacecraftComponent("power_fc", "Fuel Cell Power", type, 1200, 50, 35_000_000, 0, .97, "Power generation.");
                case ComponentType.Communications: return new SpacecraftComponent("comm_hga", "High-Gain Antenna", type, 300, 300, 25_000_000, 64, .96, "Deep-space comms.");
                case ComponentType.Structure: return new SpacecraftComponent("struct_truss", "Structural Truss", type, 2000, 0, 15_000_000, 0, .99, "Primary structure.");
                case ComponentType.Thermal: return new SpacecraftComponent("thermal_rad", "Thermal Radiators", type, 800, 100, 20_000_000, 0, .97, "Heat rejection.");
                default: return null;
            }
        }

        private void ShowInstruments()
        {
            if (manager == null || manager.CurrentObjective == null) { ShowTitle(); return; }
            Mission3DView.Instance?.Refresh(manager.SpacecraftDesign);
            Clear();
            Background(canvas.transform);
            NewText(canvas.transform, "SCIENCE INSTRUMENTS", 40, new Vector2(-500, 400), new Vector2(860, 60), TextAnchor.MiddleCenter, null, true);
            NewText(canvas.transform, "Destination: " + manager.CurrentObjective.destination + " — toggle instruments for the mission", 20,
                new Vector2(-500, 352), new Vector2(880, 36), TextAnchor.MiddleCenter, new Color(.62f, .72f, .85f));

            var d = manager.SpacecraftDesign;
            NewText(canvas.transform,
                $"Instruments {d.instruments.Count}   ·   Science/day {d.TotalSciencePerDay:F1}   ·   Data/day {d.TotalDataVolumeGbPerDay:F2} GB",
                19, new Vector2(-500, 290), new Vector2(900, 34), TextAnchor.MiddleCenter, new Color(.85f, .90f, .98f));

            var options = GetInstrumentsFor(manager.CurrentObjective.destination);
            for (int i = 0; i < options.Count; i++)
            {
                var inst = options[i];
                float y = 200f - i * 62f;
                bool added = d.instruments.Exists(x => x.instrumentId == inst.instrumentId);
                NewText(canvas.transform,
                    $"{inst.displayName}  ·  {inst.massKg:F0} kg  ·  {inst.powerDrawW:F0} W  ·  sci {inst.scienceValue:F0}/day",
                    18, new Vector2(-640, y), new Vector2(640, 34), TextAnchor.MiddleLeft, new Color(.88f, .92f, .98f));
                NewButton(canvas.transform, added ? "Remove" : "Add", new Vector2(-160, y), new Vector2(140, 46), () =>
                {
                    var existing = d.instruments.Find(x => x.instrumentId == inst.instrumentId);
                    if (existing != null) designer?.RemoveInstrument(existing); else designer?.AddInstrument(inst);
                    ShowInstruments();
                }, added ? new Color(.55f, .28f, .28f) : new Color(.20f, .48f, .32f));
            }

            NewButton(canvas.transform, "Back", new Vector2(-680, -330), new Vector2(220, 50), ShowDesign, new Color(.25f, .28f, .34f));
            var next = NewButton(canvas.transform, "Next: Launch", new Vector2(-360, -330), new Vector2(280, 50), ShowLaunch);
            next.interactable = d.instruments.Count > 0;
        }

        private SpacecraftDesigner designer => FindFirstObjectByType<SpacecraftDesigner>();

        private static List<ScientificInstrument> GetInstrumentsFor(string destination)
        {
            var all = new List<ScientificInstrument>
            {
                new ScientificInstrument("inst_camera", "High-Res Camera", 80, 120, 5.0, 15, new List<string>{"Moon","Mars","LEO"}, "Imaging."),
                new ScientificInstrument("inst_spectrometer", "Infrared Spectrometer", 60, 90, 2.0, 10, new List<string>{"Moon","Mars","LEO"}, "Mineral analysis."),
                new ScientificInstrument("inst_seismometer", "Seismometer", 45, 60, 0.5, 8, new List<string>{"Moon","Mars"}, "Seismic monitoring."),
                new ScientificInstrument("inst_solar_wind", "Solar Wind Experiment", 25, 40, 1.0, 6, new List<string>{"Moon","LEO"}, "Solar wind."),
                new ScientificInstrument("inst_laser_reflector", "Laser Retroreflector", 15, 20, 0.1, 4, new List<string>{"Moon"}, "Lunar ranging."),
            };
            var result = new List<ScientificInstrument>();
            foreach (var i in all) if (i.compatibleDestinations.Contains(destination)) result.Add(i);
            return result;
        }

        private void ShowLaunch()
        {
            if (manager == null || manager.CurrentObjective == null) { ShowTitle(); return; }
            Mission3DView.Instance?.Refresh(manager.SpacecraftDesign);
            Clear();
            Background(canvas.transform);
            NewText(canvas.transform, "LAUNCH VEHICLE & ORBIT", 34, new Vector2(-500, 400), new Vector2(880, 54), TextAnchor.MiddleCenter, null, true);
            NewText(canvas.transform, "Select one launch vehicle and one orbit", 19,
                new Vector2(-500, 352), new Vector2(880, 36), TextAnchor.MiddleCenter, new Color(.62f, .72f, .85f));

            NewText(canvas.transform, "LAUNCH VEHICLES", 18, new Vector2(-730, 300), new Vector2(420, 30), TextAnchor.MiddleCenter, new Color(.55f, .72f, .95f), true);
            var vehicles = GetVehicles();
            var selectedVehicle = manager.SpacecraftDesign.launchVehicle;
            for (int i = 0; i < vehicles.Count; i++)
            {
                var v = vehicles[i];
                bool sel = selectedVehicle != null && selectedVehicle.vehicleId == v.vehicleId;
                NewButton(canvas.transform, $"{v.displayName}\n${v.costUsd / 1_000_000:F0}M · {v.maxPayloadKg:F0} kg",
                    new Vector2(-730, 240 - i * 70), new Vector2(420, 60), () =>
                    {
                        designer?.SetLaunchVehicle(v);
                        ShowLaunch();
                    }, sel ? new Color(.20f, .55f, .40f) : new Color(.16f, .34f, .58f), 16);
            }

            NewText(canvas.transform, "ORBITS", 18, new Vector2(-270, 300), new Vector2(420, 30), TextAnchor.MiddleCenter, new Color(.55f, .72f, .95f), true);
            var orbits = GetOrbits();
            var selectedOrbit = manager.SpacecraftDesign.orbit;
            for (int i = 0; i < orbits.Count; i++)
            {
                var o = orbits[i];
                bool sel = selectedOrbit != null && selectedOrbit.orbitId == o.orbitId;
                NewButton(canvas.transform, $"{o.displayName}\n{o.deltaVRequiredKmS:F0} km/s · science ×{o.scienceMultiplier:F1}",
                    new Vector2(-270, 240 - i * 70), new Vector2(420, 60), () =>
                    {
                        designer?.SetOrbit(o);
                        ShowLaunch();
                    }, sel ? new Color(.20f, .55f, .40f) : new Color(.16f, .34f, .58f), 16);
            }

            var d = manager.SpacecraftDesign;
            string summary = $"Mass {d.TotalMassKg:N0} kg · Power {d.TotalPowerDrawW:N0} W · Cost ${d.TotalCostUsd:N0}\n" +
                             $"Vehicle: {(selectedVehicle != null ? selectedVehicle.displayName : "none")}   ·   Orbit: {(selectedOrbit != null ? selectedOrbit.displayName : "none")}";
            NewText(canvas.transform, summary, 17, new Vector2(-500, -40), new Vector2(880, 70), TextAnchor.MiddleCenter, new Color(.88f, .92f, .98f));

            NewButton(canvas.transform, "Back", new Vector2(-680, -330), new Vector2(220, 50), ShowInstruments, new Color(.25f, .28f, .34f));
            NewButton(canvas.transform, "SIMULATE MISSION", new Vector2(-360, -330), new Vector2(280, 50), Simulate, new Color(.55f, .38f, .12f));
        }

        private static List<LaunchVehicle> GetVehicles()
        {
            return new List<LaunchVehicle>
            {
                new LaunchVehicle("lv_saturn_v", "Saturn V", 185000000, 140000, 12000, new List<string>{"LEO","Lunar"}, .96, ""),
                new LaunchVehicle("lv_falcon_heavy", "Falcon Heavy", 150000000, 63800, 10000, new List<string>{"LEO","GTO","Lunar"}, .94, ""),
                new LaunchVehicle("lv_atlas_v", "Atlas V", 110000000, 18800, 6000, new List<string>{"LEO","GTO"}, .97, ""),
                new LaunchVehicle("lv_delta_iv", "Delta IV Heavy", 350000000, 28790, 11000, new List<string>{"LEO","GTO","Lunar"}, .95, ""),
            };
        }

        private static List<OrbitDefinition> GetOrbits()
        {
            return new List<OrbitDefinition>
            {
                new OrbitDefinition("LEO", "Low Earth Orbit", 9400, 1.0, .1, ""),
                new OrbitDefinition("GTO", "Geostationary Transfer", 10500, 1.2, .25, ""),
                new OrbitDefinition("Lunar", "Lunar Orbit", 12000, 2.0, 2.5, ""),
            };
        }

        private void Simulate()
        {
            var des = designer;
            var violations = des != null ? des.GetConstraintViolations() : new List<string>();
            if (violations.Count > 0)
            {
                NewText(canvas.transform, "Fix these first:\n• " + string.Join("\n• ", violations), 17,
                    new Vector2(-500, -170), new Vector2(880, 90), TextAnchor.MiddleCenter, new Color(1f, .45f, .42f));
                return;
            }
            var engine = FindFirstObjectByType<SimulationEngine>();
            if (engine == null) { Debug.LogError("No SimulationEngine found."); return; }
            engine.RunSimulation();

            // Reset the 3D stack and fly it while the results are being prepared.
            Mission3DView.Instance?.Refresh(manager.SpacecraftDesign);
            Mission3DView.Instance?.PlayLaunch();
            NewText(canvas.transform, "LIFTOFF — running flight simulation…", 24,
                new Vector2(-500, -170), new Vector2(880, 44), TextAnchor.MiddleCenter, new Color(1f, .82f, .45f), true);

            // Freeze input during the launch sequence.
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;

            Invoke(nameof(ShowResults), 2.6f);
        }

        private void ShowResults()
        {
            var result = manager != null ? manager.SimulationResult : null;
            if (result == null) { ShowTitle(); return; }
            Clear();
            Background(canvas.transform);
            NewText(canvas.transform, "MISSION RESULTS", 36, new Vector2(-500, 400), new Vector2(880, 56), TextAnchor.MiddleCenter, null, true);

            NewText(canvas.transform, result.missionSuccess ? "MISSION SUCCESS" : "MISSION FAILED", 40,
                new Vector2(-500, 320), new Vector2(880, 56), TextAnchor.MiddleCenter,
                result.missionSuccess ? new Color(.40f, .95f, .55f) : new Color(1f, .42f, .40f), true);

            NewText(canvas.transform, $"Mission Score: {result.score:F1} / 100", 26,
                new Vector2(-500, 255), new Vector2(880, 44), TextAnchor.MiddleCenter, new Color(.90f, .94f, 1f), true);

            NewText(canvas.transform, string.Join("\n", result.feedbackMessages), 15,
                new Vector2(-500, 60), new Vector2(880, 300), TextAnchor.UpperCenter, new Color(.82f, .88f, .96f));

            NewButton(canvas.transform, "DESIGN ANOTHER MISSION", new Vector2(-500, -330), new Vector2(360, 54), () =>
            {
                if (manager != null) manager.ResetMission();
                ShowTitle();
            }, new Color(.18f, .42f, .62f));
        }
    }
}
