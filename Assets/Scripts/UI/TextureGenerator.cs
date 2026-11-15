using UnityEngine;

namespace SpaceMissionDesign.UI
{
    /// <summary>
    /// Paints textures procedurally at runtime so the rocket, planets and particles
    /// never need external image files. Every texture is seeded and tiling so it
    /// looks intentional, not placeholder.
    /// </summary>
    public static class TextureGenerator
    {
        private static readonly System.Random rng = new System.Random(1729);

        private static float Rnd(float min, float max) => (float)(min + rng.NextDouble() * (max - min));
        private static float Rnd() => (float)rng.NextDouble();

        // ------------------------------------------------------------------
        // Rocket surfaces
        // ------------------------------------------------------------------

        /// <summary>Brushed metal with scattered scratches and oxidation specks.</summary>
        public static Texture2D Metal(int size = 512)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[size * size];

            Color baseCol = new Color(.46f, .50f, .55f);
            for (int i = 0; i < px.Length; i++) px[i] = baseCol;

            // Brushed grain: horizontal streaks.
            for (int y = 0; y < size; y++)
            {
                float band = Mathf.PerlinNoise(0f, y * .03f);
                for (int x = 0; x < size; x++)
                {
                    float stroke = Mathf.PerlinNoise(x * .015f, y * .02f + 100f);
                    float v = baseCol.grayscale + (stroke - .5f) * .18f + (band - .5f) * .06f;
                    px[y * size + x] = new Color(v, v + .02f, v + .04f, 1);
                }
            }

            // Random scratches.
            for (int s = 0; s < size * 2; s++)
            {
                int x = (int)Rnd(0, size - 20);
                int y = (int)Rnd(0, size);
                int len = (int)Rnd(8, 40);
                float grey = Rnd(.25f, .45f);
                for (int i = 0; i < len; i++)
                {
                    int xi = Mathf.Clamp(x + i, 0, size - 1);
                    int yi = Mathf.Clamp(y + (int)(Mathf.Sin(i * .4f) * 2f), 0, size - 1);
                    px[yi * size + xi] = new Color(grey, grey + .01f, grey + .03f, 1);
                }
            }

            // Oxidation / dirt specks.
            for (int s = 0; s < size * 3; s++)
            {
                int x = (int)Rnd(0, size);
                int y = (int)Rnd(0, size);
                int r = (int)Rnd(2, 6);
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int xi = Mathf.Clamp(x + dx, 0, size - 1);
                        int yi = Mathf.Clamp(y + dy, 0, size - 1);
                        if ((dx * dx + dy * dy) > r * r) continue;
                        float fall = 1f - new Vector2(dx, dy).magnitude / r;
                        Color c = px[yi * size + xi];
                        c = Color.Lerp(c, new Color(.32f, .34f, .38f), fall * .35f);
                        px[yi * size + xi] = c;
                    }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Off-white paint with grime streaks and subtle panel variation.</summary>
        public static Texture2D WhitePaint(int size = 512)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[size * size];

            Color baseCol = new Color(.90f, .92f, .95f);
            for (int i = 0; i < px.Length; i++) px[i] = baseCol;

            // Panels: faint vertical bands.
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float band = Mathf.PerlinNoise(x * .008f, 50f);
                    float v = baseCol.grayscale + (band - .5f) * .04f;
                    px[y * size + x] = new Color(v, v + .01f, v + .03f, 1);
                }

            // Grime streaks running downward.
            for (int s = 0; s < size; s++)
            {
                int x = (int)Rnd(0, size);
                int y = (int)Rnd(0, size / 2);
                int len = (int)Rnd(20, 120);
                for (int i = 0; i < len; i++)
                {
                    int xi = Mathf.Clamp(x + (int)(Mathf.Sin(i * .1f) * 3f), 0, size - 1);
                    int yi = Mathf.Clamp(y + i, 0, size - 1);
                    float fall = 1f - i / (float)len;
                    Color c = px[yi * size + xi];
                    c = Color.Lerp(c, new Color(.62f, .66f, .72f), fall * .5f);
                    px[yi * size + xi] = c;
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Black thermal tiles with a grid and hot-spot mottling.</summary>
        public static Texture2D ThermalTiles(int size = 512)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[size * size];

            Color baseCol = new Color(.08f, .09f, .10f);
            for (int i = 0; i < px.Length; i++) px[i] = baseCol;

            int tile = 24;
            for (int y = 0; y < size; y += tile)
                for (int x = 0; x < size; x += tile)
                {
                    // Grout lines.
                    for (int gy = 0; gy < 2 && y + gy < size; gy++)
                        for (int gx = 0; gx < tile && x + gx < size; gx++)
                            px[(y + gy) * size + x + gx] = new Color(.03f, .03f, .035f, 1);
                    for (int gx = 0; gx < 2 && x + gx < size; gx++)
                        for (int gy = 0; gy < tile && y + gy < size; gy++)
                            px[(y + gy) * size + x + gx] = new Color(.03f, .03f, .035f, 1);
                }

            // Char / heat mottle.
            for (int s = 0; s < size * 4; s++)
            {
                int x = (int)Rnd(0, size);
                int y = (int)Rnd(0, size);
                int r = (int)Rnd(4, 16);
                float hot = Rnd(.15f, .35f);
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int xi = Mathf.Clamp(x + dx, 0, size - 1);
                        int yi = Mathf.Clamp(y + dy, 0, size - 1);
                        if ((dx * dx + dy * dy) > r * r) continue;
                        float fall = 1f - new Vector2(dx, dy).magnitude / r;
                        Color c = px[yi * size + xi];
                        c = Color.Lerp(c, new Color(hot, hot * .85f, hot * .7f), fall * .6f);
                        px[yi * size + xi] = c;
                    }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Gold foil with a subtle crinkle pattern.</summary>
        public static Texture2D GoldFoil(int size = 512)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[size * size];

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float crinkle = Mathf.PerlinNoise(x * .02f, y * .02f + 50f);
                    float v = .78f + (crinkle - .5f) * .12f;
                    px[y * size + x] = new Color(v, v * .62f, v * .18f, 1);
                }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------
        // Planets
        // ------------------------------------------------------------------

        /// <summary>Procedural "blue marble" Earth: deep oceans, continents, clouds.</summary>
        public static Texture2D Earth(int size = 1024)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];

            Color ocean = new Color(.06f, .16f, .30f);
            Color shallow = new Color(.14f, .42f, .62f);
            Color land = new Color(.28f, .36f, .22f);
            Color mountain = new Color(.42f, .44f, .38f);
            Color ice = new Color(.88f, .92f, .96f);

            float cx = size * .5f, cy = size * .5f;
            float r = size * .46f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float d = new Vector2(dx, dy).magnitude / r;
                    if (d > 1f) { px[y * size + x] = new Color(0, 0, 0, 0); continue; }

                    // Continental noise: large blobs of land.
                    float n1 = Mathf.PerlinNoise(x * .0018f, y * .0018f);
                    float n2 = Mathf.PerlinNoise(x * .0045f + 100f, y * .0045f + 100f);
                    float n3 = Mathf.PerlinNoise(x * .0009f, y * .0009f + 300f);
                    float landN = n1 * .6f + n2 * .3f + n3 * .1f;

                    // Latitude bias: poles icy, equator more ocean.
                    float lat = Mathf.Abs(dy) / (size * .5f);
                    float polar = Mathf.SmoothStep(.7f, 1f, lat);
                    float equator = 1f - polar;

                    Color c;
                    if (polar > .85f) c = ice;
                    else if (landN > .58f + equator * .08f) c = mountain;
                    else if (landN > .48f + equator * .10f) c = land;
                    else if (d < .55f) c = shallow;
                    else c = ocean;

                    // Atmosphere falloff toward limb.
                    float limb = 1f - d * d;
                    c = Color.Lerp(c, new Color(.16f, .30f, .52f), (1f - limb) * .5f);
                    c.a = limb;

                    px[y * size + x] = c;
                }
            }

            // High-altitude cloud streaks.
            for (int s = 0; s < size * 2; s++)
            {
                int x = (int)Rnd(0, size);
                int y = (int)Rnd(0, size);
                int len = (int)Rnd(40, 200);
                int thick = (int)Rnd(6, 18);
                float angle = Rnd(0, Mathf.PI * 2f);
                for (int i = 0; i < len; i++)
                {
                    int xi = Mathf.Clamp(x + (int)(Mathf.Cos(angle) * i), 0, size - 1);
                    int yi = Mathf.Clamp(y + (int)(Mathf.Sin(angle) * i), 0, size - 1);
                    float fall = 1f - i / (float)len;
                    Color c = px[yi * size + xi];
                    c = Color.Lerp(c, new Color(1f, 1f, 1f, .55f), fall * .5f);
                    px[yi * size + xi] = c;
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Gray Moon with scattered craters of all sizes.</summary>
        public static Texture2D Moon(int size = 512)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];

            Color surface = new Color(.62f, .62f, .60f);
            for (int i = 0; i < px.Length; i++) px[i] = surface;

            float cx = size * .5f, cy = size * .5f;
            float r = size * .48f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float d = new Vector2(dx, dy).magnitude / r;
                    if (d > 1f) { px[y * size + x] = new Color(0, 0, 0, 0); continue; }
                    float limb = 1f - d * d;
                    px[y * size + x] = new Color(surface.r * limb, surface.g * limb, surface.b * limb, limb);
                }
            }

            // Craters: dark rings with bright rims.
            for (int s = 0; s < 400; s++)
            {
                int cx2 = (int)Rnd(0, size);
                int cy2 = (int)Rnd(0, size);
                int cr = (int)Rnd(3, 60);
                float craterD = new Vector2(cx2 - cx, cy2 - cy).magnitude / r;
                if (craterD > .92f) continue;

                // Dark interior.
                for (int dy = -cr; dy <= cr; dy++)
                    for (int dx = -cr; dx <= cr; dx++)
                    {
                        int xi = Mathf.Clamp(cx2 + dx, 0, size - 1);
                        int yi = Mathf.Clamp(cy2 + dy, 0, size - 1);
                        float dd = new Vector2(dx, dy).magnitude;
                        if (dd > cr) continue;
                        float fall = 1f - dd / cr;
                        Color c = px[yi * size + xi];
                        c = Color.Lerp(c, new Color(.42f, .42f, .40f), fall * .4f);
                        px[yi * size + xi] = c;
                    }

                // Bright rim.
                for (int dy = -cr - 2; dy <= cr + 2; dy++)
                    for (int dx = -cr - 2; dx <= cr + 2; dx++)
                    {
                        int xi = Mathf.Clamp(cx2 + dx, 0, size - 1);
                        int yi = Mathf.Clamp(cy2 + dy, 0, size - 1);
                        float dd = new Vector2(dx, dy).magnitude;
                        if (dd < cr - 1 || dd > cr + 2) continue;
                        float fall = 1f - Mathf.Abs(dd - cr) / 3f;
                        Color c = px[yi * size + xi];
                        c = Color.Lerp(c, new Color(.82f, .82f, .80f), fall * .6f);
                        px[yi * size + xi] = c;
                    }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Soft radial dot used for stars and exhaust particles.</summary>
        public static Texture2D SoftDot(int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Soft Dot",
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            float c = (size - 1) * .5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x - c, y - c).magnitude / c;
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Simple flag: stripes and a canton with dots (stars).</summary>
        public static Texture2D Flag(int size = 256)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];

            Color red = new Color(.72f, .12f, .12f);
            Color white = Color.white;
            Color blue = new Color(.10f, .16f, .34f);

            int cantonW = size / 3;
            int cantonH = size / 2;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Color c;
                    if (x < cantonW && y < cantonH) c = blue;
                    else c = (y % (size / 13) < size / 26) ? red : white;

                    // Canton stars.
                    if (x < cantonW && y < cantonH)
                    {
                        float fx = x / (float)cantonW, fy = y / (float)cantonH;
                        float n = Mathf.PerlinNoise(fx * 9f, fy * 9f + 100f);
                        if (n > .86f) c = white;
                    }

                    px[y * size + x] = c;
                }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Procedural cloud layer: wispy white clouds on a transparent background.</summary>
        public static Texture2D Clouds(int size = 1024)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[size * size];

            // Start fully transparent.
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 0);

            // Wispy cloud streaks.
            for (int s = 0; s < size * 3; s++)
            {
                int x = (int)Rnd(0, size);
                int y = (int)Rnd(0, size);
                int len = (int)Rnd(30, 180);
                int thick = (int)Rnd(4, 14);
                float angle = Rnd(0, Mathf.PI * 2f);
                for (int i = 0; i < len; i++)
                {
                    int xi = Mathf.Clamp(x + (int)(Mathf.Cos(angle) * i), 0, size - 1);
                    int yi = Mathf.Clamp(y + (int)(Mathf.Sin(angle) * i), 0, size - 1);
                    for (int dy = -thick; dy <= thick; dy++)
                        for (int dx = -thick; dx <= thick; dx++)
                        {
                            int x2 = Mathf.Clamp(xi + dx, 0, size - 1);
                            int y2 = Mathf.Clamp(yi + dy, 0, size - 1);
                            float dd = new Vector2(dx, dy).magnitude;
                            if (dd > thick) continue;
                            float fall = 1f - dd / thick;
                            float alpha = fall * .65f * (1f - i / (float)len);
                            var existing = px[y2 * size + x2];
                            existing.a = (byte)Mathf.Max(existing.a, alpha * 255f);
                            px[y2 * size + x2] = existing;
                        }
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
