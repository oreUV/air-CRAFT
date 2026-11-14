using System.Collections;
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
        private Transform flame;
        private ParticleSystem exhaust;
        private Vector3 focusHome = new Vector3(4f, 11.5f, 0f);
        private Material white, metal, dark, gold, blue, grey, red, pad, earthMat, moonMat, flameMat;

        // Rocket stack layout (world units), shared by the model placement and decorations.
        private const float EngineH = 2.2f;
        private const float Stage1H = 9.0f;
        private const float Stage2H = 6.4f;
        private const float Stage1D = 3.4f;
        private const float Stage2D = 2.6f;
        private const float ServiceH = 1.6f;
        private const float ServiceD = 2.1f;
        private const float CapsuleH = 2.8f;
        private const float CapsuleD = 2.4f;
        private const float Stage1Base = EngineH;
        private const float Stage2Base = Stage1Base + Stage1H - 1.2f;
        private const float ServiceBase = Stage2Base + Stage2H - .4f;
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
            pad = Mat("Pad", new Color(.22f, .23f, .25f), .1f, .3f);
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
            sun.intensity = 1.25f;
            sun.color = new Color(1f, .97f, .90f);
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            DontDestroyOnLoad(lightGO);

            // Launch pad and ground.
            Cylinder("Launch pad", new Vector3(6, -.35f, 0), 11f, .5f, pad, null);
            Cylinder("Pad ring", new Vector3(6, -.05f, 0), 6.2f, .25f, grey, null);
            Cylinder("Ground", new Vector3(6, -1.2f, 0), 70f, .3f, pad, null);

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
            main.startSize = new ParticleSystem.MinMaxCurve(.22f, .7f);
            main.maxParticles = 1400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 160f;

            Shader psShader = Shader.Find("Particles/Standard Unlit");
            if (psShader == null) psShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (psShader == null) psShader = Shader.Find("Sprites/Default");
            var psMat = new Material(psShader != null ? psShader : Shader.Find("Unlit/Color"));
            psMat.color = new Color(.92f, .95f, 1f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = psMat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Emit(1400);

            DontDestroyOnLoad(go);
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
            orbit.distance = 46f;
            orbit.minDistance = 22f;
            orbit.maxDistance = 100f;
        }

        // ---------------- model import ----------------

        /// <summary>
        /// Instantiates a model prefab from Resources/Models, scales it to the requested
        /// height/width and aligns its bottom-centre to <paramref name="at"/> (local space).
        /// Returns null when the asset is missing so callers can fall back to primitives.
        /// </summary>
        private Transform PlaceModel(GameObject prefab, Transform parent, Vector3 at,
            float height, float width, float yaw = 0f, Material tint = null)
        {
            if (prefab == null) return null;

            var go = Instantiate(prefab, parent);
            go.name = prefab.name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one;

            foreach (var col in go.GetComponentsInChildren<Collider>()) Destroy(col);
            foreach (var anim in go.GetComponentsInChildren<Animator>()) Destroy(anim);

            if (!TryLocalBounds(go, parent, out var bounds)) return go.transform;

            float h = Mathf.Max(bounds.size.y, .0001f);
            float w = Mathf.Max(bounds.size.x, bounds.size.z);
            w = Mathf.Max(w, .0001f);

            var scale = new Vector3(width / w, height / h, width / w);
            go.transform.localScale = scale;

            // Scaling happens about the object's own origin, so the parent-space bounds
            // simply scale with it.
            Vector3 min = Vector3.Scale(bounds.min, scale);
            Vector3 max = Vector3.Scale(bounds.max, scale);
            Vector3 centre = (min + max) * .5f;

            go.transform.localPosition = at - new Vector3(centre.x, min.y, centre.z);

            if (tint != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = tint;

            return go.transform;
        }

        private static bool TryLocalBounds(GameObject go, Transform space, out Bounds bounds)
        {
            bounds = new Bounds();
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0 || space == null) return false;

            bool has = false;
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
            root.position = new Vector3(7f, 1.0f, 0f);
            rocketRoot = root;

            var tankPrefab = Resources.Load<GameObject>("Models/fueltank1");
            var capsulePrefab = Resources.Load<GameObject>("Models/cockpit");
            var enginePrefab = Resources.Load<GameObject>("Models/thruster1");
            bool usingModels = tankPrefab != null || capsulePrefab != null || enginePrefab != null;

            // --- Stages (fueltank1) ---
            if (tankPrefab != null)
            {
                PlaceModel(tankPrefab, root, new Vector3(0, Stage1Base, 0), Stage1H, Stage1D, 0f, white);
                PlaceModel(tankPrefab, root, new Vector3(0, Stage2Base, 0), Stage2H, Stage2D, 0f, metal);
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
                capsulePlaced = PlaceModel(capsulePrefab, root, new Vector3(0, CapsuleBase, 0), CapsuleH, CapsuleD, 0f, white) != null;
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

            // --- Engines (thruster1): centre bell plus a ring of four ---
            if (!has || Has(ComponentType.Propulsion))
            {
                if (enginePrefab != null)
                {
                    PlaceModel(enginePrefab, root, new Vector3(0, 0, 0), EngineH, 1.9f, 0f, dark);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * .5f + .4f;
                        PlaceModel(enginePrefab, root,
                            new Vector3(Mathf.Cos(a) * 1.05f, 0, Mathf.Sin(a) * 1.05f), EngineH * .6f, 1.05f, 0f, grey);
                    }
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
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI * .5f;
                    Cylinder("Strut", new Vector3(Mathf.Cos(a) * (Stage1D * .5f + .08f), Stage1Base + Stage1H * .45f, Mathf.Sin(a) * (Stage1D * .5f + .08f)),
                        .08f, Stage1H * .8f, grey, root);
                }
            }

            float serviceMid = ServiceBase + ServiceH * .5f;

            // --- Power: fuel-cell boxes on the service module ---
            if (Has(ComponentType.Power))
            {
                Cube("Fuel cell L", new Vector3(-1.5f, serviceMid, 0), new Vector3(.5f, .9f, .8f), blue, root);
                Cube("Fuel cell R", new Vector3(1.5f, serviceMid, 0), new Vector3(.5f, .9f, .8f), blue, root);
            }

            // --- Communications: high-gain dish on a mast ---
            if (Has(ComponentType.Communications))
            {
                Cylinder("Comm mast", new Vector3(1.6f, serviceMid + .7f, 0), .06f, 1.1f, grey, root);
                Sphere("Comm dish", new Vector3(1.95f, serviceMid + 1.25f, 0), new Vector3(1f, .35f, 1f), metal, root);
            }

            // --- Thermal: radiator panels ---
            if (Has(ComponentType.Thermal))
            {
                Cube("Radiator L", new Vector3(-1.6f, serviceMid, 0), new Vector3(.12f, 1.4f, 1.1f), grey, root);
                Cube("Radiator R", new Vector3(1.6f, serviceMid, 0), new Vector3(.12f, 1.4f, 1.1f), grey, root);
            }

            // --- Instruments ring around the top of stage 2 ---
            if (has && design.instruments.Count > 0)
            {
                float y = Stage2Base + Stage2H - .9f;
                for (int i = 0; i < design.instruments.Count; i++)
                {
                    float a = i * Mathf.PI * 2f / Mathf.Max(1, design.instruments.Count);
                    Cylinder("Instrument " + i, new Vector3(Mathf.Cos(a) * (Stage2D * .5f + .12f), y, Mathf.Sin(a) * (Stage2D * .5f + .12f)),
                        .2f, .5f, gold, root);
                }
            }

            // --- Launch vehicle flavour: side boosters built from the tank model ---
            if (has && design.launchVehicle != null)
            {
                string id = design.launchVehicle.vehicleId;
                if (id == "lv_falcon_heavy" || id == "lv_delta_iv")
                {
                    for (int s = -1; s <= 1; s += 2)
                    {
                        if (tankPrefab != null)
                            PlaceModel(tankPrefab, root, new Vector3(s * 2.35f, Stage1Base + .2f, 0), 6.4f, 1.25f, 0f, white);
                        else
                            Cylinder("Booster", new Vector3(s * 2.35f, Stage1Base + 3.2f, 0), .62f, 6.4f, white, root);
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
            var flameGO = Cylinder("Engine flame", new Vector3(0, -1.7f, 0), .85f, 3.4f, flameMat, root);
            flame = flameGO.transform;
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

                // Flicker the flame cone.
                if (flame != null)
                {
                    float flicker = 1f + Mathf.Sin(Time.time * 38f) * .12f;
                    flame.localScale = new Vector3(1.7f * flicker, 1.7f * flicker, 1.7f * flicker);
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

    /// <summary>Simple orbit camera: right-drag to rotate, scroll to zoom.</summary>
    public sealed class OrbitCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 46f;
        public float minDistance = 22f;
        public float maxDistance = 100f;

        private float pitch = 5f;
        private float yaw = 0f;

        private void LateUpdate()
        {
            if (target == null) return;

            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * 4f;
                pitch -= Input.GetAxis("Mouse Y") * 4f;
            }
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > .01f)
                distance = Mathf.Clamp(distance - scroll * 3.5f, minDistance, maxDistance);

            pitch = Mathf.Clamp(pitch, -15f, 80f);
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = target.position + rot * new Vector3(0f, 0f, -distance);
            transform.LookAt(target);
        }
    }
}
