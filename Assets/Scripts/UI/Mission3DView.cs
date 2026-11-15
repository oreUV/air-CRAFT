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

        public enum FlightPhase { PreLaunch, Countdown, Ignition, Ascent, TransitToMoon, MoonArrival, MoonStay, ReturnToEarth, EarthArrival, Complete }

        private Transform rocketRoot;
        private Transform focus;
        private OrbitCamera cameraRig;
        private Camera rigCamera;
        private float nextCameraAudit;
        private Vector3 flameBaseScale = Vector3.one;
        private Transform flame;
        private ParticleSystem exhaust;
        private Vector3 focusHome = new Vector3(10f, PadTop + RocketHeight * .5f, 0f);
        private Material white, metal, dark, gold, blue, grey, red, pad, flag, earthMat, moonMat, flameMat, exhaustMat, cloudMat, cirrusMat;

        // ---------------- world layout (all fixed; the rocket travels through it) ----------------
        private static readonly Vector3 EarthCenter = new Vector3(10f, -300f, 0f);
        private const float EarthR = 300f;        // surface at y = 0 under the pad
        private const float CloudShellR = 322f;   // low cloud deck
        private const float CirrusShellR = 344f;  // high thin deck
        private static readonly Vector3 MoonCenter = new Vector3(10f, 260f, 2900f);
        private const float MoonR = 240f;

        // Launch Complex 39A, Kennedy Space Center - the pad Apollo 11 left from
        // (28.5729 N, 80.6490 W). The Earth texture is mapped so this exact point
        // sits under the vehicle.
        private const float SiteLatitude = 28.5729f;
        private const float SiteLongitude = -80.6490f;

        // A Saturn V (110 m) on a 300 unit planet would be 0.005 units tall, so the
        // vehicle and its pad are exaggerated - but kept small enough that the planet
        // reads as a planet. Model-space art is built at 28 units and scaled by this.
        private const float RocketModelHeight = 28f;
        private const float RocketHeight = 6f;
        private const float VehicleScale = RocketHeight / RocketModelHeight;
        private const float TableHeight = 3f;                      // model space
        private const float PadTop = TableHeight * VehicleScale;    // world space

        private Transform earthSphere;
        private Transform cloudLayer;
        private Transform cirrusLayer;
        private Transform moonSphere;
        private GameObject starfieldGO;
        private Material starMat;
        private Texture2D earthTex;
        private Texture2D cloudTex;
        private ParticleSystem padSmoke;
        private Vector3 rocketPadHome = new Vector3(10f, PadTop, 0f);
        private const float CountdownSeconds = 4f;
        private const float IgnitionSeconds = 2.2f;

        // ---------------- flight state ----------------
        private FlightPhase phase = FlightPhase.PreLaunch;
        private float phaseElapsed;
        private Vector3 flightDir = Vector3.up;
        private float flightSpeed;

        private static readonly Color surfaceSky = new Color(.45f, .68f, .92f);
        private static readonly Color spaceSky = new Color(.012f, .014f, .025f);

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
        }

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

        // ---------------- status for the HUD ----------------

        public bool InFlight => phase != FlightPhase.PreLaunch && phase != FlightPhase.Complete;
        public FlightPhase CurrentPhase => phase;

        public float AltitudeUnits
        {
            get
            {
                if (rocketRoot == null) return 0f;
                return Mathf.Max(0f, (rocketRoot.position - EarthCenter).magnitude - EarthR - PadTop);
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
                    case FlightPhase.Countdown:
                        return $"COUNTDOWN  T-{Mathf.Max(0f, CountdownSeconds - phaseElapsed):F0}s  -  ALL SYSTEMS GO";
                    case FlightPhase.Ignition: return "IGNITION  -  HOLD-DOWN CLAMPS RELEASING";
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
            SetTransparent(cloudMat, new Color(1f, 1f, 1f, .55f), 3000);

            // High, thin deck: the same cloud field tiled twice so it reads as a separate
            // layer instead of a copy of the deck below.
            var cirrusTex = cloudTex != null ? cloudTex : TextureGenerator.Clouds();
            cirrusMat = Mat("Cirrus", cirrusTex, tint: new Color(.93f, .96f, 1f), 0f, .1f, null);
            cirrusMat.mainTextureScale = new Vector2(1.9f, 1f);
            SetTransparent(cirrusMat, new Color(.93f, .96f, 1f, .22f), 3001);

            moonMat = Mat("Moon", TextureGenerator.Moon(), tint: Color.white, .02f, .15f);

            flameMat = Mat("Flame", TextureGenerator.SoftDot(), tint: new Color(1f, .55f, .1f), 0f, .05f, new Color(1f, .4f, .05f) * 2.2f);
            exhaustMat = Mat("Exhaust", TextureGenerator.SoftDot(), tint: new Color(1f, .7f, .3f), 0f, .05f);
        }

        /// <summary>
        /// Turns the second Earth map into a cloud coverage texture. Clouds are bright and
        /// colourless, so luminance gates them and saturation removes coloured land: that
        /// works whether the source is a cloud-only map or a second full-colour globe.
        /// </summary>
        private static Texture2D MakeCloudsFromMap(Texture2D source)
        {
            var tex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            var src = source.GetPixels32();
            var dst = new Color32[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                float r = src[i].r / 255f, g = src[i].g / 255f, b = src[i].b / 255f;
                float lum = (r + g + b) / 3f;
                float maxc = Mathf.Max(r, Mathf.Max(g, b));
                float minc = Mathf.Min(r, Mathf.Min(g, b));
                float sat = maxc > 1e-3f ? (maxc - minc) / maxc : 0f;

                float bright = Mathf.Clamp01((lum - .42f) / .40f);   // only bright pixels are cloud
                float neutral = Mathf.Clamp01(1f - sat * 2.5f);      // coloured land is not cloud
                float coverage = bright * neutral;
                dst[i] = new Color32(255, 255, 255, (byte)(coverage * 255f));
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
            // shrinking planet and thinning sky all come from real geometry. The mesh
            // is UV-mapped so the point under the pad is LC-39A on the real Earth map.
            earthSphere = Planet("Earth", EarthCenter, EarthR, earthMat).transform;

            // Cloud decks the vehicle punches through on the way up.
            cloudLayer = Planet("Low cloud deck", EarthCenter, CloudShellR, cloudMat).transform;
            if (cloudLayer.TryGetComponent<MeshRenderer>(out var cr) && cr.material != null)
            {
                var c = cr.material.color;
                c.a = .55f;
                cr.material.color = c;
            }

            cirrusLayer = Planet("High cirrus deck", EarthCenter, CirrusShellR, cirrusMat).transform;
            if (cirrusLayer.TryGetComponent<MeshRenderer>(out var cir) && cir.material != null)
            {
                var c = cir.material.color;
                c.a = .22f;
                cir.material.color = c;
            }

            BuildAtmosphere();
            BuildLaunchComplex();

            // Moon: fixed far ahead along the flight path.
            moonSphere = Sphere("Moon", MoonCenter, MoonR * 2f, moonMat, null).transform;

            BuildPadSmoke();

            // Distance haze so the ground blends into the sky at the horizon.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = surfaceSky;
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 170f;

            BuildStars();

            if (Camera.main != null) Camera.main.backgroundColor = surfaceSky;
        }

        // ---------------- planet geometry ----------------

        /// <summary>
        /// Creates a sphere whose equirectangular UVs put (lat, lon) at its local +Y -
        /// the point the launch pad stands on. Unity's primitive sphere has an unknown
        /// UV convention, so the mesh is generated here where the mapping is explicit.
        /// </summary>
        private GameObject Planet(string name, Vector3 center, float radius, Material mat)
        {
            var go = new GameObject(name);
            go.transform.position = center;
            DontDestroyOnLoad(go);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = GlobeMesh(radius, 96, 64, SiteLatitude, SiteLongitude);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        private static Mesh GlobeMesh(float radius, int lonSeg, int latSeg, float siteLat, float siteLon)
        {
            float lat0 = siteLat * Mathf.Deg2Rad;

            // Geographic frame with the site at +Y. North is +X and east is +Z, so the
            // vehicle looks out over the Atlantic along the flight path to the Moon.
            Vector3 site = Vector3.up;
            Vector3 pole = (site * Mathf.Sin(lat0) + Vector3.right * Mathf.Cos(lat0)).normalized;
            Vector3 meridian = (site - pole * Mathf.Sin(lat0)).normalized;  // toward the equator
            Vector3 east = Vector3.forward;

            var verts = new List<Vector3>((lonSeg + 1) * (latSeg + 1));
            var uvs = new List<Vector2>(verts.Capacity);
            var normals = new List<Vector3>(verts.Capacity);
            var tris = new List<int>(lonSeg * latSeg * 6);

            // The texture wraps at the antimeridian; keep that seam 180 degrees away from
            // the pad (over the Pacific) and duplicate the column so nothing smears.
            float seam = -180f - siteLon;

            for (int y = 0; y <= latSeg; y++)
            {
                float v = y / (float)latSeg;
                float lat = (v - .5f) * Mathf.PI;
                float cl = Mathf.Cos(lat), sl = Mathf.Sin(lat);
                for (int x = 0; x <= lonSeg; x++)
                {
                    float u = x / (float)lonSeg;
                    float d = (seam + 360f * u) * Mathf.Deg2Rad;
                    Vector3 ring = meridian * Mathf.Cos(d) + east * Mathf.Sin(d);
                    var dir = (pole * sl + ring * cl).normalized;
                    verts.Add(dir * radius);
                    normals.Add(dir);
                    uvs.Add(new Vector2(u, v));
                }
            }

            for (int y = 0; y < latSeg; y++)
            {
                for (int x = 0; x < lonSeg; x++)
                {
                    int a = y * (lonSeg + 1) + x;
                    int b = a + 1;
                    int c = a + lonSeg + 1;
                    int d = c + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            }

            var mesh = new Mesh { name = "Globe", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            // Match the winding of a built-in sphere so the planet is never inside-out,
            // whatever the renderer's front-face convention is.
            if (Mathf.Sign(SignedVolume(mesh)) != ReferenceWinding())
            {
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int t = tris[i];
                    tris[i] = tris[i + 1];
                    tris[i + 1] = t;
                }
                mesh.SetTriangles(tris, 0);
            }
            return mesh;
        }

        private static float SignedVolume(Mesh mesh)
        {
            var v = mesh.vertices;
            var t = mesh.triangles;
            float sum = 0f;
            for (int i = 0; i + 2 < t.Length; i += 3)
                sum += Vector3.Dot(v[t[i]], Vector3.Cross(v[t[i + 1]], v[t[i + 2]]));
            return sum;
        }

        private static float referenceWinding;

        private static float ReferenceWinding()
        {
            if (Mathf.Abs(referenceWinding) > 1e-6f) return referenceWinding;
            var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            probe.SetActive(false);
            var mesh = probe.GetComponent<MeshFilter>().sharedMesh;
            referenceWinding = mesh != null ? Mathf.Sign(SignedVolume(mesh)) : 1f;
            Destroy(probe);
            return referenceWinding;
        }

        /// <summary>
        /// Haze shells stacked above the surface. From the pad the camera sits inside all
        /// of them and they tint the sky; climbing out, each one peels away in turn and
        /// the outer shells leave a blue rim around the planet seen from space.
        /// </summary>
        private void BuildAtmosphere()
        {
            AddShell("Atmosphere - troposphere", EarthR + 2.5f, new Color(.70f, .84f, 1f, .10f), 2990);
            AddShell("Atmosphere - stratosphere", EarthR + 30f, new Color(.46f, .68f, 1f, .075f), 2991);
            AddShell("Atmosphere - exosphere", EarthR + 78f, new Color(.30f, .50f, .95f, .055f), 2992);
        }

        private void AddShell(string name, float radius, Color tint, int queue)
        {
            // Unlit so the haze keeps a constant tint no matter which way the sun faces,
            // and so nothing is shaded across the inside of the shell.
            Shader s = Shader.Find("Unlit/Transparent");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Standard");
            var mat = new Material(s) { name = name };
            SetTransparent(mat, tint, queue);
            Planet(name, EarthCenter, radius, mat);
        }

        private static void SetTransparent(Material m, Color tint, int queue)
        {
            m.SetOverrideTag("RenderType", "Transparent");
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 3f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", 1f);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", 10f);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(tint.r, tint.g, tint.b) * .25f);
            }
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = queue;
        }

        /// <summary>
        /// Apollo launch complex built in model space (28-unit vehicle) and scaled down as
        /// one object, so pad and vehicle stay in proportion at any vehicle scale.
        /// </summary>
        private void BuildLaunchComplex()
        {
            var complex = new GameObject("Launch Complex").transform;
            complex.position = new Vector3(10f, 0f, 0f);
            complex.localScale = Vector3.one * VehicleScale;
            DontDestroyOnLoad(complex.gameObject);

            // Flat concrete apron + pad deck at ground level (top exactly at y = 0).
            Cube("Concrete apron", new Vector3(0f, -.4f, 0f), new Vector3(36f, .8f, 36f), pad, complex);
            Cylinder("Pad deck", new Vector3(0f, -.22f, 0f), 13f, .45f, grey, complex);
            Cube("Flame trench", new Vector3(0f, -.28f, 0f), new Vector3(21f, .55f, 4.6f), dark, complex);

            // Raised launch table: the vehicle stands on this ring, so the engines vent
            // down through the open centre and the flame stays visible above the ground
            // exactly as it does on a real launch mount.
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a) * 3.4f, TableHeight - .3f, Mathf.Sin(a) * 3.4f);
                var seg = Cube("Launch table", p, new Vector3(3.1f, .6f, 1.4f), metal, complex);
                seg.transform.localRotation = Quaternion.Euler(0f, -i * 45f, 0f);
            }
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a) * 3.65f, TableHeight * .5f, Mathf.Sin(a) * 3.65f);
                Cube("Table leg", p, new Vector3(.9f, TableHeight, .9f), dark, complex);
            }

            // Hold-down clamps gripping the base of the vehicle.
            for (int i = 0; i < 4; i++)
            {
                float ang = (i * 90f + 45f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(ang) * 3.5f, TableHeight + .65f, Mathf.Sin(ang) * 3.5f);
                Cube("Hold-down clamp", p, new Vector3(1.5f, 1.3f, 1.5f), metal, complex);
            }

            // Service tower beside the rocket.
            var tower = new GameObject("Service Tower").transform;
            tower.SetParent(complex, false);
            const float bx = 9.5f, bz = 0f, half = 2.3f, height = 44f;

            var corners = new[]
            {
                new Vector3(-half, 0f, -half), new Vector3(half, 0f, -half),
                new Vector3(half, 0f, half), new Vector3(-half, 0f, half)
            };
            foreach (var c in corners)
                Cube("Tower leg", new Vector3(bx + c.x, height * .5f, bz + c.z), new Vector3(.5f, height, .5f), red, tower);

            for (float y = 3f; y <= height; y += 3.7f)
            {
                Cube("Tower beam", new Vector3(bx, y, bz - half), new Vector3(half * 2f, .24f, .24f), grey, tower);
                Cube("Tower beam", new Vector3(bx, y, bz + half), new Vector3(half * 2f, .24f, .24f), grey, tower);
                Cube("Tower beam", new Vector3(bx - half, y, bz), new Vector3(.24f, .24f, half * 2f), grey, tower);
                Cube("Tower beam", new Vector3(bx + half, y, bz), new Vector3(.24f, .24f, half * 2f), grey, tower);
            }

            Cube("Tower platform", new Vector3(bx, height, bz), new Vector3(6f, .5f, 6f), metal, tower);
            Cube("Tower mast", new Vector3(bx, height + 4.6f, bz), new Vector3(.35f, 9f, .35f), metal, tower);
            Cube("Lightning rod", new Vector3(bx, height + 9.4f, bz), new Vector3(.12f, 1.4f, .12f), red, tower);

            // Umbilical arms reaching out toward the rocket.
            Cube("Umbilical arm", new Vector3(bx - half - 2.6f, 22f, bz), new Vector3(6f, .55f, 1f), grey, tower);
            Cube("Umbilical arm", new Vector3(bx - half - 2.6f, 11f, bz), new Vector3(6f, .55f, 1f), grey, tower);

            // Support buildings on the apron (kept inside the concrete edge so they do
            // not hang off the curved ground).
            Cube("Blockhouse", new Vector3(-13.5f, 1.4f, 10f), new Vector3(9f, 3.4f, 7f), grey, complex);
            Cube("Water tower", new Vector3(14.5f, 6.1f, -9f), new Vector3(2.4f, 14f, 2.4f), metal, complex);
        }

        /// <summary>Ground smoke that billows out of the flame trench at ignition.</summary>
        private void BuildPadSmoke()
        {
            // Sized in world units to match the shrunken pad (VehicleScale).
            const float s = VehicleScale;
            var go = new GameObject("Pad Smoke");
            go.transform.position = new Vector3(10f, .3f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f * s, 8f * s);
            main.startSize = new ParticleSystem.MinMaxCurve(5f * s, 13f * s);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(.92f, .92f, .94f, .30f), new Color(.75f, .74f, .73f, .14f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 900;
            main.gravityModifier = -.015f;

            var emission = ps.emission;
            emission.enabled = false;
            emission.rateOverTime = 90f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 7f * s;
            shape.rotation = new Vector3(-90f, 0f, 0f); // emit upward, then spread outward

            // Radial velocity turns the rising column into the ground-hugging cloud
            // that rolls away from the flame trench on a real launch.
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.radial = new ParticleSystem.MinMaxCurve(7f * s, 17f * s);

            Shader psShader = Shader.Find("Particles/Standard Unlit");
            if (psShader == null) psShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (psShader == null) psShader = Shader.Find("Sprites/Default");
            var psMat = new Material(psShader != null ? psShader : Shader.Find("Unlit/Color"));
            psMat.mainTexture = TextureGenerator.SoftDot();
            if (psMat.HasProperty("_Color")) psMat.SetColor("_Color", new Color(.95f, .95f, .96f, .55f));
            var psRenderer = go.GetComponent<ParticleSystemRenderer>();
            psRenderer.material = psMat;
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            psRenderer.sortingFudge = 4f;

            padSmoke = ps;
            DontDestroyOnLoad(go);
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
            orbit.minDistance = 7f;
            orbit.maxDistance = 500f;
            orbit.homeDistance = 18f;
            orbit.distance = 18f;
            orbit.targetDistance = 18f;
            orbit.pitch = -8f;
            orbit.yaw = 0f;
            orbit.maxPanDistance = 24f;
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

            // rocketRoot lives in world space and is what the flight state machine moves.
            // All artwork is authored at RocketModelHeight (28 units) and built under a
            // "visual" child scaled by VehicleScale, so the whole stack - model, plume
            // and vapour trail - shrinks to RocketHeight without touching the flight code.
            var root = new GameObject("Rocket").transform;
            root.position = rocketPadHome;
            root.rotation = Quaternion.identity;
            rocketRoot = root;

            var visual = new GameObject("Vehicle").transform;
            visual.SetParent(root, false);
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one * VehicleScale;

            var saturnV = PlaceSaturnVFbx(visual, Vector3.zero, RocketModelHeight);

            if (saturnV == null)
            {
                Cylinder("Stage 1", new Vector3(0, Stage1Base + Stage1H * .5f, 0), Stage1D * .5f, Stage1H, white, visual);
                Cylinder("Stage 2", new Vector3(0, Stage2Base + Stage2H * .5f, 0), Stage2D * .5f, Stage2H, metal, visual);
                Cylinder("Service module", new Vector3(0, ServiceBase + ServiceH * .5f, 0), ServiceD * .5f, ServiceH, metal, visual);
                Sphere("Command module", new Vector3(0, CapsuleBase + CapsuleH * .45f, 0), CapsuleD, white, visual);
                Cylinder("Escape tower", new Vector3(0, StackTop + .6f, 0), .11f, 1.2f, dark, visual);
                Sphere("Tower tip", new Vector3(0, StackTop + 1.25f, 0), .24f, dark, visual);
            }

            var flameGO = Cylinder("Engine flame", new Vector3(0, -3.4f, 0), 1.2f, 6f, flameMat, visual);
            flame = flameGO.transform;
            flameBaseScale = flame.localScale;
            flame.gameObject.SetActive(false);

            BuildExhaust(visual, -3.4f);

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
            ArmPadSmoke(false);
            if (cameraRig != null) cameraRig.shake = 0f;
        }

        /// <summary>
        /// Vapour/exhaust trail under the engines. Particles are simulated in world space
        /// (so the plume falls behind the climbing vehicle) and sized in world units, i.e.
        /// already scaled for the shrunken vehicle.
        /// </summary>
        private void BuildExhaust(Transform root, float y)
        {
            var go = new GameObject("Exhaust");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.5f, .95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.2f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(.22f, .62f);
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

            // Park the vehicle exactly on the pad before the clock starts.
            rocketRoot.position = rocketPadHome;
            rocketRoot.rotation = Quaternion.identity;

            // Hold a low, cinematic pad framing; the first mouse or key input hands
            // control straight back to the player.
            if (cameraRig != null)
            {
                cameraRig.userControlled = false;
                cameraRig.cinematicPitch = 4f;
                cameraRig.cinematicYaw = 55f;
                cameraRig.cinematicDistance = 20f;
            }

            phase = FlightPhase.Countdown;
            phaseElapsed = 0f;
            flightSpeed = 0f;
            flightDir = Vector3.up;
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
            ArmPadSmoke(false);
            if (cameraRig != null)
            {
                cameraRig.shake = 0f;
                cameraRig.userControlled = true;
            }
            MissionComplete?.Invoke();
        }

        private float MoonStayClock;

        private void Update()
        {
            EnforceSingleCamera();
            if (rocketRoot == null) return;

            // Camera focus chases the rocket the whole time so the player can orbit freely.
            // The chase is deliberately tight: at ascent speed a soft follow would let the
            // vehicle drift right out of frame.
            Vector3 focusTarget = phase == FlightPhase.PreLaunch
                ? focusHome
                : rocketRoot.position + Vector3.up * (RocketHeight * .5f);
            float k = 1f - Mathf.Exp(-18f * Time.deltaTime);
            focus.position = Vector3.Lerp(focus.position, focusTarget, k);

            if (!InFlight) return;

            phaseElapsed += Time.deltaTime;
            StepFlight();

            // --- environment driven by real altitude ---
            float alt = AltitudeUnits;

            // The sky is a multi-stop altitude gradient, not a single blue->black fade:
            // ground haze thickens it, the troposphere is deep blue, the stratosphere goes
            // navy and the last shell peels off into black. The stops are placed where the
            // haze shells sit, so the sky thins exactly as the vehicle leaves them.
            float skyT = Mathf.Clamp01(alt / 170f);
            Color sky = SkyColorAt(alt);
            if (Camera.main != null) Camera.main.backgroundColor = sky;

            // Stars fade in once the sky is dark enough for them to show.
            if (starMat != null && starMat.HasProperty("_Color"))
            {
                Color c = starMat.color;
                c.a = Mathf.Clamp01((alt - 55f) / 95f);
                starMat.color = c;
            }

            // Each cloud deck thins as the vehicle climbs through and above it.
            if (cloudLayer != null && cloudLayer.TryGetComponent<MeshRenderer>(out var cloudRend) && cloudRend.material != null)
            {
                Color c = cloudRend.material.color;
                c.a = .55f * (1f - Mathf.Clamp01((alt - 12f) / 22f));
                cloudRend.material.color = c;
            }
            if (cirrusLayer != null && cirrusLayer.TryGetComponent<MeshRenderer>(out var cirRend) && cirRend.material != null)
            {
                Color c = cirRend.material.color;
                c.a = .22f * (1f - Mathf.Clamp01((alt - 34f) / 26f));
                cirRend.material.color = c;
            }

            UpdateEngineVisuals();

            // Ground haze follows the sky colour and opens up as the air thins, so the
            // pad looks properly hazy from the ground and crisp from altitude.
            if (RenderSettings.fog)
            {
                RenderSettings.fogColor = sky;
                RenderSettings.fogStartDistance = Mathf.Lerp(22f, 900f, skyT);
                RenderSettings.fogEndDistance = Mathf.Lerp(165f, 5200f, skyT);
            }

            // Launch vibration: strongest at ignition, fading as the vehicle clears the pad.
            if (cameraRig != null)
            {
                float shake = 0f;
                if (phase == FlightPhase.Ignition)
                    shake = .07f * Mathf.Clamp01(phaseElapsed / IgnitionSeconds);
                else if (phase == FlightPhase.Ascent)
                    shake = .07f * Mathf.Clamp01(1f - alt / 40f);
                cameraRig.shake = Mathf.Max(shake, cameraRig.shake);
            }
        }

        /// <summary>
        /// Sky colour for an altitude above the launch site. Stops are chosen to match the
        /// atmosphere shells: blue haze at the surface, deep blue in the troposphere, navy
        /// through the stratosphere, then black.
        /// </summary>
        private static Color SkyColorAt(float alt)
        {
            Color haze = new Color(.63f, .79f, .95f);
            Color low = new Color(.40f, .62f, .92f);
            Color mid = new Color(.16f, .34f, .74f);
            Color high = new Color(.05f, .12f, .34f);
            Color edge = new Color(.015f, .025f, .075f);

            if (alt <= 0f) return haze;
            if (alt < 9f) return Color.Lerp(haze, low, alt / 9f);
            if (alt < 30f) return Color.Lerp(low, mid, (alt - 9f) / 21f);
            if (alt < 70f) return Color.Lerp(mid, high, (alt - 30f) / 40f);
            if (alt < 150f) return Color.Lerp(high, edge, (alt - 70f) / 80f);
            return Color.Lerp(edge, spaceSky, Mathf.Clamp01((alt - 150f) / 120f));
        }

        private void StepFlight()
        {
            var pos = rocketRoot.position;
            float dt = Time.deltaTime;
            Vector3 toMoon = (MoonCenter - EarthCenter).normalized;
            Vector3 upNow = (pos - EarthCenter).normalized;
            float alt = (pos - EarthCenter).magnitude - EarthR - PadTop;

            switch (phase)
            {
                case FlightPhase.Countdown:
                {
                    // Vehicle restrains on the pad while the launch clock runs down.
                    flightDir = Vector3.up;
                    pos = rocketPadHome;
                    if (rocketRoot != null) rocketRoot.rotation = Quaternion.identity;
                    if (phaseElapsed >= CountdownSeconds)
                    {
                        phase = FlightPhase.Ignition;
                        phaseElapsed = 0f;
                        ArmPadSmoke(true);
                    }
                    break;
                }

                case FlightPhase.Ignition:
                {
                    float t = Mathf.Clamp01(phaseElapsed / IgnitionSeconds);

                    // The stack shivers against the hold-down clamps as thrust builds.
                    float strainX = Mathf.Sin(Time.time * 61f) * .045f * t;
                    float strainZ = Mathf.Cos(Time.time * 47f) * .032f * t;

                    // Clamps release at ~55% of spool-up; the vehicle then eases upward
                    // before the Ascent phase takes over - a liftoff, not a jump.
                    float rise = Mathf.Clamp01((t - .55f) / .45f);
                    rise = rise * rise * 1.2f;

                    pos = rocketPadHome + new Vector3(strainX, rise, strainZ);
                    flightDir = Vector3.up;
                    if (rocketRoot != null)
                        rocketRoot.rotation = Quaternion.Euler(strainZ * 90f, 0f, -strainX * 90f);

                    if (t >= 1f)
                    {
                        phase = FlightPhase.Ascent;
                        phaseElapsed = 0f;
                        flightSpeed = 1.4f;
                    }
                    break;
                }

                case FlightPhase.Ascent:
                {
                    // Continuous acceleration off the pad; pitch over toward the Moon
                    // between turnStartAlt and turnFullAlt (classic gravity turn).
                    // Thrust builds over the first seconds, so the vehicle starts slowly
                    // and only then gains real speed - a liftoff, not a teleport.
                    const float turnStartAlt = 40f, turnFullAlt = 220f;
                    float climb = Mathf.Clamp01(alt / turnFullAlt);
                    float speedCap = Mathf.Lerp(10f, 95f, climb * climb);
                    flightSpeed = Mathf.Min(flightSpeed + 6.5f * dt, speedCap);

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
                    float altHome = (pos - EarthCenter).magnitude - EarthR - PadTop;
                    flightSpeed = Mathf.Max(flightSpeed - 90f * dt, 10f);
                    Vector3 descend = (EarthCenter - pos).normalized;
                    pos += descend * (flightSpeed * dt);

                    if (altHome <= .5f)
                    {
                        // Touch down on the launch table.
                        Vector3 radial = (pos - EarthCenter).normalized;
                        pos = EarthCenter + radial * (EarthR + PadTop + .01f);
                        pos.x = 10f; pos.z = 0f;
                        rocketRoot.position = pos;
                        rocketRoot.rotation = Quaternion.LookRotation(Vector3.forward, radial);
                        phase = FlightPhase.Complete;
                        SetBurn(false);
                        ArmPadSmoke(true);
                        MissionComplete?.Invoke();
                        return;
                    }
                    break;
                }
            }

            rocketRoot.position = pos;

            // Nose follows the flight direction (the pad phases set their own orientation).
            if (phase != FlightPhase.MoonStay && phase != FlightPhase.EarthArrival &&
                phase != FlightPhase.Countdown && phase != FlightPhase.Ignition)
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
            bool spooling = phase == FlightPhase.Ignition;
            bool burning = spooling || phase == FlightPhase.Ascent || phase == FlightPhase.TransitToMoon ||
                           phase == FlightPhase.MoonArrival || phase == FlightPhase.ReturnToEarth ||
                           phase == FlightPhase.EarthArrival;
            if (flame != null && flame.gameObject.activeSelf != burning) SetBurn(burning);

            // Fire builds from a stub to full thrust while the engines spool up.
            float fire = 1f;
            if (spooling) fire = Mathf.Clamp01(phaseElapsed / (IgnitionSeconds * .7f));

            if (exhaust != null)
            {
                var em = exhaust.emission;
                if (em.enabled != burning) em.enabled = burning;
                if (burning) em.rateOverTime = 320f * fire;
            }

            if (flame != null && burning)
            {
                float flicker = 1f + Mathf.Sin(Time.time * 38f) * .12f;
                flame.localScale = new Vector3(
                    flameBaseScale.x * flicker * fire,
                    flameBaseScale.y * (2f - flicker) * (.3f + .7f * fire),
                    flameBaseScale.z * flicker * fire);
            }

            // Pad smoke billows from the flame trench at ignition and thins as the
            // vehicle climbs away.
            if (padSmoke != null)
            {
                bool billowing = spooling || (phase == FlightPhase.Ascent && AltitudeUnits < 28f);
                var pem = padSmoke.emission;
                if (pem.enabled != billowing) pem.enabled = billowing;
            }
        }

        /// <summary>Switches the ground smoke cloud at the flame trench on or off.</summary>
        private void ArmPadSmoke(bool on)
        {
            if (padSmoke == null) return;
            var em = padSmoke.emission;
            em.enabled = on;
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
    /// Orbit / pan / zoom camera. Plain mouse movement never moves the camera - the view
    /// only changes while a button is held:
    /// RIGHT-DRAG orbit - MIDDLE-DRAG pan - SCROLL zoom - LEFT DOUBLE-CLICK recentre.
    /// Arrow keys orbit and +/- zoom as a fallback. Uses raw mouse deltas so it never
    /// depends on Input Manager axis definitions.
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

        // Cinematic framing used while the launch sequence plays. The first deliberate
        // mouse or key input hands full control back to the player.
        public bool userControlled = true;
        public float cinematicPitch = 5f;
        public float cinematicYaw = 0f;
        public float cinematicDistance = 46f;

        // Engine vibration, in world units. Set by the mission each frame, decays here.
        public float shake;

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

            // The launch sequence drives the camera until the player takes over.
            if (!userControlled && AnyManualInput()) userControlled = true;
            if (!userControlled)
            {
                float k0 = 1f - Mathf.Exp(-1.5f * dt);
                pitch = Mathf.LerpAngle(pitch, cinematicPitch, k0);
                yaw = Mathf.LerpAngle(yaw, cinematicYaw, k0);
                targetDistance = Mathf.Lerp(targetDistance, cinematicDistance, k0);
                distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-14f * dt));
            }

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

            bool overUI = PointerOverUI();

            // --- orbit: RIGHT mouse button drag ---
            // Plain mouse movement never moves the camera; the view only changes while a
            // button is held (or a key is down), which is what the player expects.
            orbiting = Input.GetMouseButton(1) && !overUI;
            if (orbiting)
            {
                yaw += delta.x * orbitSensitivity;
                pitch -= delta.y * orbitSensitivity;
            }

            // --- keyboard orbit fallback (always available) ---
            if (Input.GetKey(KeyCode.LeftArrow)) yaw -= keyOrbitSpeed * dt;
            if (Input.GetKey(KeyCode.RightArrow)) yaw += keyOrbitSpeed * dt;
            if (Input.GetKey(KeyCode.UpArrow)) pitch += keyOrbitSpeed * dt;
            if (Input.GetKey(KeyCode.DownArrow)) pitch -= keyOrbitSpeed * dt;

            // --- pan: MIDDLE mouse button drag ---
            panning = Input.GetMouseButton(2);
            if (panning)
            {
                float worldPerPixel = WorldUnitsPerPixel();
                panWanted -= (transform.right * delta.x + transform.up * delta.y) * worldPerPixel;
                panWanted = Vector3.ClampMagnitude(panWanted, maxPanDistance);
            }

            // --- double-click recentres (left button; skipped over UI) ---
            if (Input.GetMouseButtonDown(0) && !overUI)
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

            // Engine/launch vibration.
            if (shake > .0005f)
            {
                transform.position += new Vector3(
                    UnityEngine.Random.Range(-shake, shake),
                    UnityEngine.Random.Range(-shake, shake),
                    UnityEngine.Random.Range(-shake, shake));
                shake *= .88f;
            }
            else shake = 0f;
        }

        /// <summary>True when the player is actively asking for camera control.</summary>
        private static bool AnyManualInput()
        {
            return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2) ||
                   Input.GetMouseButton(1) || Input.GetMouseButton(2) ||
                   Mathf.Abs(Input.mouseScrollDelta.y) > .01f ||
                   Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow) ||
                   Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||
                   Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.Minus) ||
                   Input.GetKey(KeyCode.KeypadPlus) || Input.GetKey(KeyCode.KeypadMinus);
        }

        public void ResetView()
        {
            userControlled = true;
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
