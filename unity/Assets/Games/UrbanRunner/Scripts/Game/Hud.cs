using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UrbanRunner.Sim;

namespace UrbanRunner.Game
{
    /// <summary>
    /// In-run HUD (HANDOVER §4.2: the HUD lives in Unity), matching the approved design: big Bricolage numbers,
    /// Phosphor icons, solid panels, the "Keep running?" card and tutorial prompts. Built in code at load;
    /// text updates use TMP's non-allocating SetText, so the run loop doesn't allocate.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        static readonly Color Night = new Color32(0x0E, 0x12, 0x30, 0xFF);
        static readonly Color Deep = new Color32(0x0B, 0x0F, 0x29, 0xFF);
        static readonly Color Magenta = new Color32(0xFF, 0x3D, 0xA6, 0xFF);
        static readonly Color MagentaShade = new Color32(0xB5, 0x19, 0x6F, 0xFF);
        static readonly Color Ink = new Color32(0x14, 0x05, 0x1F, 0xFF);
        static readonly Color Cyan = new Color32(0x2E, 0xE6, 0xFF, 0xFF);
        static readonly Color Amber = new Color32(0xFF, 0xB5, 0x47, 0xFF);
        static readonly Color TextColor = new Color32(0xF4, 0xF6, 0xFF, 0xFF);
        static readonly Color Muted = new Color32(0xC3, 0xC7, 0xEA, 0xFF);
        static readonly Color Success = new Color32(0x3D, 0xFF, 0xA2, 0xFF);
        static readonly Color Danger = new Color32(0xFF, 0x8A, 0x8A, 0xFF);
        static readonly Color CardInk = new Color32(0x4A, 0x50, 0x80, 0xFF);

        public event Action PausePressed, ResumePressed, QuitPressed, ReviveYes, ReviveNo;

        GameAssets _a;
        Sprite _round, _ring;
        TextMeshProUGUI _toast, _toastIcon, _combo;
        RectTransform _pill, _comboRoot;
        float _pillPulse, _comboPulse;
        int _shownCombo = -1;
        RectTransform _toastRoot;
        float _toastLeft;
        TextMeshProUGUI _distance, _bestLine, _sparks, _countdown, _reviveDistance, _reviveCause, _reviveBest, _reviveSparks, _prompt, _promptIcon;
        GameObject _pauseButton, _pauseMenu, _revive, _waiting, _promptCard;
        Image _ringFill;
        readonly Image[] _timerFill = new Image[3];
        readonly GameObject[] _timerRoot = new GameObject[3];
        int _shownDistance = -1, _shownSparks = -1, _shownCountdown = -1, _shownBestDelta = int.MinValue;
        string _shownPrompt;

        public int LastCountdown => _shownCountdown;

        public void Init(GameAssets assets)
        {
            _a = assets;
            _round = MakeRoundedSprite(64, 30);
            _ring = MakeRingSprite(128, 0.22f);

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.GetComponent<RectTransform>();
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).transform.SetParent(transform, false);

            var safe = Rect(root, "Safe", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeArea>();

            // Distance + best line (top-left)
            _distance = Text(safe, "Distance", _a.displayFont, 120, TextColor, new Vector2(0, 1), new Vector2(56, -60), new Vector2(700, 140), TextAlignmentOptions.TopLeft);
            _distance.characterSpacing = -4;
            _bestLine = Text(safe, "BestLine", _a.font, 34, Muted, new Vector2(0, 1), new Vector2(60, -196), new Vector2(700, 50), TextAlignmentOptions.TopLeft);

            // Sparks pill + pause (top-right)
            var pill = Panel(safe, "SparksPill", Deep, new Vector2(1, 1), new Vector2(-196, -60), new Vector2(250, 120), new Vector2(1, 1));
            Icon(pill, Icons.Sparkle, 50, Cyan, new Vector2(0, 0.5f), new Vector2(54, 0));
            _sparks = Text(pill, "Count", _a.displayFont, 60, TextColor, new Vector2(0, 0.5f), new Vector2(96, 0), new Vector2(150, 90), TextAlignmentOptions.Left, new Vector2(0, 0.5f));
            _pill = pill.rectTransform;
            // Combo badge under the sparks pill: "×12" while gems keep coming (D32).
            var combo = Panel(safe, "Combo", Cyan, new Vector2(1, 1), new Vector2(-196, -192), new Vector2(150, 70), new Vector2(1, 1));
            _combo = Text(combo, "Text", _a.displayFont, 46, Ink, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 70), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f));
            _combo.SetText("×0123456789");
            _combo.ForceMeshUpdate(); // pre-warm glyphs: no atlas allocation mid-run
            _comboRoot = combo.rectTransform;
            _comboRoot.gameObject.SetActive(false);
            var pause = Panel(safe, "Pause", Deep, new Vector2(1, 1), new Vector2(-56, -60), new Vector2(120, 120), new Vector2(1, 1));
            Icon(pause, Icons.Pause, 54, TextColor, new Vector2(0.5f, 0.5f), Vector2.zero);
            AddButton(pause.gameObject, () => PausePressed?.Invoke());
            _pauseButton = pause.gameObject;

            // Power-up tiles (left column)
            var colors = new[] { Magenta, Success, Amber };
            var icons = new[] { Icons.Magnet, Icons.Shield, Icons.Lightning };
            for (var i = 0; i < 3; i++)
            {
                var row = Rect(safe, "PowerUp" + i, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -270 - i * 112), new Vector2(330, 92));
                row.pivot = new Vector2(0, 1);
                var tile = Panel(row, "Tile", colors[i], new Vector2(0, 0.5f), Vector2.zero, new Vector2(92, 92), new Vector2(0, 0.5f));
                Icon(tile, icons[i], 50, Ink, new Vector2(0.5f, 0.5f), Vector2.zero);
                var track = Panel(row, "Track", new Color(colors[i].r, colors[i].g, colors[i].b, 0.25f), new Vector2(0, 0.5f), new Vector2(116, 0), new Vector2(200, 18), new Vector2(0, 0.5f));
                var fill = Panel(track, "Fill", colors[i], new Vector2(0, 0), Vector2.zero, Vector2.zero, new Vector2(0, 0.5f));
                fill.rectTransform.anchorMax = new Vector2(1, 1);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                _timerFill[i] = fill;
                _timerRoot[i] = row.gameObject;
                row.gameObject.SetActive(false);
            }

            // Toast (upper middle): "Faster!", "The Warden is on you!"
            _toastRoot = Rect(root, "Toast", new Vector2(0.5f, 0.74f), new Vector2(0.5f, 0.74f), Vector2.zero, new Vector2(900, 130));
            _toastIcon = Icon(_toastRoot, Icons.Lightning, 86, Amber, new Vector2(0.5f, 0.5f), new Vector2(-300, 0));
            _toast = Text(_toastRoot, "Text", _a.displayFont, 96, TextColor, new Vector2(0.5f, 0.5f), new Vector2(40, 0), new Vector2(700, 130), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f));
            // Pre-warm the dynamic font atlases so the first toast never allocates mid-run.
            _toast.SetText("Faster! Warden on you! New best! Close! Lantern 12345 of 5! Hunt complete!");
            _toast.ForceMeshUpdate();
            _toastIcon.SetText(Icons.Lightning + Icons.Warning + Icons.Sparkle);
            _toastIcon.ForceMeshUpdate();
            _toastRoot.gameObject.SetActive(false);

            // Tutorial prompt (centre card)
            var promptCard = Panel(root, "TutorialPrompt", new Color(0.04f, 0.05f, 0.16f, 0.92f), new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(820, 300), new Vector2(0.5f, 0.5f));
            _promptIcon = Icon(promptCard, Icons.Left, 130, Amber, new Vector2(0.5f, 0.66f), Vector2.zero);
            _prompt = Text(promptCard, "Prompt", _a.displayFont, 72, TextColor, new Vector2(0.5f, 0.24f), Vector2.zero, new Vector2(780, 100), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f));
            _promptCard = promptCard.gameObject;
            _promptCard.SetActive(false);

            BuildPauseMenu(root);
            BuildRevive(root);

            _waiting = Overlay(root, "WaitingForAd", new Color(0.035f, 0.043f, 0.13f, 0.86f));
            Text(_waiting.GetComponent<RectTransform>(), "Text", _a.font, 48, TextColor, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 80), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f)).SetText("Loading ad…");
            _waiting.SetActive(false);
        }

        void BuildPauseMenu(RectTransform root)
        {
            _pauseMenu = Overlay(root, "PauseMenu", new Color(0.035f, 0.043f, 0.13f, 0.9f));
            var pm = _pauseMenu.GetComponent<RectTransform>();
            Text(pm, "Title", _a.displayFont, 140, TextColor, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(900, 170), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f)).SetText("Paused");
            PrimaryButton(pm, "Resume", "Keep running", new Vector2(0.5f, 0.46f), new Vector2(820, 170), () => ResumePressed?.Invoke());
            TextButton(pm, "Quit", "Quit this run", new Vector2(0.5f, 0.36f), () => QuitPressed?.Invoke());
            _pauseMenu.SetActive(false);
        }

        void BuildRevive(RectTransform root)
        {
            _revive = Overlay(root, "Revive", new Color(0.035f, 0.043f, 0.13f, 0.8f));
            var rv = _revive.GetComponent<RectTransform>();
            var chip = Panel(rv, "Cause", new Color(1f, 0.35f, 0.35f, 0.16f), new Vector2(0, 1), new Vector2(66, -180), new Vector2(620, 74), new Vector2(0, 1));
            _reviveCause = Text(chip, "Text", _a.font, 34, Danger, new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(580, 60), TextAlignmentOptions.Left, new Vector2(0, 0.5f));
            _reviveDistance = Text(rv, "Distance", _a.displayFont, 150, TextColor, new Vector2(0, 1), new Vector2(60, -280), new Vector2(960, 170), TextAlignmentOptions.TopLeft);
            _reviveBest = Text(rv, "Best", _a.font, 42, Muted, new Vector2(0, 1), new Vector2(66, -460), new Vector2(960, 60), TextAlignmentOptions.TopLeft);

            var card = Panel(rv, "Card", TextColor, new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(1000, 600), new Vector2(0.5f, 0));
            var ringBack = Img(card, "RingBack", _ring, new Color32(0xE3, 0xE5, 0xF5, 0xFF), new Vector2(0, 1), new Vector2(60, -60), new Vector2(180, 180), new Vector2(0, 1));
            _ringFill = Img(ringBack.rectTransform, "RingFill", _ring, Magenta, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180, 180), new Vector2(0.5f, 0.5f));
            _ringFill.type = Image.Type.Filled;
            _ringFill.fillMethod = Image.FillMethod.Radial360;
            _ringFill.fillOrigin = (int)Image.Origin360.Top;
            _ringFill.fillClockwise = false;
            _countdown = Text(ringBack.rectTransform, "Count", _a.displayFont, 76, Night, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180, 180), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f));
            Text(card, "Title", _a.displayFont, 80, Night, new Vector2(0, 1), new Vector2(276, -70), new Vector2(680, 100), TextAlignmentOptions.TopLeft).SetText("Keep running?");
            _reviveSparks = Text(card, "Sub", _a.font, 40, CardInk, new Vector2(0, 1), new Vector2(278, -172), new Vector2(680, 60), TextAlignmentOptions.TopLeft);
            PrimaryButton(card, "Yes", "Watch a short ad", new Vector2(0.5f, 0), new Vector2(900, 170), () => ReviveYes?.Invoke(), new Vector2(0, 160), Icons.Play);
            TextButton(card, "No", "No thanks, see results", new Vector2(0.5f, 0), () => ReviveNo?.Invoke(), new Vector2(0, 60), CardInk);
            _revive.SetActive(false);
        }

        /// <summary>A short, punchy message that pops in and fades (no allocation: text is a literal).</summary>
        public void Toast(string text, string icon, Color color)
        {
            _toast.SetText(text);
            _toast.color = color;
            _toastIcon.SetText(icon);
            _toastIcon.color = color;
            _toastLeft = 1.3f;
            _toastRoot.gameObject.SetActive(true);
        }

        /// <summary>A quick "boing" on the sparks counter when gems land.</summary>
        public void PulseSparks() => _pillPulse = 1f;

        /// <summary>Gem streak: shown from 5, pops on every 5th.</summary>
        public void SetCombo(int combo)
        {
            if (combo == _shownCombo) return;
            _shownCombo = combo;
            var show = combo >= 5;
            if (_comboRoot.gameObject.activeSelf != show) _comboRoot.gameObject.SetActive(show);
            if (!show) return;
            _combo.SetText("×{0}", combo);
            if (combo % 5 == 0) _comboPulse = 1f;
        }

        void SyncPulses(float dt)
        {
            _pillPulse = Mathf.MoveTowards(_pillPulse, 0f, dt * 6f);
            var p = 1f + 0.12f * Mathf.Sin(_pillPulse * Mathf.PI);
            _pill.localScale = new Vector3(p, p, 1f);
            _comboPulse = Mathf.MoveTowards(_comboPulse, 0f, dt * 4f);
            var c = 1f + 0.3f * Mathf.Sin(_comboPulse * Mathf.PI);
            _comboRoot.localScale = new Vector3(c, c, 1f);
        }

        void SyncToast(float dt)
        {
            if (_toastLeft <= 0f) return;
            _toastLeft -= dt;
            var age = 1.3f - _toastLeft;
            // Pop in (overshoot), hold, fade out.
            var s = age < 0.18f ? Mathf.Lerp(0.6f, 1.12f, age / 0.18f) : Mathf.Lerp(1.12f, 1f, Mathf.Clamp01((age - 0.18f) / 0.15f));
            _toastRoot.localScale = new Vector3(s, s, 1f);
            var a = Mathf.Clamp01(_toastLeft / 0.35f);
            _toast.alpha = a;
            _toastIcon.alpha = a;
            if (_toastLeft <= 0f) _toastRoot.gameObject.SetActive(false);
        }

        /// <summary>Whether the tutorial prompt card is on screen (tests).</summary>
        public bool PromptVisible => _promptCard != null && _promptCard.activeSelf;

        public void Sync(RunSession session)
        {
            SyncToast(Time.unscaledDeltaTime);
            SyncPulses(Time.unscaledDeltaTime);
            var sim = session.Sim;
            var running = session.Phase == SessionPhase.Running;
            SetActive(_pauseButton, running && !session.Paused);
            SetActive(_pauseMenu, running && session.Paused);
            SetActive(_revive, session.Phase == SessionPhase.RevivePrompt);
            SetActive(_waiting, session.Phase == SessionPhase.AwaitingAd);
            if (sim == null) return;

            var d = (int)sim.Distance;
            if (d != _shownDistance) { _shownDistance = d; _distance.SetText("{0}<size=40%> m</size>", d); }
            var best = session.Start.BestDistance;
            var delta = best > 0 ? best - d : int.MaxValue;
            if (delta != _shownBestDelta)
            {
                _shownBestDelta = delta;
                if (best <= 0) _bestLine.SetText("");
                else if (delta > 0) _bestLine.SetText("{0} m to your best", delta);
                else _bestLine.SetText("<color=#3DFFA2>New best!</color>");
            }
            if (sim.Sparks != _shownSparks) { _shownSparks = sim.Sparks; _sparks.SetText("{0}", sim.Sparks); }

            SetTimer(0, sim.MagnetLeft, PowerUpRules.Duration(PowerUpKind.Magnet, sim.Upgrades));
            SetTimer(1, sim.ShieldLeft, PowerUpRules.Duration(PowerUpKind.Shield, sim.Upgrades));
            SetTimer(2, sim.DoubleLeft, PowerUpRules.Duration(PowerUpKind.DoubleSparks, sim.Upgrades));

            var prompt = session.Tutorial != null && !session.Tutorial.Completed ? session.Tutorial.Prompt : null;
            // Never on top of the pause menu or the revive card (negative test N2 found the overlap).
            SetActive(_promptCard, prompt != null && running && !session.Paused);
            if (prompt != null && !ReferenceEquals(prompt, _shownPrompt))
            {
                _shownPrompt = prompt;
                _prompt.SetText(prompt);
                var req = session.Tutorial.Required;
                _promptIcon.SetText(req == GameFactory.Core.Input.SwipeDirection.Left ? Icons.Left
                    : req == GameFactory.Core.Input.SwipeDirection.Right ? Icons.Right
                    : req == GameFactory.Core.Input.SwipeDirection.Up ? Icons.Up
                    : req == GameFactory.Core.Input.SwipeDirection.Down ? Icons.Down : Icons.Check);
                _promptIcon.color = req == GameFactory.Core.Input.SwipeDirection.None ? Success : Amber;
            }

            if (session.Phase == SessionPhase.RevivePrompt)
            {
                var c = Mathf.CeilToInt(session.PromptLeft);
                if (c != _shownCountdown)
                {
                    _shownCountdown = c;
                    _countdown.SetText("{0}", c);
                    _reviveDistance.SetText("{0} m", d);
                    _reviveSparks.SetText("Your {0} sparks stay with you.", sim.Sparks);
                    _reviveCause.SetText(sim.Caught ? "The Warden caught you" : CauseText(sim.CrashedInto));
                    if (best > 0 && best > d) _reviveBest.SetText("So close. Your best is {0} m.", best);
                    else _reviveBest.SetText(best > 0 ? "You're past your best. Keep going!" : "");
                }
                _ringFill.fillAmount = session.PromptLeft / RunSession.RevivePromptSeconds;
            }
            else _shownCountdown = -1;
        }

        static string CauseText(Obstacle o)
        {
            switch (o)
            {
                case Obstacle.Crate: return "Tripped over a crate stack";
                case Obstacle.Banner: return "Caught by a hanging sign";
                case Obstacle.Van: return "Ran into a parked van";
                case Obstacle.Cart: return "Hit by a rolling cart";
                case Obstacle.Tram:
                case Obstacle.TramRamp: return "Ran into a parked tram";
                default: return "Bumped into a market stall";
            }
        }

        static void SetActive(GameObject g, bool on) { if (g.activeSelf != on) g.SetActive(on); }

        void SetTimer(int i, float left, float total)
        {
            var on = left > 0f;
            SetActive(_timerRoot[i], on);
            if (on) _timerFill[i].fillAmount = left / total;
        }

        // ---------- builders ----------

        static Sprite MakeRoundedSprite(int size, int radius)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                var cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                var dist = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                var a = Mathf.Clamp01(radius - dist + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        static Sprite MakeRingSprite(int size, float thickness)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            var r = size / 2f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                var a = Mathf.Clamp01((1f - d) * r) * Mathf.Clamp01((d - (1f - thickness)) * r);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static RectTransform Rect(Component parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent.transform, false);
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }

        Image Panel(Component parent, string name, Color c, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            var r = Rect(parent, name, anchor, anchor, pos, size);
            r.pivot = pivot;
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = _round;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.9f;
            img.color = c;
            return img;
        }

        Image Img(Component parent, string name, Sprite s, Color c, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            var r = Rect(parent, name, anchor, anchor, pos, size);
            r.pivot = pivot;
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = s;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        TextMeshProUGUI Text(Component parent, string name, TMP_FontAsset font, float size, Color color, Vector2 anchor, Vector2 pos, Vector2 box, TextAlignmentOptions align, Vector2? pivot = null)
        {
            var r = Rect(parent, name, anchor, anchor, pos, box);
            r.pivot = pivot ?? new Vector2(align == TextAlignmentOptions.TopLeft || align == TextAlignmentOptions.Left ? 0 : 0.5f, align == TextAlignmentOptions.TopLeft ? 1 : 0.5f);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            if (font == _a.font && _a.fontShadow != null) t.fontSharedMaterial = _a.fontShadow;
            else if (font == _a.displayFont && _a.displayFontShadow != null) t.fontSharedMaterial = _a.displayFontShadow;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        TextMeshProUGUI Icon(Component parent, string glyph, float size, Color color, Vector2 anchor, Vector2 pos)
        {
            var t = Text(parent, "Icon", _a.iconFont, size, color, anchor, pos, new Vector2(size * 1.3f, size * 1.3f), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f));
            t.SetText(glyph);
            return t;
        }

        void AddButton(GameObject go, Action onClick)
        {
            var b = go.AddComponent<Button>();
            b.targetGraphic = go.GetComponent<Image>();
            b.onClick.AddListener(() => onClick());
        }

        /// <summary>Magenta button with a darker "pressable" base, like the menus.</summary>
        void PrimaryButton(Component parent, string name, string label, Vector2 anchor, Vector2 size, Action onClick, Vector2? pos = null, string icon = null)
        {
            var p = pos ?? Vector2.zero;
            Panel(parent, name + "Base", MagentaShade, anchor, p + new Vector2(0, -16), size, new Vector2(0.5f, anchor.y == 0 ? 0 : 0.5f));
            var face = Panel(parent, name, Magenta, anchor, p, size, new Vector2(0.5f, anchor.y == 0 ? 0 : 0.5f));
            var text = Text(face.rectTransform, "Label", _a.displayFont, 60, Ink, new Vector2(0.5f, 0.5f), icon != null ? new Vector2(34, 0) : Vector2.zero, size, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f));
            text.SetText(label);
            if (icon != null) Icon(face, icon, 48, Ink, new Vector2(0.5f, 0.5f), new Vector2(-text.GetPreferredValues(label).x / 2f - 10f, 0));
            AddButton(face.gameObject, onClick);
        }

        void TextButton(Component parent, string name, string label, Vector2 anchor, Action onClick, Vector2? pos = null, Color? color = null)
        {
            var r = Rect(parent, name, anchor, anchor, pos ?? Vector2.zero, new Vector2(820, 120));
            r.pivot = new Vector2(0.5f, anchor.y == 0 ? 0 : 0.5f);
            var hit = r.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var t = Text(r, "Label", _a.font, 46, color ?? Muted, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 120), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f));
            t.SetText(label);
            AddButton(r.gameObject, onClick);
        }

        static GameObject Overlay(RectTransform parent, string name, Color c)
        {
            var r = Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = r.gameObject.AddComponent<Image>();
            img.color = c;
            return r.gameObject;
        }
    }

    /// <summary>Keeps HUD elements out of notches and gesture bars.</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        UnityEngine.Rect _applied;

        void Update()
        {
            var safe = Screen.safeArea;
            if (safe == _applied || Screen.width == 0) return;
            _applied = safe;
            var r = (RectTransform)transform;
            r.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            r.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
