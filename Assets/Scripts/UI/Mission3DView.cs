using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceMissionDesign.Data;

namespace SpaceMissionDesign.UI
{
    public sealed class Mission3DView : MonoBehaviour
    {
        public static Mission3DView Instance { get; private set; }

        private Transform rocketRoot;
        private Transform focus;
        private OrbitCamera cameraRig;
        private Vector3 flameBaseScale = Vector3.one;
        private Transform flame;
        private ParticleSystem exhaust;
        private Vector3 focusHome = new Vector3(10f, 12f, 0f);
        private Material white, metal, dark, gold, blue, grey, red, pad, flag, earthMat, moonMat, flameMat, exhaustMat, cloudMat;

        // Earth departure / Moon round-trip visuals.
        private Transform earthSphere;
        private Transform cloudLayer;
        private Transform moonSphere;
        private GameObject starfieldGO;
        private Texture2D earthTex;
        private Texture2D cloudTex;
        private float missionProgress;            // 0..1 across the full Earth->Moon->Earth loop
        private MissionPhase phase = MissionPhase.PreLaunch;
        private float phaseTime;
        private const float PhaseDuration = 10f;  // seconds per phase

        private enum MissionPhase { PreLaunch, EarthDeparture, TransitToMoon, MoonArrival, MoonStay, ReturnToEarth, EarthArrival }

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
        }

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

            // --- Earth: planet directly below the pad; surface at y=0 ---
            // Camera on the surface looks up at the rocket; Earth fills the lower view.
            earthSphere = Sphere("Earth", new Vector3(10f, -60f, 0f), 60f, earthMat, null).transform;

            // --- Cloud layer: just above Earth's surface, transparent ---
            cloudLayer = Sphere("Cloud layer", new Vector3(10f, -60f, 0f), 68f, cloudMat, null).transform;

            // --- Launch pad sits on Earth's surface ---
            Cylinder("Launch pad", new Vector3(10f, 0f, 0f), 14f, .7f, pad, null);
            Cylinder("Pad ring", new Vector3(10f, .3f, 0f), 8f, .35f, grey, null);

            // --- Moon: far ahead in the direction of flight ---
            moonSphere = Sphere("Moon", new Vector3(10f, 0f, 400f), 18f, moonMat, null).transform;

            BuildStars();

            Camera.main.backgroundColor = surfaceSky;
        }

        private void BuildStars()
        {
            starfieldGO = new GameObject("Starfield");
            starfieldGO.transform.position = new Vector3(0, 12, 0);
            var ps = starfieldGO.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startSpeed = 0f;
            main.startLifetime = 9999f;
            main.startSize = new ParticleSystem.MinMaxCurve(.6f, 1.8f);
            main.maxParticles = 1400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.SphereShell;
            shape.radius = 200f;
            shape.radiusThickness = .06f;

            Shader psShader = Shader.Find("Particles/Standard Unlit");
            if (psShader == null) psShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (psShader == null) psShader = Shader.Find("Sprites/Default");
            var psMat = new Material(psShader != null ? psShader : Shader.Find("Unlit/Color"));
            psMat.mainTexture = TextureGenerator.SoftDot();
            if (psMat.HasProperty("_Color")) psMat.SetColor("_Color", new Color(.92f, .95f, 1f, 0f));
            var renderer = starfieldGO.GetComponent<ParticleSystemRenderer>();
            renderer.material = psMat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Emit(1400);

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
            cam.nearClipPlane = .1f;
            cam.farClipPlane = 900f;

            var focusGO = new GameObject("Camera Focus");
            focusGO.transform.position = focusHome;
            DontDestroyOnLoad(focusGO);
            focus = focusGO.transform;

            var orbit = cam.GetComponent<OrbitCamera>();
            if (orbit == null) orbit = cam.gameObject.AddComponent<OrbitCamera>();
            orbit.target = focus;
            orbit.minDistance = 14f;
            orbit.maxDistance = 200f;
            orbit.homeDistance = 60f;
            orbit.distance = 24f;
            orbit.targetDistance = 24f;
            orbit.pitch = -25f;
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
            // Try the complete fbx model first.
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
                    // Stand the rocket up (longest axis = +Y).
                    var upright = AxisToUpRotation(bounds.size);
                    if (upright != Quaternion.identity)
                    {
                        go.transform.localRotation = upright;
                        if (TryLocalBounds(go, parent, out var stood)) bounds = stood;
                    }
                    // Make sure the base points down.
                    if (WideEndIsUp(go))
                    {
                        go.transform.localRotation = Quaternion.Euler(180f, 0f, 0f) * go.transform.localRotation;
                        if (TryLocalBounds(go, parent, out var flipped)) bounds = flipped;
                    }

                    // Uniform scale so the rocket is targetHeight tall.
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
            launching = false;
            phase = MissionPhase.PreLaunch;
            phaseTime = 0f;
            missionProgress = 0f;
            if (focus != null) focus.position = focusHome;

            var root = new GameObject("Rocket").transform;
            root.position = new Vector3(10f, 0f, 0f);
            rocketRoot = root;

            // --- Load the complete Saturn V.fbx rocket ---
            var saturnV = PlaceSaturnVFbx(root, new Vector3(0, 0, 0), 28f);

            // Fallback: primitive rocket if the glb is missing.
            if (saturnV == null)
            {
                Cylinder("Stage 1", new Vector3(0, Stage1Base + Stage1H * .5f, 0), Stage1D * .5f, Stage1H, white, root);
                Cylinder("Stage 2", new Vector3(0, Stage2Base + Stage2H * .5f, 0), Stage2D * .5f, Stage2H, metal, root);
                Cylinder("Service module", new Vector3(0, ServiceBase + ServiceH * .5f, 0), ServiceD * .5f, ServiceH, metal, root);
                Sphere("Command module", new Vector3(0, CapsuleBase + CapsuleH * .45f, 0), CapsuleD, white, root);
                Cylinder("Escape tower", new Vector3(0, StackTop + .6f, 0), .11f, 1.2f, dark, root);
                Sphere("Tower tip", new Vector3(0, StackTop + 1.25f, 0), .24f, dark, root);
            }

            // Engine flame (hidden until liftoff).
            var flameGO = Cylinder("Engine flame", new Vector3(0, -3.4f, 0), 1.2f, 6f, flameMat, root);
            flame = flameGO.transform;
            flameBaseScale = flame.localScale;
            flame.gameObject.SetActive(false);

            BuildExhaust(root, -3.4f);

            DontDestroyOnLoad(root.gameObject);
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

        public void PlayLaunch()
        {
            if (rocketRoot != null && !launching) StartCoroutine(LaunchRoutine());
        }

        private bool launching;

        /// <summary>
        /// Full Earth -> Moon -> Earth round trip. Each phase advances the camera,
        /// planet sizes/positions, and rocket state so the player sees Earth recede,
        /// the Moon grow, then Earth return.
        /// </summary>
        private IEnumerator LaunchRoutine()
        {
            launching = true;
            cameraRig?.ClearPan();
            if (cameraRig != null) cameraRig.departing = true;

            var root = rocketRoot;
            float t = 0f;
            float ignition = .55f;
            float launchDuration = 6f;

            float startDist = cameraRig != null ? cameraRig.distance : 24f;
            float startPitch = cameraRig != null ? cameraRig.pitch : -25f;

            if (flame != null) flame.gameObject.SetActive(true);
            if (exhaust != null)
            {
                var em = exhaust.emission;
                em.enabled = true;
            }

            // Cache initial planet state.
            Vector3 earthStart = earthSphere != null ? earthSphere.position : new Vector3(10, -70, -10);
            float earthStartScale = earthSphere != null ? earthSphere.localScale.x : 110f;
            Vector3 moonStart = moonSphere != null ? moonSphere.position : new Vector3(10, 8, 420);
            float moonStartScale = moonSphere != null ? moonSphere.localScale.x : 18f;

            while (t < launchDuration)
            {
                t += Time.deltaTime;
                if (root == null) yield break;

                float u = Mathf.Clamp01(t / launchDuration);
                float s = u * u * (3f - 2f * u); // smoothstep

                // Phase 1: Earth departure.
                phase = MissionPhase.EarthDeparture;
                phaseTime = t;

                if (flame != null)
                {
                    float flicker = 1f + Mathf.Sin(Time.time * 38f) * .12f;
                    flame.localScale = new Vector3(flameBaseScale.x * flicker, flameBaseScale.y * (2f - flicker), flameBaseScale.z * flicker);
                }

                if (t > ignition)
                {
                    float tt = t - ignition;
                    float speed = tt * tt * 4.6f + tt * 2.6f;
                    root.position += Vector3.up * (speed * Time.deltaTime);
                    root.Rotate(Vector3.up, 2f * Time.deltaTime, Space.World);
                }

                // Earth recedes below and grows (curved horizon).
                if (earthSphere != null)
                {
                    float earthY = Mathf.Lerp(-60f, -220f, s);
                    float earthScale = Mathf.Lerp(60f, 150f, s);
                    earthSphere.position = earthStart + new Vector3(0f, earthY - earthStart.y, 0f);
                    earthSphere.localScale = Vector3.one * earthScale;
                }

                // Clouds thicken as we climb, then fade in space.
                if (cloudLayer != null)
                {
                    cloudLayer.position = earthSphere != null ? earthSphere.position : moonStart;
                    float cloudAlpha = Mathf.Lerp(0f, .5f, s);
                    cloudAlpha = cloudAlpha * (1f - s); // visible mid-climb, fades in space
                    var cloudRend = cloudLayer.GetComponent<MeshRenderer>();
                    if (cloudRend != null && cloudRend.material != null)
                    {
                        Color c = cloudRend.material.color;
                        c.a = cloudAlpha;
                        cloudRend.material.color = c;
                    }
                    cloudLayer.localScale = Vector3.one * Mathf.Lerp(68f, 160f, s);
                }

                // Sky: surface blue -> space black.
                if (Camera.main != null)
                    Camera.main.backgroundColor = Color.Lerp(surfaceSky, spaceSky, s);

                // Stars fade in.
                if (starfieldGO != null)
                {
                    var ps = starfieldGO.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        var emission = ps.emission;
                        emission.enabled = s > .35f;
                        var renderer = starfieldGO.GetComponent<ParticleSystemRenderer>();
                        if (renderer != null && renderer.material != null && renderer.material.HasProperty("_Color"))
                        {
                            Color c = renderer.material.color;
                            c.a = Mathf.Lerp(0f, 1f, Mathf.Clamp01((s - .35f) / .65f));
                            renderer.material.color = c;
                        }
                    }
                }

                // Moon grows ahead.
                if (moonSphere != null)
                {
                    float moonZ = Mathf.Lerp(400f, 320f, s);
                    float moonScale = Mathf.Lerp(18f, 40f, s);
                    moonSphere.position = new Vector3(moonStart.x, moonStart.y, moonZ);
                    moonSphere.localScale = Vector3.one * moonScale;
                }

                // Camera pulls back and tilts down.
                if (cameraRig != null)
                {
                    cameraRig.distance = Mathf.Lerp(startDist, 85f, s);
                    cameraRig.targetDistance = cameraRig.distance;
                    cameraRig.pitch = Mathf.Lerp(startPitch, 15f, s);
                    cameraRig.pitch = Mathf.Clamp(cameraRig.pitch, -20f, 85f);
                    Vector3 focusTarget = root.position + new Vector3(0f, 8f, 0f);
                    focus.position = Vector3.Lerp(focus.position, focusTarget, Time.deltaTime * 2.5f);
                }

                yield return null;
            }

            // Transition into transit phases.
            phase = MissionPhase.TransitToMoon;

            if (flame != null) flame.gameObject.SetActive(false);
            if (exhaust != null)
            {
                var em = exhaust.emission;
                em.enabled = false;
            }

            launching = false;
            if (cameraRig != null) cameraRig.departing = false;
            missionProgress = 1f;
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
    /// Orbit / pan / zoom camera driven by the mouse.
    /// </summary>
    public sealed class OrbitCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 46f;
        public float minDistance = 12f;
        public float maxDistance = 200f;

        [Header("Feel")]
        public float zoomStep = .12f;
        public float orbitSensitivity = 4f;
        public float maxPanDistance = 70f;

        public float pitch = 5f;
        public float yaw = 0f;

        /// <summary>When true, mouse input is ignored and the camera is driven directly (launch departure).</summary>
        public bool departing;

        public float homeDistance = 46f;
        public float targetDistance;
        private Vector3 panWanted;
        private Vector3 panApplied;
        private bool initialised;
        private bool leftPan;
        private float lastLeftClick = -10f;
        private Vector3 lastMouse;

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

            if (!departing)
            {
                float scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) > .01f)
                {
                    float factor = Mathf.Clamp(1f - scroll * zoomStep, .55f, 1.8f);
                    targetDistance = Mathf.Clamp(targetDistance * factor, minDistance, maxDistance);
                }

                distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-14f * dt));
                if (Mathf.Abs(distance - targetDistance) < .002f) distance = targetDistance;

                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (Input.GetMouseButton(1) && !shift)
                {
                    yaw += Input.GetAxis("Mouse X") * orbitSensitivity;
                    pitch -= Input.GetAxis("Mouse Y") * orbitSensitivity;
                }

                if (Input.GetMouseButtonDown(0) && !shift && !PointerOverUI())
                {
                    float now = Time.unscaledTime;
                    if (now - lastLeftClick < .28f)
                    {
                        ResetView();
                    }
                    else
                    {
                        leftPan = true;
                    }
                    lastLeftClick = now;
                }
                if (Input.GetMouseButtonUp(0))
                    leftPan = false;

                bool panHeld = Input.GetMouseButton(2) ||
                               (shift && (Input.GetMouseButton(0) || Input.GetMouseButton(1))) ||
                               leftPan;

                if (panHeld)
                {
                    float worldPerPixel = WorldUnitsPerPixel();
                    panWanted -= (transform.right * delta.x + transform.up * delta.y) * worldPerPixel;
                    panWanted = Vector3.ClampMagnitude(panWanted, maxPanDistance);
                }

                panApplied = Vector3.Lerp(panApplied, panWanted, 1f - Mathf.Exp(-20f * dt));
            }

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
