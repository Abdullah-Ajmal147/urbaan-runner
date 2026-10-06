using System.IO;
using UnityEditor;
using UnityEngine;

namespace UrbanRunner.Editor
{
    /// <summary>
    /// Bakes the three original courier skins (Mira, Dex, Juno) onto the CC0 Kenney texture layout.
    /// Clothing is fully repainted with our own design (courier jacket, hi-vis band, invented lantern glyph);
    /// only the base face/hair painting is kept, with skin tone and hair recoloured.
    /// Texture layout (1024², origin top-left): head 0–640×0–490, torso 0–640×490–1024,
    /// shoes 640–824×134–524, trousers 610–1024×765–1024.
    /// </summary>
    public static class SkinBaker
    {
        public const string OutDir = "Assets/Games/UrbanRunner/Art/Characters/Skins";
        const string BaseDir = "Assets/Games/UrbanRunner/Art/Characters/Kenney/BaseSkins";

        public struct Courier
        {
            public string Id, BaseSkin;
            public Color32 Jacket, JacketDark, Inner, Band, Emblem, Trousers, Shoes, Skin, Hair, Streak;
            public bool RecolorHair;
        }

        public static readonly Courier[] Couriers =
        {
            new Courier
            {
                Id = "mira", BaseSkin = "skaterFemaleA",
                Jacket = C(0xFF3DA6), JacketDark = C(0xC81E7C), Inner = C(0x1A2050), Band = C(0x2EE6FF), Emblem = C(0xFFB547),
                Trousers = C(0x262A62), Shoes = C(0xFF3DA6), Skin = C(0xF2B48E), Hair = C(0x1B1430), Streak = C(0x2EE6FF), RecolorHair = true,
            },
            new Courier
            {
                Id = "dex", BaseSkin = "skaterMaleA",
                Jacket = C(0x2EE6FF), JacketDark = C(0x149DB5), Inner = C(0x10163A), Band = C(0xFFB547), Emblem = C(0xFF3DA6),
                Trousers = C(0x2B2F3A), Shoes = C(0xFFB547), Skin = C(0x9A6340), Hair = C(0x120E14), Streak = C(0x120E14), RecolorHair = true,
            },
            new Courier
            {
                Id = "juno", BaseSkin = "skaterFemaleA",
                Jacket = C(0xFFB547), JacketDark = C(0xD98A1E), Inner = C(0x3B1D2E), Band = C(0xF4F6FF), Emblem = C(0x2EE6FF),
                Trousers = C(0x3A2340), Shoes = C(0x2EE6FF), Skin = C(0xE9BE9A), Hair = C(0x8A3220), Streak = C(0xFF3DA6), RecolorHair = true,
            },
        };

        /// <summary>UV-mapping aid: a 8×8 grid of distinct colours over the whole texture (cell = 128 px).</summary>
        public static void BakeUvDebug()
        {
            const int N = 1024;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color32[N * N];
            for (var y = 0; y < N; y++)
            for (var x = 0; x < N; x++)
            {
                int cx = x / 128, cy = y / 128; // cy counted from the top in image space
                var c = Color.HSVToRGB(cx / 8f, 0.85f, 0.35f + 0.65f * (cy / 7f));
                var edge = x % 128 < 3 || y % 128 < 3;
                px[(N - 1 - y) * N + x] = edge ? new Color32(0, 0, 0, 255) : (Color32)c;
            }
            t.SetPixels32(px);
            t.Apply();
            File.WriteAllBytes(PathFor("uvdebug"), t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }

        static Color32 C(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        [MenuItem("GameFactory/Urban Runner/Bake Courier Skins")]
        public static void BakeAll()
        {
            Directory.CreateDirectory(OutDir);
            foreach (var c in Couriers) Bake(c);
            AssetDatabase.Refresh();
        }

        public static string PathFor(string id) => $"{OutDir}/{id}.png";

        static void Bake(Courier c)
        {
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes($"{BaseDir}/{c.BaseSkin}.png"));
            var w = src.width;
            var h = src.height;
            var px = src.GetPixels32();

            // Pixel access in top-left image coordinates (Texture2D rows start at the bottom).
            Color32 Get(int x, int y) => px[(h - 1 - y) * w + x];
            void Set(int x, int y, Color32 v) { if (x >= 0 && y >= 0 && x < w && y < h) px[(h - 1 - y) * w + x] = v; }
            void Rect(int x0, int y0, int x1, int y1, Color32 v) { for (var y = y0; y < y1; y++) for (var x = x0; x < x1; x++) Set(x, y, v); }

            // 1. Skin tone everywhere: peach family → target, keeping the original shading.
            var refSkin = new Color32(0xF6, 0x96, 0x76, 255);
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var p = Get(x, y);
                if (!IsSkin(p)) continue;
                Set(x, y, Shade(c.Skin, Lum(p) / Lum(refSkin), p.a));
            }

            // 2. Hair (dark pixels in the head band, outside the face) and the coloured streak.
            for (var y = 0; y < 330; y++)
            for (var x = 0; x < 640; x++)
            {
                var p = Get(x, y);
                Color.RGBToHSV(p, out var hue, out var sat, out var val);
                var inFace = x > 250 && x < 400 && y > 185 && y < 290;
                if (c.RecolorHair && val < 0.36f && !inFace) Set(x, y, Shade(c.Hair, 0.75f + val, p.a));
                else if (sat > 0.45f && val > 0.45f && (hue > 0.40f && hue < 0.55f)) Set(x, y, Shade(c.Streak, val, p.a)); // teal streak
            }

            // 3. Torso: our courier jacket, painted from scratch.
            const int T0 = 490;
            Rect(0, T0, 640, 1024, c.Jacket);
            Rect(0, T0, 40, 1024, c.JacketDark);                  // sleeve shading
            Rect(600, T0, 640, 1024, c.JacketDark);
            // Torso texture columns: ~0–128 / 512–640 forearms, 128–256 / 384–512 upper arms, 256–384 body (front + back).
            Rect(292, T0, 348, 1024, c.Inner);                     // open-jacket strip showing the shirt
            Rect(314, T0 + 20, 326, 1010, c.JacketDark);           // zip
            Rect(0, 690, 640, 726, c.Band);                         // hi-vis courier band all round
            Rect(0, 726, 640, 732, Darken(c.Band, 0.7f));
            Rect(0, 990, 640, 1024, c.JacketDark);                  // cuffs / hem
            Rect(110, T0, 132, 1024, Darken(c.Jacket, 0.82f));      // bag strap over the shoulder
            Rect(508, T0, 530, 1024, Darken(c.Jacket, 0.82f));
            LanternGlyph(Set, 256, 790, c.Emblem, c.JacketDark);    // invented emblem (no real brand or letters)

            // 4. Trousers.
            Rect(610, 765, 1024, 1024, c.Trousers);
            Rect(610, 765, 1024, 775, Darken(c.Trousers, 0.7f));
            Rect(700, 775, 712, 1024, c.Band);                      // side piping
            Rect(920, 775, 932, 1024, c.Band);

            // 5. Shoes: any saturated colour in the shoe box becomes the accent.
            for (var y = 134; y < 524; y++)
            for (var x = 640; x < 824; x++)
            {
                var p = Get(x, y);
                Color.RGBToHSV(p, out _, out var sat, out var val);
                if (sat > 0.45f && val > 0.35f) Set(x, y, Shade(c.Shoes, val, p.a));
            }

            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.SetPixels32(px);
            outTex.Apply();
            File.WriteAllBytes(PathFor(c.Id), outTex.EncodeToPNG());
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(outTex);
        }

        /// <summary>A paper lantern: rounded body, cap, base and two ribs. Invented, readable at tiny size.</summary>
        static void LanternGlyph(System.Action<int, int, Color32> set, int x0, int y0, Color32 fg, Color32 bg)
        {
            const int W = 128, H = 150;
            for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++)
            {
                var nx = (x - W / 2f) / (W / 2f);
                var ny = (y - H / 2f) / (H / 2f * 0.78f);
                var body = nx * nx + ny * ny * 0.85f < 0.82f;
                var cap = y < 18 && x > 34 && x < 94;
                var foot = y > H - 18 && x > 40 && x < 88;
                if (!(body || cap || foot)) continue;
                var rib = body && (Mathf.Abs(x - W / 2) < 5 || (Mathf.Abs(nx) > 0.42f && Mathf.Abs(nx) < 0.5f));
                set(x0 + x, y0 + y, rib ? bg : fg);
            }
        }

        static bool IsSkin(Color32 p)
        {
            Color.RGBToHSV(p, out var hue, out var sat, out var val);
            return hue > 0.015f && hue < 0.075f && sat > 0.28f && sat < 0.62f && val > 0.78f;
        }

        static float Lum(Color32 c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;

        static Color32 Shade(Color32 target, float factor, byte a) => new Color32(
            (byte)Mathf.Clamp(target.r * factor, 0, 255),
            (byte)Mathf.Clamp(target.g * factor, 0, 255),
            (byte)Mathf.Clamp(target.b * factor, 0, 255), a);

        static Color32 Darken(Color32 c, float f) => Shade(c, f, 255);
    }
}
