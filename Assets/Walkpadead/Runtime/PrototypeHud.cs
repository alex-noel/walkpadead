using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Walkpadead.Core;

namespace Walkpadead.Game
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        private static readonly Color Paper = new Color(0.88f, 0.91f, 0.86f);
        private static readonly Color Muted = new Color(0.54f, 0.64f, 0.63f);
        private static readonly Color Mint = new Color(0.65f, 0.88f, 0.45f);
        private static readonly Color Amber = new Color(0.97f, 0.61f, 0.32f);
        private Font font;
        private Text speed;
        private Text inputLabel;
        private Text timer;
        private Text health;
        private Text stats;
        private Text ammo;
        private Text signal;
        private Text pursuit;
        private Image pursuitFill;
        private Image healthFill;
        private Text crosshair;
        private GameObject overlay;
        private Text panelTitle;
        private Text panelBody;
        private Text panelStatus;
        private Text buttonLabel;
        private Button mainButton;
        private GameObject reconnectButton;
        private Text bonusStatus;
        private Text bonusToggleLabel;
        private DesktopGame game;

        public void Build(DesktopGame game)
        {
            this.game = game;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            if (game.IsQuest)
            {
                canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = game.ViewCamera;
                var rect = (RectTransform)transform;
                rect.SetParent(game.ViewCamera.transform, false);
                rect.sizeDelta = new Vector2(1440, 900);
                rect.localScale = Vector3.one * 0.0012f;
                rect.localPosition = new Vector3(0, 0, 1.6f);
                gameObject.AddComponent<TrackedDeviceRaycaster>();
            }
            else gameObject.AddComponent<GraphicRaycaster>();
            var eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var events = new GameObject("UI events");
                eventSystem = events.AddComponent<EventSystem>();
            }
            var module = eventSystem.GetComponent<InputSystemUIInputModule>() ?? eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            if (game.IsQuest) game.Quest.BindUI(module); else module.AssignDefaultActions();

            var topLeft = new Vector2(0, 1);
            var topRight = Vector2.one;
            var bottomLeft = Vector2.zero;
            var bottomRight = new Vector2(1, 0);
            Label(transform, "WALKPADEAD", 27, Paper, topLeft, new Vector2(38, -30), new Vector2(400, 45), TextAnchor.UpperLeft, true);
            Label(transform, game.IsQuest ? "02  /  EXTRACTION YARD" : "01  /  SERVICE ROAD", 12, Muted, topLeft, new Vector2(40, -76), new Vector2(300, 25));
            speed = Label(transform, "", 30, Mint, topLeft, new Vector2(40, -120), new Vector2(350, 50));
            inputLabel = Label(transform, "WALKING INPUT", 11, Muted, topLeft, new Vector2(42, -169), new Vector2(330, 25));
            timer = Label(transform, "02:00", 42, Paper, topRight, new Vector2(-38, -30), new Vector2(300, 60), TextAnchor.UpperRight);
            Label(transform, "UNTIL EXTRACTION", 11, Muted, topRight, new Vector2(-40, -93), new Vector2(300, 25), TextAnchor.UpperRight);
            bonusStatus = Label(transform, "", 14, Mint, new Vector2(0.5f, 1), new Vector2(0, -38), new Vector2(570, 70), TextAnchor.UpperCenter);

            health = Label(transform, "", 25, Paper, bottomLeft, new Vector2(40, 126), new Vector2(280, 40));
            healthFill = Bar(transform, bottomLeft, new Vector2(40, 75), new Vector2(215, 4), Mint);
            stats = Label(transform, "", 13, Muted, bottomLeft, new Vector2(40, 65), new Vector2(430, 30));
            ammo = Label(transform, "", 30, Paper, bottomRight, new Vector2(-40, 119), new Vector2(300, 50), TextAnchor.UpperRight);
            Label(transform, game.IsQuest ? "A  RELOAD     •     RIGHT TRIGGER  FIRE" : "R  RELOAD     •     LMB  FIRE", 11, Muted, bottomRight, new Vector2(-40, 64), new Vector2(320, 30), TextAnchor.UpperRight);
            signal = Label(transform, "", 12, Muted, bottomLeft, new Vector2(40, 28), new Vector2(600, 26));
            Label(transform, game.IsQuest ? "B  PAUSE" : "ESC  PAUSE", 11, Muted, bottomRight, new Vector2(-40, 28), new Vector2(230, 26), TextAnchor.UpperRight);
            pursuit = Label(transform, "", 13, Amber, new Vector2(0.5f, 0), new Vector2(0, 95), new Vector2(350, 26), TextAnchor.UpperCenter);
            pursuitFill = Bar(transform, new Vector2(0.5f, 0), new Vector2(0, 59), new Vector2(240, 4), Amber);
            crosshair = Label(transform, "+", 24, Paper, new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(40, 40), TextAnchor.MiddleCenter);

            overlay = new GameObject("Run menu", typeof(RectTransform));
            overlay.transform.SetParent(transform, false);
            var backdrop = overlay.AddComponent<Image>(); backdrop.color = new Color(0.018f, 0.038f, 0.043f, 0.88f);
            var full = (RectTransform)overlay.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one; full.offsetMin = full.offsetMax = Vector2.zero;
            var card = Rect("Briefing", overlay.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(650, game.IsQuest ? 620 : 550));
            Label(card, "W A L K P A D E A D    /    " + (game.IsQuest ? "QUEST PROTOTYPE" : "DESKTOP PROTOTYPE"), 12, Mint, topLeft, new Vector2(0, 0), new Vector2(650, 30));
            panelTitle = Label(card, "Keep moving.", 61, Paper, topLeft, new Vector2(-3, -50), new Vector2(650, 95), TextAnchor.UpperLeft, true);
            panelBody = Label(card, "", 20, Paper, topLeft, new Vector2(0, -158), new Vector2(640, 135));
            panelStatus = Label(card, "", 13, Amber, topLeft, new Vector2(0, -306), new Vector2(650, 40));
            mainButton = Button(card, "Start run", new Vector2(0, -367), new Vector2(310, 60), game.StartOrResume, out buttonLabel);
            reconnectButton = Button(card, "Reconnect input", new Vector2(335, -367), new Vector2(280, 60), game.Reconnect, out _).gameObject;
            Button(card, "Enable speed bonuses", new Vector2(0, -444), new Vector2(615, 44), game.ToggleSpeedBonuses, out bonusToggleLabel);
            if (game.IsQuest)
                Button(card, "Use offline simulator · 3.0 km/h", new Vector2(0, -502), new Vector2(615, 44), game.UseSimulator, out _);
            Label(card, game.IsQuest ? "LOOK  STEER  /  TRIGGER  FIRE  /  A  RELOAD  /  B  PAUSE\nX  START / RESUME  /  Y  SPEED BONUSES  /  POINT + TRIGGER  SELECT" :
                "MOUSE  AIM  /  LMB  SHOOT  /  R  RELOAD  /  ESC  PAUSE  /  B  SPEED BONUSES", 11, Muted,
                topLeft, new Vector2(0, game.IsQuest ? -568 : -514), new Vector2(650, 40));
        }

        public void Refresh(RunSession run, InputReading reading, Sidearm gun)
        {
            bonusStatus.text = (game.SpeedBonusesEnabled ? "±0.1 SPEED BONUSES ON" : "SPEED BONUSES OFF · " + (game.IsQuest ? "Y" : "B") + " TO ENABLE") + "\n" + game.BonusStatus;
            bonusToggleLabel.text = game.SpeedBonusesEnabled ? "Disable pad speed bonuses · " + game.SpeedRangeLabel :
                game.CanControlSpeed ? "Enable ±0.1 km/h bonuses · " + game.SpeedRangeLabel : "Speed bonuses unavailable · " + game.SpeedRangeLabel;
            speed.text = reading.Available ? $"{reading.SpeedMps * 3.6:0.0} <size=16>km/h</size>" : "— <size=16>km/h</size>";
            inputLabel.text = reading.SourceLabel.ToUpperInvariant();
            var remaining = Mathf.CeilToInt((float)(RunSession.DurationSeconds - run.ElapsedSeconds));
            timer.text = $"{remaining / 60:00}:{remaining % 60:00}";
            health.text = $"{Mathf.CeilToInt((float)run.Health):000} <size=12>HEALTH</size>";
            healthFill.rectTransform.localScale = new Vector3((float)run.Health / 100, 1, 1);
            stats.text = $"{run.DistanceM:0} m walked    /    {run.Kills:00} infected cleared";
            ammo.text = gun.Reloading ? "RELOADING" : $"{gun.Ammo:00}<size=17> / {Sidearm.MagazineSize:00}</size>";
            signal.text = "●  " + reading.Status.ToUpperInvariant();
            signal.color = reading.Available ? Muted : Amber;
            pursuit.text = $"HORDE   {run.HordeGapM:0.0} m BEHIND";
            pursuitFill.rectTransform.localScale = new Vector3(1 - (float)run.HordeGapM / 35, 1, 1);
            crosshair.text = gun.HitMarkerTime > 0 ? "×" : "+";
            crosshair.color = gun.HitMarkerTime > 0 ? Mint : Paper;
            var running = run.Phase == RunPhase.Running;
            overlay.SetActive(!running); crosshair.gameObject.SetActive(running && !game.IsQuest);
            if (running) return;
            var ended = run.Phase == RunPhase.Escaped || run.Phase == RunPhase.Defeated;
            mainButton.interactable = game.ControlsReady && (ended || reading.Available);
            reconnectButton.SetActive(!ended);
            panelStatus.color = reading.Available ? Mint : Amber;
            panelStatus.text = reading.Available ? $"{reading.SourceLabel.ToUpperInvariant()} READY   •   {reading.SpeedMps * 3.6:0.0} km/h" : reading.Status;
            if (!game.ControlsReady) { panelStatus.text = game.Quest.Status; panelStatus.color = Amber; }
            if (run.Phase == RunPhase.Ready)
            {
                panelTitle.text = "Keep moving.";
                panelBody.text = game.IsQuest ? "Two minutes to extraction. Look where you want to go.\nYour pad sets the pace; your right controller aims.\nPoint at a button and pull the trigger to select." :
                    "Two minutes to extraction.\nWalking puts distance between you and the horde.\nAim at the infected ahead. Clear a path.";
                buttonLabel.text = game.IsQuest ? "X  ·  Start run" : "Start run  →";
            }
            else if (run.Phase == RunPhase.Paused)
            {
                panelTitle.text = "Take a breath.";
                panelBody.text = run.PauseReason + ".\nThe street is paused.\nResume when the input is ready and so are you.";
                buttonLabel.text = game.IsQuest ? "X  ·  Resume run" : "Resume run  →";
            }
            else
            {
                panelTitle.text = run.Phase == RunPhase.Escaped ? "You made it." : "Overrun.";
                panelBody.text = $"{run.DistanceM:0} metres walked. {run.Kills} infected cleared.\n{run.ElapsedSeconds:0} seconds survived.\n" +
                    (run.Phase == RunPhase.Escaped ? "The extraction team has your back." : "Keep your pace. Watch the road ahead.");
                panelStatus.text = reading.SourceLabel.ToUpperInvariant() + " SESSION COMPLETE";
                buttonLabel.text = "Try again  →";
            }
        }

        private RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        private Text Label(Transform parent, string value, int size, Color color, Vector2 anchor,
            Vector2 position, Vector2 dimensions, TextAnchor alignment = TextAnchor.UpperLeft, bool bold = false)
        {
            var label = Rect("Label", parent, anchor, position, dimensions).gameObject.AddComponent<Text>();
            label.font = font; label.text = value;
            label.fontSize = game.IsQuest ? Mathf.Max(size, size <= 13 ? 16 : size <= 20 ? 23 : size) : size;
            label.color = color;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.alignment = alignment; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private Image Bar(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var root = Rect("Meter", parent, anchor, position, size);
            root.gameObject.AddComponent<Image>().color = new Color(0.3f, 0.4f, 0.4f, 0.4f);
            var fill = Rect("Fill", root, new Vector2(0, 0.5f), Vector2.zero, size).gameObject.AddComponent<Image>();
            fill.color = color; fill.raycastTarget = false;
            return fill;
        }

        private Button Button(Transform parent, string title, Vector2 position, Vector2 size,
            UnityEngine.Events.UnityAction action, out Text label)
        {
            var rect = Rect(title, parent, new Vector2(0, 1), position, size);
            var image = rect.gameObject.AddComponent<Image>(); image.color = Mint;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.disabledColor = new Color(0.3f, 0.4f, 0.35f, 0.65f); button.colors = colors;
            button.onClick.AddListener(action);
            label = Label(rect, title, 19, new Color(0.04f, 0.08f, 0.06f),
                new Vector2(0.5f, 0.5f), Vector2.zero, size, TextAnchor.MiddleCenter, true);
            return button;
        }
    }
}
