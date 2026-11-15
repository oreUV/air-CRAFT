using System;
using System.Collections.Generic;
using UnityEngine;
using SpaceMissionDesign.Data;

namespace SpaceMissionDesign.UI
{
    public sealed class Mission3DView : MonoBehaviour
    {
        public static Mission3DView Instance { get; private set; }

        /// <summary>Fired once when the full Earth->Moon->Earth flight completes (or is skipped).</summary>
        public static Action MissionComplete;

        public enum FlightPhase { PreLaunch, Ascent, TransitToMoon, MoonArrival, MoonStay, ReturnToEarth, EarthArrival, Complete }

        private Transform rocketRoot;
        private Transform focus;
        private OrbitCamera cameraRig;
        private Camera rigCamera;
        private float nextCameraAudit;
        private Vector3 flameBaseScale = Vector3.one;
        private Transform flame;
        private ParticleSystem exhaust;
        private Vector3 focusHome = new Vector3(10f, 14f, 0f);
        private Material white, metal, dark, gold, blue, grey, red, pad, flag, earthMat, moonMat, flameMat, exhaustMat, cloudMat;

        // ---------------- world layout (all fixed; the rocket travels through it) ----------------
        private static readonly Vector3 EarthCenter = new Vector3(10f, -300f, 0f);
        private const float EarthR = 300f;        // surface at y = 0 under the pad
        private const float CloudShellR = 322f;   // clouds pass by at ~22 units altitude
        private const float AtmoFadeStart = 14f;  // sky starts losing blue
        private const float AtmoFadeEnd = 150f;   // fully space-black here
        private static readonly Vector3 MoonCenter = new Vector3(10f, 260f, 2900f);
        private const float MoonR = 240f;

        private Transform earthSphere;
        private Transform cloudLayer;
        private Transform moonSphere;
        private GameObject starfieldGO;
        private Material starMat;
        private Texture2D earthTex;
        private Texture2D cloudTex;

        // ---------------- flight state ----------------
        private FlightPhase phase = FlightPhase.PreLaunch;
        private float phaseElapsed;
        private Vector3 flightDir = Vector3.up;
        private float flightSpeed;

        private Color surfaceSky = new Color(.45f, .68f, .92f);
        private Color spaceSky = new Color(.012f, .014f, .025f);

        private const float EngineH = 3.5f;
        private const float Stage1H = 14.4f;
        private const float Stage2H = 10.2f;
        private const float Stage1D = 5.4f;
        private const float Stage2D = 4.2f;
        private const float ServiceH = 2.5f;
        private const float ServiceD = 3.4f;
        private const float CapsuleH = 4.5f;
        private const float CapsuleD = 3.8f;
        private const float Stage1Base = EngineH;
        private const float Stage2Base = Stage1Base + Stage1H - 1.8f;
        private const float ServiceBase = Stage2Base + Stage2H - .6f;
        private const float CapsuleBase = ServiceBase + ServiceH - .1f;
        private const float StackTop = CapsuleBase + CapsuleH;

        public void Initialize()
        {
            Instance = this;
            BuildMaterials();
            BuildWorld();
            BuildCamera();
            Refresh(null);
            Invoke(nameof(DumpDiagnostics), 1.5f);
        }

        /// <summary>
        /// One-time render diagnostic. Logs which camera renders, which camera we drive,
        /// and whether the key objects are visible / in front of the camera.
        /// </summary>
        /// <summary>
        /// Keeps exactly one camera rendering: ours, at the highest depth. Re-checked twice
        /// a second because a scene camera can appear after startup.
        /// </summary>
        private void EnforceSingleCamera()
        {
            if (rigCamera == null) return;
            if (Time.unscaledTime < nextCameraAudit) return;
            nextCameraAudit = Time.unscaledTime + .5f;

            if (!rigCamera.enabled) rigCamera.enabled = true;
            if (rigCamera.depth < 100f) rigCamera.depth = 100f;

            var cams = Camera.allCameras;
            for (int i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (c == null || c == rigCamera) continue;
                c.enabled = false;
                Debug.LogWarning($"Mission3DView: disabled extra camera '{c.name}' - it was drawing over the 3D view.");
            }
        }

        private void DumpDiagnostics()
        {
            var cam = Camera.main;
            Debug.Log($"[DIAG] pipeline={(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().Name : "built-in")} standardShaderFound={Shader.Find("Standard") != null}");
            Debug.Log($"[DIAG] cameraCount={Camera.allCamerasCount} main={(cam != null ? cam.name : "NULL")} " +
                      $"enabled={(cam != null && cam.enabled)} rect={(cam != null ? cam.pixelRect.ToString() : "-")} " +
                      $"pos={(cam != null ? cam.transform.position.ToString() : "-")} fwd={(cam != null ? cam.transform.forward.ToString() : "-")} " +
                      $"fov={(cam != null ? cam.fieldOfView : 0f):F0} near={(cam != null ? cam.nearClipPlane : 0f)} far={(cam != null ? cam.farClipPlane : 0f)} " +
                      $"clear={(cam != null ? cam.clearFlags.ToString() : "-")} mask={(cam != null ? cam.cullingMask : 0)}");
            foreach (var c in Camera.allCameras)
                Debug.Log($"[DIAG] camera '{c.name}' enabled={c.enabled} depth={c.depth} display={c.targetDisplay}");
            DumpRenderer("Earth", earthSphere);
            DumpRenderer("Moon", moonSphere);
            DumpRenderer("Rocket", rocketRoot);
            if (rocketRoot != null && cam != null)
            {
                var sp = cam.WorldToScreenPoint(rocketRoot.position + Vector3.up * 14f);
                Debug.Log($"[DIAG] rocketScreenPoint={sp} screen={Screen.width}x{Screen.height} (z<0 = behind camera)");
            }
        }

        private void DumpRenderer(string label, Transform t)
        {
            if (t == null) { Debug.Log($"[DIAG] {label}: transform NULL"); return; }
            var rs = t.GetComponentsInChildren<Renderer>();
            Debug.Log($"[DIAG] {label}: renderers={rs.Length}");
            for (int i = 0; i < Mathf.Min(rs.Length, 5); i++)
            {
                var r = rs[i];
                string shader = r.sharedMaterial != null ? r.sharedMaterial.shader.name : "no-material";
                Debug.Log($"[DIAG]   {label}[{i}] '{r.name}' enabled={r.enabled} visible={r.isVisible} shader={shader} bounds={r.bounds}");
            }
        }

        // ---------------- status for the HUD ----------------

        public bool InFlight => phase != FlightPhase.PreLaunch && phase != FlightPhase.Complete;
        public FlightPhase CurrentPhase => phase;

        public float AltitudeUnits
        {
            get
            {
                if (rocketRoot == null) return 0f;
                return (rocketRoot.position - EarthCenter).magnitude - EarthR;
            }
        }

        /// <summary>One HUD line: phase + altitude/distance readout.</summary>
        public string FlightStatus
        {
            get
            {
                const float kmPerUnit = 6f; // display scale
                switch (phase)
                {
                    case FlightPhase.PreLaunch: return "READY ON PAD";
                    case FlightPhase.Ascent: return $"ASCENT  ·  ALT {AltitudeUnits * kmPerUnit:N0} km";
                    case FlightPhase.TransitToMoon: return $"TRANSLUNAR  ·  MOON {Vector3.Distance(rocketRoot != null ? rocketRoot.position : EarthCenter, MoonCenter) * kmPerUnit:N0} km";
                    case FlightPhase.MoonArrival: return "LUNAR APPROACH  ·  BRAKING";
                    case FlightPhase.MoonStay: return $"LUNAR ORBIT  ·  {(phaseElapsed - MoonStayTotal < 0 ? $"T-{MoonStayTotal - (phaseElapsed - MoonStayTotal):F0}s" : "")}";
                    case FlightPhase.ReturnToEarth: return $"TRANS-EARTH  ·  HOME {Vector3.Distance(rocketRoot != null ? rocketRoot.position : MoonCenter, new Vector3(10f, 0f, 0f)) * kmPerUnit:N0} km";
                    case FlightPhase.EarthArrival: return "ENTRY  ·  DESCENDING";
                    default: return "MISSION COMPLETE";
                }
            }
        }

        private const float MoonStayTotal = 5f;

        // ---------------- materials ----------------

        private Material Mat(string name, Texture2D tex, Color? tint = null, float metallic = .2f, float smooth = .45f, Color? emission = null)
        {
            Shader s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Unlit/Color");
            var m = new Material(s) { name = name };
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (tint.HasValue)
            {
                if (m.HasProperty("_Color")) m.SetColor("_Color", tint.Value);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint.Value);
            }
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (emission.HasValue && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            return m;
        }

        private void BuildMaterials()
        {
            white = Mat("Hull white", TextureGenerator.WhitePaint(), tint: new Color(.92f, .94f, .97f), .2f, .5f);
            metal = Mat("Metal", TextureGenerator.Metal(), tint: new Color(.6f, .64f, .68f), .7f, .75f);
            dark = Mat("Dark", TextureGenerator.ThermalTiles(), tint: new Color(.18f, .18f, .19f), .35f, .45f);
            gold = Mat("Gold", TextureGenerator.GoldFoil(), tint: new Color(.9f, .7f, .3f), .8f, .7f);
            blue = Mat("Blue", TextureGenerator.Metal(), tint: new Color(.25f, .4f, .65f), .3f, .5f);
            grey = Mat("Grey", TextureGenerator.Metal(), tint: new Color(.45f, .48f, .52f), .4f, .5f);
            red = Mat("Red", TextureGenerator.Metal(), tint: new Color(.7f, .25f, .2f), .3f, .5f);
            pad = Mat("Pad", TextureGenerator.Metal(), tint: new Color(.18f, .19f, .21f), .15f, .35f);
            flag = Mat("Flag", TextureGenerator.Flag(), tint: Color.white, 0f, .3f);

            earthTex = Resources.Load<Texture2D>("Textures/Earth (A)");
            var earthBRaw = Resources.Load<Texture2D>("Textures/Earth (B)");
            if (earthBRaw != null)
            {
                try { cloudTex = MakeCloudsFromMap(earthBRaw); }
                catch { cloudTex = TextureGenerator.Clouds(); Debug.LogWarning("Mission3DView: Earth (B) not readable; using procedural clouds."); }
            }
            else cloudTex = TextureGenerator.Clouds();

            earthMat = Mat("Earth", earthTex ?? TextureGenerator.Earth(), tint: Color.white, .02f, .2f, new Color(.01f, .04f, .09f));
            cloudMat = Mat("Clouds", cloudTex, tint: Color.white, 0f, .1f, null);
            if (cloudMat.HasProperty("_Mode")) cloudMat.SetFloat("_Mode", 3f);
            if (cloudMat.HasProperty("_SrcBlend")) cloudMat.SetFloat("_SrcBlend", 1f);
            if (cloudMat.HasProperty("_DstBlend")) cloudMat.SetFloat("_DstBlend", 10f);
            if (cloudMat.HasProperty("_ZWrite")) cloudMat.SetFloat("_ZWrite", 0f);
            if (cloudMat.HasProperty("_Cull")) cloudMat.SetFloat("_Cull", 0f); // visible from inside too
            cloudMat.renderQueue = 3000;

            moonMat = Mat("Moon", TextureGenerator.Moon(), tint: Color.white, .02f, .15f);

            flameMat = Mat("Flame", TextureGenerator.SoftDot(), tint: new Color(1f, .55f, .1f), 0f, .05f, new Color(1f, .4f, .05f) * 2.2f);
            exhaustMat = Mat("Exhaust", TextureGenerator.SoftDot(), tint: new Color(1f, .7f, .3f), 0f, .05f);
        }

        private static Texture2D MakeCloudsFromMap(Texture2D source)
        {
            var tex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            var src = source.GetPixels32();
            var dst = new Color32[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                float bright = (src[i].r + src[i].g + src[i].b) / 3f;
                float alpha = Mathf.Clamp01((bright - .35f) * 2.2f);
                dst[i] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
            tex.SetPixels32(dst);
            tex.Apply();
            return tex;
        }

        // ---------------- world ----------------

        private void BuildWorld()
        {
            RenderSettings.ambientLight = new Color(.30f, .34f, .40f);

            var lightGO = new GameObject("Sun");
            var sun = lightGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.05f;
            sun.color = new Color(1f, .97f, .90f);
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            DontDestroyOnLoad(lightGO);

            // Earth: fixed planet whose surface sits at y = 0 under the launch pad.
            // The rocket physically climbs away from it - the horizon curvature,
            // shrinking planet and thinning sky all come from real geometry.
            earthSphere = Sphere("Earth", EarthCenter, EarthR * 2f, earthMat, null).transform;

            // Cloud shell hugging the surface - the rocket flies straight through it.
            cloudLayer = Sphere("Cloud layer", EarthCenter, CloudShellR * 2f, cloudMat, null).transform;
            if (cloudLayer.TryGetComponent<MeshRenderer>(out var cr) && cr.material != null)
            {
                var c = cr.material.color;
                c.a = .55f;
                cr.material.color = c;
            }

            // Launch pad on the surface.
            Cylinder("Launch pad", new Vector3(10f, 0f, 0f), 14f, .7f, pad, null);
            Cylinder("Pad ring", new Vector3(10f, .3f, 0f), 8f, .35f, grey, null);

            // Moon: fixed far ahead along the flight path.
            moonSphere = Sphere("Moon", MoonCenter, MoonR * 2f, moonMat, null).transform;

            // Temporary render check: an unlit magenta marker floating above the pad.
            // If this is not visible on screen, nothing 3D is rendering at all.
            var markerMat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            if (markerMat.HasProperty("_Color")) markerMat.SetColor("_Color", new Color(1f, 0f, .8f));
            var marker = Cube("Render Check Marker", new Vector3(10f, 48f, 0f), new Vector3(4f, 4f, 4f), markerMat, null);
            DontDestroyOnLoad(marker);

            BuildStars();

            if (Camera.main != null) Camera.main.backgroundColor = surfaceSky;
        }

        private void BuildStars()
        {
            starfieldGO = new GameObject("Starfield");
            starfieldGO.transform.position = new Vector3(0, 0, 800);
            var ps = starfieldGO.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startSpeed = 0f;
            main.startLifetime = 9999f;
            main.startSize = new ParticleSystem.MinMaxCurve(9f, 22f);
            main.maxParticles = 1600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.radius = 3600f;
            shape.radiusThickness = .04f; // thin shell of stars

            Shader psShader = Shader.Find("Particles/Standard Unlit");
            if (psShader == null) psShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (psShader == null) psShader = Shader.Find("Sprites/Default");
            var psMat = new Material(psShader != null ? psShader : Shader.Find("Unlit/Color"));
            psMat.mainTexture = TextureGenerator.SoftDot();
            if (psMat.HasProperty("_Color")) psMat.SetColor("_Color", new Color(.92f, .95f, 1f, 0f));
            starMat = psMat;
            var renderer = starfieldGO.GetComponent<ParticleSystemRenderer>();
            renderer.material = psMat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Emit(1600);

            DontDestroyOnLoad(starfieldGO);
        }

        private void BuildCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
                DontDestroyOnLoad(go);
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = surfaceSky;
            cam.fieldOfView = 42f;
            cam.nearClipPlane = .3f;
            cam.farClipPlane = 9000f; // reaches the Moon and the star shell

            // This camera must be the ONLY one drawing the game view, and must draw last.
            // A second enabled camera (e.g. a scene camera named "Camera") with a higher
            // depth renders an opaque frame over ours, freezing the visible picture even
            // though our camera is orbiting correctly.
            rigCamera = cam;
            cam.depth = 100f;
            cam.enabled = true;
            cam.cullingMask = ~0;
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.targetTexture = null;
            EnforceSingleCamera();

            var focusGO = new GameObject("Camera Focus");
            focusGO.transform.position = focusHome;
            DontDestroyOnLoad(focusGO);
            focus = focusGO.transform;

            var orbit = cam.GetComponent<OrbitCamera>();
            if (orbit == null) orbit = cam.gameObject.AddComponent<OrbitCamera>();
            orbit.target = focus;
            orbit.minDistance = 16f;
            orbit.maxDistance = 900f;
            orbit.homeDistance = 40f;
            orbit.distance = 30f;
            orbit.targetDistance = 30f;
            orbit.pitch = -22f;
            orbit.yaw = 0f;
            cameraRig = orbit;

            orbit.enabled = false;
            var pivot = focus.position;
            var rot = Quaternion.Euler(orbit.pitch, orbit.yaw, 0f);
            cam.transform.position = pivot + rot * new Vector3(0f, 0f, -orbit.distance);
            cam.transform.LookAt(pivot);
            orbit.enabled = true;
        }

        // ---------------- model import ----------------

        /// <summary>
        /// Loads the Saturn V.fbx rocket as a single complete model and places it on the
        /// launch pad on Earth. Falls back to a primitive stack if the fbx is missing.
        /// </summary>
        private Transform PlaceSaturnVFbx(Transform parent, Vector3 at, float targetHeight)
        {
            var prefab = Resources.Load<GameObject>("Models/saturn");
            if (prefab != null)
            {
                var go = Instantiate(prefab, parent);
                go.name = "Saturn V";
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                foreach (var col in go.GetComponentsInChildren<Collider>()) Destroy(col);
                foreach (var anim in go.GetComponentsInChildren<Animator>()) Destroy(anim);

                if (TryLocalBounds(go, parent, out var bounds))
                {
                    var upright = AxisToUpRotation(bounds.size);
                    if (upright != Quaternion.identity)
                    {
                        go.transform.localRotation = upright;
                        if (TryLocalBounds(go, parent, out var stood)) bounds = stood;
                    }
                    if (WideEndIsUp(go))
                    {
                        go.transform.localRotation = Quaternion.Euler(180f, 0f, 0f) * go.transform.localRotation;
                        if (TryLocalBounds(go, parent, out var flipped)) bounds = flipped;
                    }

                    float h = Mathf.Max(bounds.size.y, .0001f);
                    var scale = Vector3.one * (targetHeight / h);
                    go.transform.localScale = scale;

                    Vector3 min = Vector3.Scale(bounds.min, scale);
                    Vector3 max = Vector3.Scale(bounds.max, scale);
                    Vector3 centre = (min + max) * .5f;

                    go.transform.localPosition = at - new Vector3(centre.x, min.y, centre.z);
                    return go.transform;
                }
                Destroy(go);
            }

            Debug.LogWarning("Mission3DView: Saturn V.fbx not found or invalid, using primitive rocket.");
            return null;
        }

        private static bool WideEndIsUp(GameObject go)
        {
            var points = new List<Vector3>(1024);
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null || !mesh.isReadable) return false;
                var toWorld = mf.transform.localToWorldMatrix;
                Vector3[] vertices = mesh.vertices;
                int step = Mathf.Max(1, vertices.Length / 500);
                for (int i = 0; i < vertices.Length; i += step)
                    points.Add(toWorld.MultiplyPoint3x4(vertices[i]));
            }
            if (points.Count < 60) return false;
            float minY = float.MaxValue, maxY = float.MinValue;
            float sumX = 0f, sumZ = 0f;
            foreach (var p in points)
            {
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
                sumX += p.x;
                sumZ += p.z;
            }
            float centreX = sumX / points.Count, centreZ = sumZ / points.Count;
            float height = Mathf.Max(maxY - minY, 1e-4f);
            float lowCut = minY + height * .18f;
            float highCut = maxY - height * .18f;
            float lowSpread = 0f, highSpread = 0f;
            int lowCount = 0, highCount = 0;
            foreach (var p in points)
            {
                float r = new Vector2(p.x - centreX, p.z - centreZ).magnitude;
                if (p.y <= lowCut) { lowSpread += r; lowCount++; }
                else if (p.y >= highCut) { highSpread += r; highCount++; }
            }
            if (lowCount < 8 || highCount < 8) return false;
            lowSpread /= lowCount;
            highSpread /= highCount;
            return highSpread > lowSpread * 1.12f;
        }

        private static bool TryLocalBounds(GameObject go, Transform space, out Bounds bounds)
        {
            bounds = new Bounds();
            if (space == null) return false;
            bool has = false;
            var filters = go.GetComponentsInChildren<MeshFilter>();
            foreach (var mf in filters)
            {
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null) continue;
                var toSpace = space.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var mb = mesh.bounds;
                Vector3 c = mb.center, e = mb.extents;
                for (int i = 0; i < 8; i++)
                {
                    var corner = c + new Vector3(
                        (i & 1) == 0 ? -e.x : e.x,
                        (i & 2) == 0 ? -e.y : e.y,
                        (i & 4) == 0 ? -e.z : e.z);
                    var lp = toSpace.MultiplyPoint3x4(corner);
                    if (!has) { bounds = new Bounds(lp, Vector3.zero); has = true; }
                    else bounds.Encapsulate(lp);
                }
            }
            if (has) return true;
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return false;
            foreach (var r in renderers)
            {
                var wb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = wb.center + new Vector3(
                        (i & 1) == 0 ? -wb.extents.x : wb.extents.x,
                        (i & 2) == 0 ? -wb.extents.y : wb.extents.y,
                        (i & 4) == 0 ? -wb.extents.z : wb.extents.z);
                    var lp = space.InverseTransformPoint(corner);
                    if (!has) { bounds = new Bounds(lp, Vector3.zero); has = true; }
                    else bounds.Encapsulate(lp);
                }
            }
            return has;
        }

        private static Quaternion AxisToUpRotation(Vector3 size)
        {
            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (longest <= size.y * 1.03f) return Quaternion.identity;
            if (size.x >= size.z)
                return Quaternion.Euler(0f, 0f, 90f);
            else
                return Quaternion.Euler(-90f, 0f, 0f);
        }

        // ---------------- rocket assembly ----------------

        public void Refresh(SpacecraftDesign design)
        {
            if (rocketRoot != null) Destroy(rocketRoot.gameObject);
            ResetFlightState();

            var root = new GameObject("Rocket").transform;
            root.position = new Vector3(10f, 0f, 0f);
            root.rotation = Quaternion.identity;
            rocketRoot = root;

            var saturnV = PlaceSaturnVFbx(root, new Vector3(0, 0, 0), 28f);

            if (saturnV == null)
            {
                Cylinder("Stage 1", new Vector3(0, Stage1Base + Stage1H * .5f, 0), Stage1D * .5f, Stage1H, white, root);
                Cylinder("Stage 2", new Vector3(0, Stage2Base + Stage2H * .5f, 0), Stage2D * .5f, Stage2H, metal, root);
                Cylinder("Service module", new Vector3(0, ServiceBase + ServiceH * .5f, 0), ServiceD * .5f, ServiceH, metal, root);
                Sphere("Command module", new Vector3(0, CapsuleBase + CapsuleH * .45f, 0), CapsuleD, white, root);
                Cylinder("Escape tower", new Vector3(0, StackTop + .6f, 0), .11f, 1.2f, dark, root);
                Sphere("Tower tip", new Vector3(0, StackTop + 1.25f, 0), .24f, dark, root);
            }

            var flameGO = Cylinder("Engine flame", new Vector3(0, -3.4f, 0), 1.2f, 6f, flameMat, root);
            flame = flameGO.transform;
            flameBaseScale = flame.localScale;
            flame.gameObject.SetActive(false);

            BuildExhaust(root, -3.4f);

            if (focus != null) focus.position = focusHome;
            if (cameraRig != null) cameraRig.ClearPan();
            if (Camera.main != null) Camera.main.backgroundColor = surfaceSky;

            DontDestroyOnLoad(root.gameObject);
        }

        private void ResetFlightState()
        {
            phase = FlightPhase.PreLaunch;
            phaseElapsed = 0f;
            flightDir = Vector3.up;
            flightSpeed = 0f;
            if (flame != null) flame.gameObject.SetActive(false);
            if (exhaust != null)
            {
                var em = exhaust.emission;
                em.enabled = false;
            }
        }

        private void BuildExhaust(Transform root, float y)
        {
            var go = new GameObject("Exhaust");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.55f, .95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(10f, 19f);
            main.startSize = new ParticleSystem.MinMaxCurve(.8f, 2.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .78f, .32f, .95f), new Color(1f, .38f, .06f, .45f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 900;
            main.gravityModifier = .08f;
            var emission = ps.emission;
            emission.enabled = false;
            emission.rateOverTime = 320f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = .7f;
            shape.rotation = new Vector3(180f, 0f, 0f);
            Shader psShader = Shader.Find("Particles/Standard Unlit");
            if (psShader == null) psShader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (psShader == null) psShader = Shader.Find("Sprites/Default");
            var psMat = new Material(psShader != null ? psShader : Shader.Find("Unlit/Color"));
            psMat.mainTexture = TextureGenerator.SoftDot();
            if (psMat.HasProperty("_Color")) psMat.SetColor("_Color", new Color(1f, .72f, .35f, .9f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = psMat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 2f;
            exhaust = ps;
        }

        // ---------------- continuous flight ----------------

        public void PlayLaunch()
        {
            if (rocketRoot == null || InFlight) return;
            ResetFlightState();
            cameraRig?.ClearPan();
            phase = FlightPhase.Ascent;
            phaseElapsed = 0f;
            if (Camera.main != null) Camera.main.backgroundColor = surfaceSky;
        }

        /// <summary>Ends the flight immediately (skip button) and reports completion.</summary>
        public void SkipToResults()
        {
            if (!InFlight) return;
            phase = FlightPhase.Complete;
            if (flame != null) flame.gameObject.SetActive(false);
            if (exhaust != null)
            {
                var em = exhaust.emission;
                em.enabled = false;
            }
            MissionComplete?.Invoke();
        }

        private float MoonStayClock;

        private void Update()
        {
            EnforceSingleCamera();
            if (rocketRoot == null) return;

            // Camera focus chases the rocket the whole time so the player can orbit freely.
            Vector3 focusTarget = phase == FlightPhase.PreLaunch
                ? focusHome
                : rocketRoot.position + Vector3.up * 10f;
            float k = 1f - Mathf.Exp(-3.5f * Time.deltaTime);
            focus.position = Vector3.Lerp(focus.position, focusTarget, k);

            if (!InFlight) return;

            phaseElapsed += Time.deltaTime;
            StepFlight();

            // --- environment driven by real altitude ---
            float alt = AltitudeUnits;

            // Sky fades from blue to space-black as the atmosphere thins.
            float skyT = Mathf.Clamp01((alt - AtmoFadeStart) / (AtmoFadeEnd - AtmoFadeStart));
            if (Camera.main != null)
                Camera.main.backgroundColor = Color.Lerp(surfaceSky, spaceSky, skyT);

            // Stars fade in with altitude.
            if (starMat != null && starMat.HasProperty("_Color"))
            {
                Color c = starMat.color;
                c.a = Mathf.Clamp01((alt - 70f) / 110f);
                starMat.color = c;
            }

            // Cloud alpha: thicken through the shell, gone once clear of the atmosphere.
            if (cloudLayer.TryGetComponent<MeshRenderer>(out var cloudRend) && cloudRend.material != null)
            {
                Color c = cloudRend.material.color;
                c.a = .55f * (1f - Mathf.Clamp01((alt - 40f) / 90f));
                cloudRend.material.color = c;
            }

            UpdateEngineVisuals();
        }

        private void StepFlight()
        {
            var pos = rocketRoot.position;
            float dt = Time.deltaTime;
            Vector3 toMoon = (MoonCenter - EarthCenter).normalized;
            Vector3 upNow = (pos - EarthCenter).normalized;
            float alt = (pos - EarthCenter).magnitude - EarthR;

            switch (phase)
            {
                case FlightPhase.Ascent:
                {
                    // Continuous acceleration off the pad; pitch over toward the Moon
                    // between turnStartAlt and turnFullAlt (classic gravity turn).
                    const float turnStartAlt = 70f, turnFullAlt = 260f;
                    float climb = Mathf.Clamp01(alt / turnFullAlt);
                    flightSpeed = Mathf.Lerp(9f, 165f, climb);

                    float turn = Mathf.Clamp01((alt - turnStartAlt) / (turnFullAlt - turnStartAlt));
                    flightDir = Vector3.Slerp(Vector3.up, toMoon, turn).normalized;

                    pos += flightDir * (flightSpeed * dt);

                    if (turn >= 1f)
                    {
                        phase = FlightPhase.TransitToMoon;
                        phaseElapsed = 0f;
                    }
                    break;
                }

                case FlightPhase.TransitToMoon:
                {
                    flightSpeed = 430f;
                    flightDir = (MoonCenter - pos).normalized;
                    pos += flightDir * (flightSpeed * dt);

                    float distMoonSurf = Vector3.Distance(pos, MoonCenter) - MoonR;
                    if (distMoonSurf < 850f)
                    {
                        phase = FlightPhase.MoonArrival;
                        phaseElapsed = 0f;
                    }
                    break;
                }

                case FlightPhase.MoonArrival:
                {
                    float distMoonSurf = Vector3.Distance(pos, MoonCenter) - MoonR;
                    float brake = Mathf.Clamp01(1f - distMoonSurf / 850f);
                    flightSpeed = Mathf.Lerp(430f, 55f, brake * brake);
                    flightDir = (MoonCenter - pos).normalized;
                    pos += flightDir * (flightSpeed * dt);

                    if (distMoonSurf < 60f)
                    {
                        phase = FlightPhase.MoonStay;
                        phaseElapsed = 0f;
                        MoonStayClock = 0f;
                        SetBurn(false);
                    }
                    break;
                }

                case FlightPhase.MoonStay:
                {
                    // Glide around the Moon while the crew does science.
                    MoonStayClock += dt;
                    Vector3 orbitNormal = Vector3.Cross(toMoon, Vector3.right).normalized;
                    Vector3 a = Vector3.Cross(orbitNormal, toMoon).normalized;
                    Vector3 b = orbitNormal;
                    float ang = MoonStayClock * .55f;
                    pos = MoonCenter + (a * Mathf.Cos(ang) + b * Mathf.Sin(ang)) * (MoonR + 85f);

                    if (MoonStayClock >= MoonStayTotal)
                    {
                        phase = FlightPhase.ReturnToEarth;
                        phaseElapsed = 0f;
                        flightSpeed = 60f;
                        SetBurn(true);
                    }
                    break;
                }

                case FlightPhase.ReturnToEarth:
                {
                    Vector3 home = new Vector3(10f, 40f, 0f);
                    flightDir = (home - pos).normalized;
                    float distHomeSurf = Vector3.Distance(pos, new Vector3(10f, 0f, 0f));
                    if (distHomeSurf < 900f)
                    {
                        float brake = Mathf.Clamp01(1f - distHomeSurf / 900f);
                        flightSpeed = Mathf.Max(Mathf.Lerp(430f, 26f, brake * brake), 18f);
                    }
                    else
                    {
                        flightSpeed = Mathf.Min(flightSpeed + 300f * dt, 430f);
                    }
                    pos += flightDir * (flightSpeed * dt);

                    if (distHomeSurf < 120f)
                    {
                        phase = FlightPhase.EarthArrival;
                        phaseElapsed = 0f;
                    }
                    break;
                }

                case FlightPhase.EarthArrival:
                {
                    float altHome = (pos - EarthCenter).magnitude - EarthR;
                    flightSpeed = Mathf.Max(flightSpeed - 90f * dt, 10f);
                    Vector3 descend = (EarthCenter - pos).normalized;
                    pos += descend * (flightSpeed * dt);

                    if (altHome <= .5f)
                    {
                        // Touch down on the pad.
                        Vector3 radial = (pos - EarthCenter).normalized;
                        pos = EarthCenter + radial * (EarthR + .01f);
                        pos.x = 10f; pos.z = 0f;
                        rocketRoot.position = pos;
                        rocketRoot.rotation = Quaternion.LookRotation(Vector3.forward, radial);
                        phase = FlightPhase.Complete;
                        SetBurn(false);
                        MissionComplete?.Invoke();
                        return;
                    }
                    break;
                }
            }

            rocketRoot.position = pos;

            // Nose follows the flight direction.
            if (phase != FlightPhase.MoonStay && phase != FlightPhase.EarthArrival)
            {
                Quaternion want = Quaternion.FromToRotation(Vector3.up, flightDir);
                rocketRoot.rotation = Quaternion.Slerp(rocketRoot.rotation, want, 1f - Mathf.Exp(-2.2f * dt));
            }
        }

        private void SetBurn(bool on)
        {
            if (flame != null) flame.gameObject.SetActive(on);
            if (exhaust != null)
            {
                var em = exhaust.emission;
                em.enabled = on;
            }
        }

        private void UpdateEngineVisuals()
        {
            bool burning = phase == FlightPhase.Ascent || phase == FlightPhase.TransitToMoon ||
                           phase == FlightPhase.MoonArrival || phase == FlightPhase.ReturnToEarth ||
                           phase == FlightPhase.EarthArrival;
            if (flame != null && flame.gameObject.activeSelf != burning) SetBurn(burning);
            if (exhaust != null)
            {
                var em = exhaust.emission;
                if (em.enabled != burning) em.enabled = burning;
            }

            if (flame != null && burning)
            {
                float flicker = 1f + Mathf.Sin(Time.time * 38f) * .12f;
                flame.localScale = new Vector3(flameBaseScale.x * flicker, flameBaseScale.y * (2f - flicker), flameBaseScale.z * flicker);
            }
        }

        // ---------------- primitive helpers ----------------

        private GameObject Cylinder(string name, Vector3 pos, float radius, float height, Material mat, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(radius * 2f, height * .5f, radius * 2f);
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            return go;
        }

        private GameObject Sphere(string name, Vector3 pos, float diameter, Material mat, Transform parent)
        {
            return Sphere(name, pos, new Vector3(diameter, diameter, diameter), mat, parent);
        }

        private GameObject Sphere(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            return go;
        }

        private GameObject Cube(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            return go;
        }
    }

    /// <summary>
    /// Orbit / pan / zoom camera driven by the mouse. Input stays live during flight -
    /// the camera simply keeps following the rocket while the player orbits it.
    /// LEFT-DRAG orbit · RIGHT-DRAG pan · SCROLL zoom · DOUBLE-CLICK recentre.
    /// Arrow keys also orbit and +/- zoom as a fallback. Uses raw mouse deltas so it
    /// never depends on Input Manager axis definitions.
    /// </summary>
    public sealed class OrbitCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 46f;
        public float minDistance = 12f;
        public float maxDistance = 200f;

        [Header("Feel")]
        public float zoomStep = .12f;
        public float orbitSensitivity = .22f;   // degrees per pixel while dragging
        public float hoverSensitivity = .16f;   // degrees per pixel on plain mouse move over the 3D view
        public float keyOrbitSpeed = 90f;       // deg/s with arrow keys
        public float maxPanDistance = 70f;

        public float pitch = 5f;
        public float yaw = 0f;

        public float homeDistance = 46f;
        public float targetDistance;
        private Vector3 panWanted;
        private Vector3 panApplied;
        private bool initialised;
        private bool orbiting;
        private bool panning;
        private float lastLeftClick = -10f;
        private Vector3 lastMouse;
        private bool loggedInput;
        private float lastDebugLog;

        private void LateUpdate()
        {
            if (target == null) return;

            if (!initialised)
            {
                homeDistance = targetDistance = distance = Mathf.Max(distance, minDistance);
                lastMouse = Input.mousePosition;
                initialised = true;
            }

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 mouse = Input.mousePosition;
            Vector3 delta = mouse - lastMouse;
            lastMouse = mouse;

            // --- zoom: scroll wheel (mouse must be over the game window for this to fire) ---
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > .01f)
            {
                float factor = Mathf.Clamp(1f - scroll * zoomStep, .55f, 1.8f);
                targetDistance = Mathf.Clamp(targetDistance * factor, minDistance, maxDistance);
            }

            // --- zoom fallback: +/- keys ---
            if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus))
                targetDistance = Mathf.Clamp(targetDistance * (1f - .8f * dt), minDistance, maxDistance);
            if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus))
                targetDistance = Mathf.Clamp(targetDistance * (1f + .8f * dt), minDistance, maxDistance);

            distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-14f * dt));
            if (Mathf.Abs(distance - targetDistance) < .002f) distance = targetDistance;

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool overUI = PointerOverUI();

            // Left button drag orbits from anywhere.
            if (Input.GetMouseButtonDown(0) && !shift)
            {
                orbiting = true;
                if (!loggedInput)
                {
                    loggedInput = true;
                    Debug.Log($"OrbitCamera: left-drag detected at {mouse}. Hold the button and drag to orbit.");
                }
            }
            if (Input.GetMouseButtonUp(0)) orbiting = false;

            // Plain mouse movement over the 3D (right) half of the screen also orbits -
            // no button required. This is the "move my mouse to change the camera" mode.
            bool hoverLook = !overUI && !orbiting && mouse.x > Screen.width * .5f;

            if (orbiting)
            {
                yaw += delta.x * orbitSensitivity;
                pitch -= delta.y * orbitSensitivity;
            }
            else if (hoverLook && (Mathf.Abs(delta.x) > .01f || Mathf.Abs(delta.y) > .01f))
            {
                yaw += delta.x * hoverSensitivity;
                pitch -= delta.y * hoverSensitivity;
            }

            if (Time.time - lastDebugLog > 1f && (orbiting || hoverLook) && (Mathf.Abs(delta.x) > .5f || Mathf.Abs(delta.y) > .5f))
            {
                lastDebugLog = Time.time;
                Debug.Log($"OrbitCamera: yaw={yaw:F1} pitch={pitch:F1} dist={distance:F1} camPos={transform.position}");
            }

            // --- keyboard orbit fallback (always available) ---
            if (Input.GetKey(KeyCode.LeftArrow)) yaw -= keyOrbitSpeed * dt;
            if (Input.GetKey(KeyCode.RightArrow)) yaw += keyOrbitSpeed * dt;
            if (Input.GetKey(KeyCode.UpArrow)) pitch += keyOrbitSpeed * dt;
            if (Input.GetKey(KeyCode.DownArrow)) pitch -= keyOrbitSpeed * dt;

            // --- RIGHT/MIDDLE-DRAG pans ---
            panning = Input.GetMouseButton(1) || Input.GetMouseButton(2) || (shift && Input.GetMouseButton(0));
            if (panning)
            {
                float worldPerPixel = WorldUnitsPerPixel();
                panWanted -= (transform.right * delta.x + transform.up * delta.y) * worldPerPixel;
                panWanted = Vector3.ClampMagnitude(panWanted, maxPanDistance);
            }

            // --- DOUBLE-CLICK recentres (skipped when clicking a UI button) ---
            if (Input.GetMouseButtonDown(0) && !shift && !overUI)
            {
                float now = Time.unscaledTime;
                if (now - lastLeftClick < .28f) ResetView();
                lastLeftClick = now;
            }

            panApplied = Vector3.Lerp(panApplied, panWanted, 1f - Mathf.Exp(-20f * dt));

            pitch = Mathf.Clamp(pitch, -20f, 85f);
            var pivot = target.position + panApplied;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = pivot + rot * new Vector3(0f, 0f, -distance);
            transform.LookAt(pivot);
        }

        public void ResetView()
        {
            pitch = 5f;
            yaw = 0f;
            panWanted = Vector3.zero;
            panApplied = Vector3.zero;
            targetDistance = homeDistance;
            distance = homeDistance;
        }

        public void ClearPan()
        {
            panWanted = Vector3.zero;
            panApplied = Vector3.zero;
        }

        public void SetHomeDistance(float value)
        {
            homeDistance = targetDistance = distance = Mathf.Clamp(value, minDistance, maxDistance);
        }

        private float WorldUnitsPerPixel()
        {
            var cam = GetComponent<Camera>();
            if (cam == null) return distance * .0019f;
            float halfFov = cam.fieldOfView * .5f * Mathf.Deg2Rad;
            return 2f * distance * Mathf.Tan(halfFov) / Mathf.Max(Screen.height, 1);
        }

        private static bool PointerOverUI()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }
    }
}
