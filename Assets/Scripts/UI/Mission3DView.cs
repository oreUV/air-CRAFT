using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceMissionDesign.Data;

namespace SpaceMissionDesign.UI
{
    /// <summary>
    /// Builds and animates the 3D mission scene: launch pad, a rocket assembled from the
    /// supplied Tripo models (cockpit / fueltank1 / thruster1) with primitive fallbacks,
    /// Earth/Moon backdrop, starfield, and an orbit camera.
    /// </summary>
    public sealed class Mission3DView : MonoBehaviour
    {
        public static Mission3DView Instance { get; private set; }

        private Transform rocketRoot;
        private Transform focus;
        private OrbitCamera cameraRig;
        private Vector3 flameBaseScale = Vector3.one;
        private Transform flame;
        private ParticleSystem exhaust;
        private Vector3 focusHome = new Vector3(4f, 11.5f, 0f);
        private Material white, metal, dark, gold, blue, grey, red, pad, earthMat, moonMat, flameMat;

        // Rocket stack layout (world units), shared by the model placement and decorations.
        // All heights scale the FBX models uniformly, so widths follow each part's own
        // proportions — these constants only position things around the models.
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

        private Material Mat(string name, Color c, float metallic = .2f, float smooth = .45f, Color? emission = null)
        {
            Shader s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Unlit/Color");
            var m = new Material(s) { name = name };
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
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
            white = Mat("Hull white", new Color(.86f, .88f, .90f), .25f, .5f);
            metal = Mat("Metal", new Color(.55f, .58f, .62f), .65f, .7f);
            dark = Mat("Dark", new Color(.12f, .13f, .15f), .4f, .5f);
            gold = Mat("Gold", new Color(.85f, .62f, .22f), .7f, .65f);
            blue = Mat("Blue", new Color(.20f, .35f, .62f), .3f, .5f);
            grey = Mat("Grey", new Color(.38f, .40f, .43f), .35f, .4f);
            red = Mat("Red", new Color(.65f, .22f, .18f), .3f, .5f);
            pad = Mat("Pad", new Color(.13f, .14f, .16f), .1f, .3f);
            earthMat = Mat("Earth", new Color(.12f, .32f, .62f), .05f, .35f, new Color(.02f, .08f, .18f));
            moonMat = Mat("Moon", new Color(.62f, .62f, .60f), .02f, .2f);
            flameMat = Mat("Flame", new Color(1f, .58f, .18f), 0f, .1f, new Color(1f, .45f, .08f) * 2.2f);
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

            // Launch pad and ground.
            Cylinder("Launch pad", new Vector3(10, -.5f, 0), 14f, .7f, pad, null);
            Cylinder("Pad ring", new Vector3(10, -.15f, 0), 8f, .35f, grey, null);
            Cylinder("Ground", new Vector3(10, -1.6f, 0), 90f, .4f, pad, null);

            // Earth and Moon on the open (right) side of the screen.
            Sphere("Earth", new Vector3(58, 36, 100), 18f, earthMat, null);
            Sphere("Moon", new Vector3(86, 16, 112), 8f, moonMat, null);

            BuildStars();
        }

        private void BuildStars()
        {
            var go = new GameObject("Starfield");
            go.transform.position = new Vector3(0, 12, 0);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startSpeed = 0f;
            main.startLifetime = 9999f;
            main.startSize = new ParticleSystem.MinMaxCurve(.6f, 1.8f);
            main.maxParticles = 1400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            var emission = ps.emission;
            emission.enabled = false;

            // A thin shell, not a solid ball: stars were previously spawning a few units from
            // the camera and rendering as big hard squares.
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.SphereShell;
            shape.radius = 200f;
            shape.radiusThickness = .06f;

            Shader psShader = Shader.Find("Particles/Standard Unlit");
            if (psShader == null) psShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (psShader == null) psShader = Shader.Find("Sprites/Default");
            var psMat = new Material(psShader != null ? psShader : Shader.Find("Unlit/Color"));
            psMat.mainTexture = SoftDotTexture();
            if (psMat.HasProperty("_Color")) psMat.SetColor("_Color", new Color(.92f, .95f, 1f, 1f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = psMat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Emit(1400);

            DontDestroyOnLoad(go);
        }

        private static Texture2D softDot;

        /// <summary>
        /// Radial white-to-transparent sprite used for stars and exhaust so the particles are
        /// soft round dots instead of untextured squares.
        /// </summary>
        private static Texture2D SoftDotTexture()
        {
            if (softDot != null) return softDot;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Soft Dot",
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            float c = (size - 1) * .5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x - c, y - c).magnitude / c;
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            softDot = tex;
            return tex;
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
            cam.backgroundColor = new Color(.015f, .02f, .04f);
            cam.fieldOfView = 42f;
            cam.nearClipPlane = .1f;
            cam.farClipPlane = 600f;

            var focusGO = new GameObject("Camera Focus");
            focusGO.transform.position = focusHome;
            DontDestroyOnLoad(focusGO);
            focus = focusGO.transform;

            var orbit = cam.GetComponent<OrbitCamera>();
            if (orbit == null) orbit = cam.gameObject.AddComponent<OrbitCamera>();
            orbit.target = focus;
            orbit.minDistance = 14f;
            orbit.maxDistance = 130f;
            orbit.SetHomeDistance(40f);
            cameraRig = orbit;
        }

        // ---------------- model import ----------------

        /// <summary>
        /// Instantiates a model prefab from Resources/Models, scales it uniformly so it is
        /// <paramref name="height"/> tall, and aligns its bottom-centre to
        /// <paramref name="at"/> (parent local space). Scaling is uniform so the supplied
        /// meshes keep their real proportions.
        /// Returns null when the asset is missing so callers can fall back to primitives.
        /// </summary>
        private Transform PlaceModel(GameObject prefab, Transform parent, Vector3 at,
            float height, float yaw = 0f, Material tint = null)
        {
            return PlaceModel(prefab, parent, at, height, out _, yaw, tint);
        }

        /// <summary>
        /// As above, also reporting the placed height/width/depth in parent space so callers
        /// that arrange several parts (engine clusters, boosters) can space them correctly
        /// no matter what proportions the supplied model has.
        /// </summary>
        private Transform PlaceModel(GameObject prefab, Transform parent, Vector3 at,
            float height, out Vector3 placedSize, float yaw = 0f, Material tint = null)
        {
            placedSize = Vector3.zero;
            if (prefab == null) return null;

            var go = Instantiate(prefab, parent);
            go.name = prefab.name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one;

            foreach (var col in go.GetComponentsInChildren<Collider>()) Destroy(col);
            foreach (var anim in go.GetComponentsInChildren<Animator>()) Destroy(anim);

            if (!TryLocalBounds(go, parent, out var bounds)) return go.transform;

            // The FBX axis conversion is inconsistent between Blender and Unity's importer,
            // so orientation is normalised here from the mesh itself instead of trusting it.
            var upright = UprightRotation(bounds.size);
            if (upright != Quaternion.identity)
            {
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * upright;
                if (TryLocalBounds(go, parent, out var stood)) bounds = stood;
            }

            // Keep the working end down: heat shield, tank base, nozzle bell.
            // The flip is applied in parent space, so it turns the part over rather than
            // spinning it around whatever local axis happens to point up.
            if (WideEndIsUp(go))
            {
                go.transform.localRotation = Quaternion.Euler(180f, 0f, 0f) * go.transform.localRotation;
                if (TryLocalBounds(go, parent, out var flipped)) bounds = flipped;
            }

            float h = Mathf.Max(bounds.size.y, .0001f);

            // Uniform scale: the prefab's own height/width ratio is preserved.
            var scale = Vector3.one * (height / h);
            go.transform.localScale = scale;

            // Scaling happens about the object's own origin, so the parent-space bounds
            // simply scale with it.
            Vector3 min = Vector3.Scale(bounds.min, scale);
            Vector3 max = Vector3.Scale(bounds.max, scale);
            Vector3 centre = (min + max) * .5f;

            go.transform.localPosition = at - new Vector3(centre.x, min.y, centre.z);
            placedSize = max - min;

            if (tint != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = tint;

            return go.transform;
        }

        /// <summary>
        /// Returns the rotation needed to stand a model up, given its current bounds.
        /// Models exported with the long axis on X or Z are rotated so that axis becomes +Y.
        /// Already-upright models get identity, which makes this safe to run every refresh.
        /// </summary>
        private static Quaternion UprightRotation(Vector3 size)
        {
            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));

            // Within a few percent of the vertical dimension: leave it alone.
            if (longest <= size.y * 1.03f) return Quaternion.identity;

            // Lying along X: +X becomes +Y, so -X (the wide end) ends up pointing down.
            if (size.x >= size.z) return Quaternion.Euler(0f, 0f, 90f);

            // Lying along Z: +Z becomes +Y, -Z points down.
            return Quaternion.Euler(-90f, 0f, 0f);
        }

        /// <summary>
        /// True when the wider end of the model sits at the top. Compares how far vertices
        /// spread around the vertical axis in the bottom and top 18% of the mesh, which is
        /// what distinguishes a nozzle bell from a narrow throat, or a heat shield from a nose.
        /// Returns false when vertex data is unavailable (mesh not marked readable), in which
        /// case the bake convention (wide end down) is assumed to already be correct.
        /// </summary>
        private static bool WideEndIsUp(GameObject go)
        {
            var points = new List<Vector3>(1024);
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null) return false;

                Vector3[] vertices;
                try { vertices = mesh.vertices; }
                catch (UnityException) { return false; }   // not readable at runtime

                var toWorld = mf.transform.localToWorldMatrix;
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

        /// <summary>
        /// Axis-aligned bounds of every mesh under <paramref name="go"/>, expressed in
        /// <paramref name="space"/>'s local space. Computed from mesh data and the transform
        /// matrices rather than <see cref="Renderer.bounds"/>, so the result is correct in the
        /// same frame a rotation or scale is applied.
        /// </summary>
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

            // Fallback for renderers that are not plain MeshFilters (e.g. skinned meshes).
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

        // ---------------- rocket assembly ----------------

        public void Refresh(SpacecraftDesign design)
        {
            if (rocketRoot != null) Destroy(rocketRoot.gameObject);
            launching = false;
            if (focus != null) focus.position = focusHome;

            var root = new GameObject("Rocket").transform;
            root.position = new Vector3(10f, .18f, 0f);
            rocketRoot = root;

            var tankPrefab = Resources.Load<GameObject>("Models/fueltank1");
            var capsulePrefab = Resources.Load<GameObject>("Models/cockpit");
            var enginePrefab = Resources.Load<GameObject>("Models/thruster1");
            bool usingModels = tankPrefab != null || capsulePrefab != null || enginePrefab != null;

            // --- Stages (fueltank1) ---
            float stage1Width = Stage1D, stage2Width = Stage2D;
            if (tankPrefab != null)
            {
                PlaceModel(tankPrefab, root, new Vector3(0, Stage1Base, 0), Stage1H, out var s1, 0f, white);
                stage1Width = Mathf.Max(s1.x, s1.z);
                PlaceModel(tankPrefab, root, new Vector3(0, Stage2Base, 0), Stage2H, out var s2, 0f, metal);
                stage2Width = Mathf.Max(s2.x, s2.z);
            }
            else
            {
                Cylinder("Stage 1", new Vector3(0, Stage1Base + Stage1H * .5f, 0), Stage1D * .5f, Stage1H, white, root);
                Cylinder("Stage 2", new Vector3(0, Stage2Base + Stage2H * .5f, 0), Stage2D * .5f, Stage2H, metal, root);
                Cylinder("Sep 1-2", new Vector3(0, Stage1Base + Stage1H - .1f, 0), Stage1D * .5f, .22f, dark, root);
            }

            // --- Service module ---
            Cylinder("Service module", new Vector3(0, ServiceBase + ServiceH * .5f, 0), ServiceD * .5f, ServiceH, metal, root);

            // --- Command module (cockpit) ---
            bool capsulePlaced = false;
            if (capsulePrefab != null)
                capsulePlaced = PlaceModel(capsulePrefab, root, new Vector3(0, CapsuleBase, 0), CapsuleH, 0f, white) != null;
            if (!capsulePlaced)
                Sphere("Command module", new Vector3(0, CapsuleBase + CapsuleH * .45f, 0), CapsuleD, white, root);

            // --- Escape tower ---
            Cylinder("Escape tower", new Vector3(0, StackTop + .6f, 0), .11f, 1.2f, dark, root);
            Sphere("Tower tip", new Vector3(0, StackTop + 1.25f, 0), .24f, dark, root);

            bool has = design != null;
            bool Has(ComponentType t)
            {
                if (!has) return false;
                foreach (var c in design.components) if (c.type == t) return true;
                return false;
            }

            // --- Engines (thruster1): centre bell plus a ring of four, spaced from real bounds ---
            if (!has || Has(ComponentType.Propulsion))
            {
                if (enginePrefab != null)
                {
                    PlaceModel(enginePrefab, root, Vector3.zero, EngineH, out var mainBell, 0f, metal);
                    float mainRadius = Mathf.Max(mainBell.x, mainBell.z) * .5f;

                    // Scaling is uniform, so a shorter bell has proportionally the same width.
                    const float bellH = 1.5f;
                    float smallRadius = mainRadius * (bellH / EngineH);
                    float ring = Mathf.Max(mainRadius + smallRadius * .9f, .45f);

                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * .5f + .4f;
                        PlaceModel(enginePrefab, root,
                            new Vector3(Mathf.Cos(a) * ring, 0, Mathf.Sin(a) * ring), bellH, 0f, metal);
                    }

                    // The generated nozzle meshes are thin open shells with a torn upper rim.
                    // A recessed engine bay hides those rims and ties the cluster to stage 1,
                    // leaving only the clean bell sections visible.
                    float clusterRadius = ring + smallRadius;
                    float bayBottom = EngineH * .52f;
                    float bayTop = Stage1Base + .02f;
                    Cylinder("Engine bay", new Vector3(0, (bayBottom + bayTop) * .5f, 0),
                        clusterRadius * 1.06f, bayTop - bayBottom, dark, root);
                    Cylinder("Thrust ring", new Vector3(0, Stage1Base - .04f, 0),
                        stage1Width * .5f + .05f, .2f, grey, root);
                }
                else
                {
                    Cylinder("Engine bell", new Vector3(0, EngineH * .5f, 0), 1.2f, EngineH, dark, root);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * .5f + .4f;
                        Cylinder("Nozzle", new Vector3(Mathf.Cos(a) * 1.0f, .45f, Mathf.Sin(a) * 1.0f), .3f, .9f, grey, root);
                    }
                }
            }

            // --- Structure: external struts ---
            if (Has(ComponentType.Structure))
            {
                float r = stage1Width * .5f + .09f;
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI * .5f;
                    Cylinder("Strut", new Vector3(Mathf.Cos(a) * r, Stage1Base + Stage1H * .45f, Mathf.Sin(a) * r),
                        .08f, Stage1H * .8f, grey, root);
                }
            }

            float serviceMid = ServiceBase + ServiceH * .5f;

            // --- Power: fuel-cell boxes on the service module ---
            if (Has(ComponentType.Power))
            {
                float x = ServiceD * .5f + .18f;
                Cube("Fuel cell L", new Vector3(-x, serviceMid, 0), new Vector3(.5f, .9f, .8f), blue, root);
                Cube("Fuel cell R", new Vector3(x, serviceMid, 0), new Vector3(.5f, .9f, .8f), blue, root);
            }

            // --- Communications: high-gain dish on a mast ---
            if (Has(ComponentType.Communications))
            {
                float x = ServiceD * .5f + .1f;
                Cylinder("Comm mast", new Vector3(x, serviceMid + .7f, 0), .06f, 1.1f, grey, root);
                Sphere("Comm dish", new Vector3(x + .35f, serviceMid + 1.25f, 0), new Vector3(1f, .35f, 1f), metal, root);
            }

            // --- Thermal: radiator panels ---
            if (Has(ComponentType.Thermal))
            {
                float x = ServiceD * .5f + .12f;
                Cube("Radiator L", new Vector3(-x, serviceMid, 0), new Vector3(.12f, 1.4f, 1.1f), grey, root);
                Cube("Radiator R", new Vector3(x, serviceMid, 0), new Vector3(.12f, 1.4f, 1.1f), grey, root);
            }

            // --- Instruments ring around the top of stage 2 ---
            if (has && design.instruments.Count > 0)
            {
                float y = Stage2Base + Stage2H - .9f;
                float r = stage2Width * .5f + .14f;
                for (int i = 0; i < design.instruments.Count; i++)
                {
                    float a = i * Mathf.PI * 2f / Mathf.Max(1, design.instruments.Count);
                    Cylinder("Instrument " + i, new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r),
                        .2f, .5f, gold, root);
                }
            }

            // --- Launch vehicle flavour: side boosters built from the tank model ---
            if (has && design.launchVehicle != null)
            {
                string id = design.launchVehicle.vehicleId;
                if (id == "lv_falcon_heavy" || id == "lv_delta_iv")
                {
                    const float boosterH = 5.6f;
                    float boosterWidth = boosterH / Mathf.Max(1f, Stage1H / Stage1D);
                    if (tankPrefab != null)
                    {
                        // Measure one booster to get its real width, then mirror it.
                        Transform first = PlaceModel(tankPrefab, root, new Vector3(-1f, Stage1Base + .2f, 0),
                            boosterH, out var bs, 0f, white);
                        boosterWidth = Mathf.Max(bs.x, bs.z);
                        float offset = stage1Width * .5f + boosterWidth * .5f + .12f;
                        if (first != null) first.localPosition = new Vector3(-offset, first.localPosition.y, 0f);
                        PlaceModel(tankPrefab, root, new Vector3(offset, Stage1Base + .2f, 0), boosterH, 0f, white);
                    }
                    else
                    {
                        float offset = stage1Width * .5f + .7f;
                        for (int s = -1; s <= 1; s += 2)
                            Cylinder("Booster", new Vector3(s * offset, Stage1Base + boosterH * .5f + .2f, 0),
                                .62f, boosterH, white, root);
                    }
                }
                if (id == "lv_saturn_v" && enginePrefab == null)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * Mathf.PI * 2f / 5f;
                        Cylinder("F1 nozzle", new Vector3(Mathf.Cos(a) * .9f, .4f, Mathf.Sin(a) * .9f), .34f, .7f, dark, root);
                    }
                }
            }

            // Engine flame (hidden until liftoff).
            var flameGO = Cylinder("Engine flame", new Vector3(0, -2.8f, 0), .9f, 5.2f, flameMat, root);
            flame = flameGO.transform;
            flameBaseScale = flame.localScale;
            flame.gameObject.SetActive(false);

            BuildExhaust(root, Mathf.Min(EngineH * .5f, 1.1f));

            DontDestroyOnLoad(root.gameObject);
            if (!usingModels) Debug.Log("Mission3DView: no models in Resources/Models, using primitive rocket.");
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
            psMat.mainTexture = SoftDotTexture();
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

        private IEnumerator LaunchRoutine()
        {
            launching = true;
            cameraRig?.ClearPan();
            var root = rocketRoot;
            float t = 0f;
            float ignition = .55f;

            if (flame != null) flame.gameObject.SetActive(true);
            if (exhaust != null)
            {
                var em = exhaust.emission;
                em.enabled = true;
            }

            while (t < 3.6f)
            {
                t += Time.deltaTime;
                if (root == null) yield break;

                // Flicker the flame cone without resetting its base proportions.
                if (flame != null)
                {
                    float flicker = 1f + Mathf.Sin(Time.time * 38f) * .12f;
                    flame.localScale = new Vector3(flameBaseScale.x * flicker, flameBaseScale.y * (2f - flicker), flameBaseScale.z * flicker);
                }

                if (t > ignition)
                {
                    float s = t - ignition;
                    float speed = s * s * 5.2f + s * 3f;
                    root.position += Vector3.up * (speed * Time.deltaTime);
                    root.Rotate(Vector3.up, 4f * Time.deltaTime, Space.World);
                }

                // Camera rides up with the vehicle.
                if (focus != null)
                    focus.position = Vector3.Lerp(focus.position, root.position + new Vector3(0f, 8f, 0f), Time.deltaTime * 2.2f);

                yield return null;
            }

            if (flame != null) flame.gameObject.SetActive(false);
            if (exhaust != null)
            {
                var em = exhaust.emission;
                em.enabled = false;
            }
            launching = false;
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
    ///   • Scroll wheel      – zoom in and out (smoothed, never passes through the target)
    ///   • Left-drag         – move (pan) the view, when the pointer is over the open 3D area
    ///   • Middle-drag       – move (pan) the view, works anywhere
    ///   • Right-drag        – orbit around the target
    ///   • Shift + drag      – move (pan) instead of orbiting
    /// </summary>
    public sealed class OrbitCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 46f;
        public float minDistance = 12f;
        public float maxDistance = 140f;

        [Header("Feel")]
        [Tooltip("Fraction of the current distance moved per scroll notch.")]
        public float zoomStep = .12f;
        public float orbitSensitivity = 4f;
        [Tooltip("How far the view can be panned away from the target, in world units.")]
        public float maxPanDistance = 70f;

        private float pitch = 5f;
        private float yaw = 0f;
        private float homeDistance = 46f;
        private float targetDistance;
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

            // ---------------- zoom ----------------
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > .01f)
            {
                // Clamp per-frame so a big trackpad flick cannot invert or leap the zoom.
                float factor = Mathf.Clamp(1f - scroll * zoomStep, .55f, 1.8f);
                targetDistance = Mathf.Clamp(targetDistance * factor, minDistance, maxDistance);
            }

            // Exponential smoothing keeps the zoom frame-rate independent and smooth.
            distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-14f * dt));
            if (Mathf.Abs(distance - targetDistance) < .002f) distance = targetDistance;

            // ---------------- orbit ----------------
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetMouseButton(1) && !shift)
            {
                yaw += Input.GetAxis("Mouse X") * orbitSensitivity;
                pitch -= Input.GetAxis("Mouse Y") * orbitSensitivity;
            }

            // ---------------- pan ----------------
            // Left-drag only pans when the press started over the open 3D area, so the
            // UI panel and its buttons keep working normally. Double-clicking that area
            // recentres the view, which is the way back if you pan or zoom off into space.
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
                // 1:1 "grab the world" panning: convert screen pixels into world units at the
                // pivot's depth so the scene follows the cursor exactly at any zoom level.
                float worldPerPixel = WorldUnitsPerPixel();
                panWanted -= (transform.right * delta.x + transform.up * delta.y) * worldPerPixel;
                panWanted = Vector3.ClampMagnitude(panWanted, maxPanDistance);
            }

            panApplied = Vector3.Lerp(panApplied, panWanted, 1f - Mathf.Exp(-20f * dt));

            // ---------------- apply ----------------
            pitch = Mathf.Clamp(pitch, -20f, 85f);
            var pivot = target.position + panApplied;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = pivot + rot * new Vector3(0f, 0f, -distance);
            transform.LookAt(pivot);
        }

        /// <summary>Clears any panning and returns to the starting orientation and distance.</summary>
        public void ResetView()
        {
            pitch = 5f;
            yaw = 0f;
            panWanted = Vector3.zero;
            panApplied = Vector3.zero;
            targetDistance = homeDistance;
            distance = homeDistance;
        }

        /// <summary>Drops the pan offset but keeps the current zoom and angle.</summary>
        public void ClearPan()
        {
            panWanted = Vector3.zero;
            panApplied = Vector3.zero;
        }

        /// <summary>
        /// Uses the closest distance at which the model is still fully visible when the user
        /// has scrolled too far in, so the camera never ends up inside the rocket.
        /// </summary>
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
