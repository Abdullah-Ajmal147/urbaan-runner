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
    /// In-run HUD (HANDOVER §4.2: the HUD lives in Unity): distance, sparks, power-up timers, pause,
    /// pause menu and the revive prompt. Built in code; text updates use TMP's non-allocating SetText.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        static readonly Color Night = new Color32(0x0E, 0x12, 0x30, 0xFF);
        static readonly Color Surface = new Color32(0x1A, 0x20, 0x50, 0xF0);
        static readonly Color Magenta = new Color32(0xFF, 0x3D, 0xA6, 0xFF);
        static readonly Color Cyan = new Color32(0x2E, 0xE6, 0xFF, 0xFF);
        static readonly Color Amber = new Color32(0xFF, 0xB5, 0x47, 0xFF);
        static readonly Color TextColor = new Color32(0xF4, 0xF6, 0xFF, 0xFF);
        static readonly Color Success = new Color32(0x3D, 0xFF, 0xA2, 0xFF);

        public event Action PausePressed;
        public event Action ResumePressed;
        public event Action QuitPressed;
        public event Action ReviveYes;
        public event Action ReviveNo;

        TMP_FontAsset _font;
        TextMeshProUGUI _distance, _sparks, _countdown;
        GameObject _pauseButton, _pauseMenu, _revivePrompt, _waiting;
        readonly Image[] _timerFill = new Image[3];
        readonly GameObject[] _timerRoot = new GameObject[3];
        int _shownDistance = -1, _shownSparks = -1, _shownCountdown = -1;

        public void Init(TMP_FontAsset font)
        {
            _font = font;
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
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }

            var safe = Rect(root, "Safe", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeArea>();

            // Top bar
            _distance = Label(safe, "Distance", new Vector2(0, 1), new Vector2(40, -40), 64, TextAlignmentOptions.TopLeft, TextColor);
            _sparks = Label(safe, "Sparks", new Vector2(1, 1), new Vector2(-200, -40), 64, TextAlignmentOptions.TopRight, Cyan);
            _pauseButton = Button(safe, "Pause", "II", new Vector2(1, 1), new Vector2(-60, -80), new Vector2(120, 120), Surface, () => PausePressed?.Invoke()).gameObject;

            // Power-up timers under the distance
            var labels = new[] { "MAGNET", "SHIELD", "2× SPARKS" };
            var colors = new[] { Magenta, Success, Amber };
            for (var i = 0; i < 3; i++)
            {
                var r = Rect(safe, labels[i], new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -140 - i * 70), new Vector2(300, 56));
                r.pivot = new Vector2(0, 1);
                Panel(r, Surface);
                var fill = Panel(Rect(r, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), colors[i]);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.sprite = WhiteSprite();
                _timerFill[i] = fill;
                var t = Label(r, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, 30, TextAlignmentOptions.Center, Night);
                t.rectTransform.sizeDelta = new Vector2(300, 56);
                t.SetText(labels[i]);
                _timerRoot[i] = r.gameObject;
                r.gameObject.SetActive(false);
            }

            // Pause menu
            _pauseMenu = Overlay(root, "PauseMenu");
            var pm = _pauseMenu.GetComponent<RectTransform>();
            Label(pm, "Title", new Vector2(0.5f, 0.62f), Vector2.zero, 96, TextAlignmentOptions.Center, TextColor).SetText("Paused");
            Button(pm, "Resume", "Resume", new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(640, 150), Magenta, () => ResumePressed?.Invoke());
            Button(pm, "Quit", "Quit run", new Vector2(0.5f, 0.33f), Vector2.zero, new Vector2(640, 150), Surface, () => QuitPressed?.Invoke());
            _pauseMenu.SetActive(false);

            // Revive prompt (HANDOVER §3.8)
            _revivePrompt = Overlay(root, "RevivePrompt");
            var rp = _revivePrompt.GetComponent<RectTransform>();
            Label(rp, "Title", new Vector2(0.5f, 0.66f), Vector2.zero, 96, TextAlignmentOptions.Center, TextColor).SetText("Continue?");
            _countdown = Label(rp, "Countdown", new Vector2(0.5f, 0.56f), Vector2.zero, 140, TextAlignmentOptions.Center, Amber);
            Button(rp, "Yes", "Watch ad to continue", new Vector2(0.5f, 0.42f), Vector2.zero, new Vector2(760, 160), Magenta, () => ReviveYes?.Invoke());
            Button(rp, "No", "No thanks", new Vector2(0.5f, 0.30f), Vector2.zero, new Vector2(760, 130), Surface, () => ReviveNo?.Invoke());
            _revivePrompt.SetActive(false);

            _waiting = Overlay(root, "Waiting");
            Label(_waiting.GetComponent<RectTransform>(), "Text", new Vector2(0.5f, 0.5f), Vector2.zero, 64, TextAlignmentOptions.Center, TextColor).SetText("Loading ad…");
            _waiting.SetActive(false);
        }

        public void Sync(RunSession session)
        {
            var sim = session.Sim;
            var running = session.Phase == SessionPhase.Running;
            _pauseButton.SetActive(running && !session.Paused);
            _pauseMenu.SetActive(running && session.Paused);
            _revivePrompt.SetActive(session.Phase == SessionPhase.RevivePrompt);
            _waiting.SetActive(session.Phase == SessionPhase.AwaitingAd);
            if (sim == null) return;

            var d = (int)sim.Distance;
            if (d != _shownDistance) { _shownDistance = d; _distance.SetText("{0} m", d); }
            if (sim.Sparks != _shownSparks) { _shownSparks = sim.Sparks; _sparks.SetText("{0}", sim.Sparks); }

            SetTimer(0, sim.MagnetLeft, PowerUpRules.Duration(PowerUpKind.Magnet, sim.Upgrades));
            SetTimer(1, sim.ShieldLeft, PowerUpRules.Duration(PowerUpKind.Shield, sim.Upgrades));
            SetTimer(2, sim.DoubleLeft, PowerUpRules.Duration(PowerUpKind.DoubleSparks, sim.Upgrades));

            if (session.Phase == SessionPhase.RevivePrompt)
            {
                var c = Mathf.CeilToInt(session.PromptLeft);
                if (c != _shownCountdown) { _shownCountdown = c; _countdown.SetText("{0}", c); }
            }
        }

        void SetTimer(int i, float left, float total)
        {
            var on = left > 0f;
            if (_timerRoot[i].activeSelf != on) _timerRoot[i].SetActive(on);
            if (on) _timerFill[i].fillAmount = left / total;
        }

        // ---------- tiny UI builders ----------

        static Sprite _white;
        static Sprite WhiteSprite()
        {
            if (_white == null)
                _white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            return _white;
        }

        static RectTransform Rect(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = go.GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }

        static Image Panel(RectTransform r, Color c)
        {
            var img = r.gameObject.AddComponent<Image>();
            img.color = c;
            return img;
        }

        TextMeshProUGUI Label(RectTransform parent, string name, Vector2 anchor, Vector2 pos, float size, TextAlignmentOptions align, Color color)
        {
            var r = Rect(parent, name, anchor, anchor, pos, new Vector2(900, size * 1.4f));
            r.pivot = new Vector2(align == TextAlignmentOptions.TopLeft ? 0 : align == TextAlignmentOptions.TopRight ? 1 : 0.5f, align == TextAlignmentOptions.Center ? 0.5f : 1f);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        Button Button(RectTransform parent, string name, string text, Vector2 anchor, Vector2 pos, Vector2 size, Color bg, Action onClick)
        {
            var r = Rect(parent, name, anchor, anchor, pos, size);
            var img = Panel(r, bg);
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick());
            var t = Label(r, "Text", new Vector2(0.5f, 0.5f), Vector2.zero, Mathf.Min(56, size.y * 0.42f), TextAlignmentOptions.Center, TextColor);
            t.rectTransform.sizeDelta = size;
            t.SetText(text);
            return b;
        }

        static GameObject Overlay(RectTransform parent, string name)
        {
            var r = Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Panel(r, new Color(0.055f, 0.07f, 0.19f, 0.88f));
            return r.gameObject;
        }
    }

    /// <summary>Keeps HUD elements out of notches and gesture bars.</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        Rect _applied;

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
