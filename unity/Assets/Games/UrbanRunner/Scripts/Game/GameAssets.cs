using TMPro;
using UnityEngine;

namespace UrbanRunner.Game
{
    /// <summary>
    /// Everything the views need, wired by the editor setup (UrbanRunnerProjectSetup.EnsureGameAssets).
    /// Referencing materials here also guarantees their shaders are included in builds.
    /// </summary>
    [CreateAssetMenu(menuName = "Urban Runner/Game Assets")]
    public sealed class GameAssets : ScriptableObject
    {
        [Header("Palette (HANDOVER §3.1), toon-shaded with outline for obstacles")]
        public Material night;
        public Material surface;
        public Material magenta;
        public Material cyan;
        public Material amber;
        public Material text;
        public Material success;
        public Material danger;

        [Header("World")]
        public Material ground;
        public Material laneLine;
        public Material vanBody;
        public Material glass;
        public Material wood;
        public Material asphalt;
        public Material pavement;
        public Material kerb;
        public Material buildingToon;
        public Material awningMagenta, awningCyan, awningAmber;
        public Material lanternAmber, lanternMagenta, lanternCyan, lanternCap;
        public Material neonMagenta, neonCyan, neonAmber, signBack, cord;
        public GameObject[] buildings;
        public Material[] buildingPaints;
        public Material[] tramPaints;
        public Material tramCream, tramRoof, tramWindow, rail;
        public Material sky;

        [Header("Glow (unlit / additive)")]
        public Material sparkGlow;
        public Material magnetGlow;
        public Material shieldGlow;
        public Material doubleGlow;
        public Material haloAmber, haloMagenta, haloCyan;
        public Material streakAmber, streakMagenta, streakCyan;
        public Material sparkHalo, shieldHalo;
        public Material trailGlow, kiteToon;
        [Tooltip("Lantern Hunt colours, in order: amber, pink, blue, mint, violet.")]
        public Material[] huntBodies, huntHalos, huntBeams;
        public Material blobShadow;

        [Header("Courier (Kenney CC0 model, our skins)")]
        public GameObject courierModel;
        public AnimationClip runClip, jumpClip, idleClip;
        public Material courierMira, courierDex, courierJuno;

        [Header("Audio")]
        public AudioClip music;
        public AudioClip sfxSpark, sfxLane, sfxJump, sfxSlide, sfxLand, sfxCrash, sfxPowerUp, sfxShieldBreak, sfxRevive, sfxCountdown, sfxUiTap;

        [Header("UI")]
        public TMP_FontAsset font;        // Instrument Sans SemiBold (body)
        public TMP_FontAsset displayFont; // Bricolage Grotesque ExtraBold (numbers, titles)
        public TMP_FontAsset iconFont;    // Phosphor Fill (icons as glyphs)
        [Tooltip("Font materials with a soft dark underlay, so HUD text reads on the bright dusk sky (D27).")]
        public Material fontShadow, displayFontShadow;

        public Material CourierMaterial(string id) =>
            id == "dex" ? courierDex : id == "juno" ? courierJuno : courierMira;
    }

    /// <summary>Phosphor Fill glyph codepoints used by the HUD.</summary>
    public static class Icons
    {
        public const string Magnet = "";
        public const string Shield = "";
        public const string Lightning = "";
        public const string Sparkle = "";
        public const string Pause = "";
        public const string Play = "";
        public const string Left = "";
        public const string Right = "";
        public const string Up = "";
        public const string Down = "";
        public const string Check = "";
        public const string Warning = "\ue4e2"; // warning-circle
    }
}
