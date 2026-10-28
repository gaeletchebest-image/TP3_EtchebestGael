using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace HandShooter
{
    public sealed class HandMenu : MonoBehaviour
    {
        public PrototypePlayer player;
        public HandWeapon weapon;
        public GameObject MenuRoot { get; private set; }
        private Text hudStats, menuStats, mode, palmText, hitText, controls, rules;
        private readonly Button[] fingers = new Button[4];
        private readonly Text[] fingerText = new Text[4];
        private Button thumb;
        private Text thumbText;
        private Font font;
        private readonly Color ink = new Color(0.9f, 0.95f, 0.97f);
        private readonly Color muted = new Color(0.53f, 0.64f, 0.69f);
        private readonly Color mint = new Color(0.27f, 0.93f, 0.75f);
        private readonly Color orange = new Color(1f, 0.59f, 0.31f);
        private Transform ui;
        private void Start()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("Prototype UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1366, 768);
            scaler.matchWidthOrHeight = 0.5f;
            ui = canvas.transform;
            if (!EventSystem.current)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            RectTransform top = Box(ui, "Range HUD", 26, 24, 320, 72, new Color(0.03f, 0.07f, 0.09f, 0.9f));
            Label(top, "HAND / LAB", 16, 8, 290, 26, 20, mint);
            Label(top, "CAMPO DE PRUEBAS     /     PROTOTIPO 01", 16, 36, 290, 24, 11, muted);
            RectTransform bottom = Box(ui, "Weapon HUD", 26, -26, 460, 82, new Color(0.03f, 0.07f, 0.09f, 0.94f));
            bottom.anchorMin = bottom.anchorMax = new Vector2(0, 0);
            bottom.pivot = new Vector2(0, 1);
            bottom.anchoredPosition = new Vector2(26, 108);
            hudStats = Label(bottom, "", 16, 8, 430, 60, 17, ink);
            controls = Label(ui, "", 26, -25, 800, 24, 13, muted);
            var c = controls.rectTransform;
            c.anchorMin = c.anchorMax = new Vector2(0, 0); c.anchoredPosition = new Vector2(26, 28);
            var cross = Label(ui, "+", 0, 0, 32, 32, 25, mint);
            cross.alignment = TextAnchor.MiddleCenter;
            cross.rectTransform.anchorMin = cross.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            cross.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            cross.rectTransform.anchoredPosition = Vector2.zero;
            hitText = Label(ui, "", 0, 0, 240, 45, 19, orange);
            hitText.alignment = TextAnchor.MiddleCenter;
            hitText.rectTransform.anchorMin = hitText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            hitText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            hitText.rectTransform.anchoredPosition = new Vector2(0, -48);
            BuildMenu();
        }
        private void BuildMenu()
        {
            RectTransform overlay = Box(ui, "Hand menu", 0, 0, 0, 0, new Color(0.015f, 0.03f, 0.045f, 0.86f));
            overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            MenuRoot = overlay.gameObject;
            RectTransform panel = Box(overlay, "Panel", 0, 0, 980, 650, new Color(0.045f, 0.075f, 0.095f, 1));
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f); panel.anchoredPosition = Vector2.zero;
            Box(panel, "Accent", 0, 0, 980, 3, mint);
            Label(panel, "CONFIGURAR MANO", 34, 28, 700, 42, 32, ink);
            Label(panel, "Juntá los dedos para ganar potencia. Liberalos para disparar más rápido.", 36, 78, 900, 30, 17, muted);
            Label(panel, "01 / DEDOS PRINCIPALES", 36, 135, 420, 30, 15, mint);
            Label(panel, "02 / RESULTADO", 526, 135, 400, 30, 15, mint);
            string[] names = { "ÍNDICE", "MEDIO", "ANULAR", "MEÑIQUE" };
            float[] heights = { 158, 188, 170, 128 };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                fingers[i] = MakeButton(panel, names[i], 72 + i * 92, 372 - heights[i], 82, heights[i], () => weapon.Hand.TryToggle(index));
                fingerText[i] = fingers[i].GetComponentInChildren<Text>();
                fingerText[i].fontSize = 12;
                fingerText[i].alignment = TextAnchor.MiddleCenter;
            }
            RectTransform palm = Box(panel, "Palm", 66, 380, 374, 86, new Color(0.09f, 0.16f, 0.18f));
            palmText = Label(palm, "", 20, 12, 340, 62, 19, ink);
            thumb = MakeButton(panel, "Pulgar", 66, 482, 374, 72, () => weapon.Hand.ThumbActive = !weapon.Hand.ThumbActive);
            thumbText = thumb.GetComponentInChildren<Text>();
            thumbText.fontSize = 17;
            mode = Label(panel, "", 526, 184, 410, 40, 26, ink);
            menuStats = Label(panel, "", 526, 240, 410, 150, 20, ink);
            Label(panel, "REGLAS DE LA MANO", 526, 405, 410, 25, 13, mint);
            rules = Label(panel, "", 526, 441, 415, 102, 15, muted);
            Label(panel, "Los botones atenuados romperían el grupo activo.", 36, 574, 460, 42, 13, muted);
            MakeButton(panel, "VOLVER A DISPARAR", 526, 574, 410, 44, () => player.SetMenuOpen(false));
        }
        private void LateUpdate()
        {
            if (!MenuRoot || weapon.Hand == null) return;
            MenuRoot.SetActive(player.IsMenuOpen);
            var stats = weapon.Stats;
            var hand = weapon.Hand;
            var blast = weapon.settings.EvaluateExplosion(stats.Damage);
            Color accent = stats.Hitscan ? mint : orange;
            hudStats.text = $"{(stats.Hitscan ? "RAYCAST" : "PROYECTIL")}   /   {hand.ActiveCount} DEDO(S)\nDAÑO {stats.Damage:0.##}     ESPERA {stats.Interval:0.##} s";
            if (!stats.Hitscan) hudStats.text += $"\nÁREA {blast.Damage:0.##} DAÑO / {blast.Radius:0.##} m";
            controls.text = $"{player.settings.forwardKey}{player.settings.leftKey}{player.settings.backwardKey}{player.settings.rightKey}  MOVER     MOUSE  APUNTAR     CLIC  DISPARAR     {player.settings.menuKey}  MANO";
            hitText.text = Time.time - weapon.LastHitTime < 0.5f ? $"IMPACTO  -{weapon.LastHitDamage:0.##}" : "";
            if (!player.IsMenuOpen) return;
            mode.text = stats.Hitscan ? "RAYCAST / INSTANTÁNEO" : "PROYECTIL / EXPLOSIVO";
            mode.color = accent;
            rules.text = $"Siempre queda al menos un cañón activo.\nLos dedos activos forman un solo grupo.\nPulgar activo: raycast, valores base.\nProyectil: daño ×{weapon.settings.projectileDamageMultiplier:0.##}, espera ×{weapon.settings.projectileCooldownMultiplier:0.##}.\nExplosión: {weapon.settings.explosionDamageFraction:P0} del daño directo.";
            menuStats.text = $"{stats.Damage:0.##}    DAÑO POR DISPARO\n\n{stats.Interval:0.##} s    ENTRE DISPAROS\n{1f / stats.Interval:0.##}    DISPAROS / SEGUNDO";
            if (!stats.Hitscan) menuStats.text += $"\nEXPLOSIÓN {blast.Damage:0.##} DAÑO / {blast.Radius:0.##} m";
            palmText.text = $"{hand.ActiveCount:00} ACTIVO(S) / {4 - hand.ActiveCount:00} INACTIVO(S)\nUN SOLO GRUPO / UN CAÑÓN";
            string[] names = { "ÍNDICE", "MEDIO", "ANULAR", "MEÑIQUE" };
            for (int i = 0; i < 4; i++)
            {
                fingers[i].interactable = hand.CanToggle(i);
                fingers[i].image.color = hand.IsActive(i) ? mint : new Color(0.13f, 0.2f, 0.23f);
                fingerText[i].color = hand.IsActive(i) ? new Color(0.02f, 0.09f, 0.08f) : ink;
                fingerText[i].text = $"{i + 1:00}\n\n{names[i]}\n\n{(hand.IsActive(i) ? "ACTIVO" : "INACTIVO")}";
            }
            thumb.image.color = hand.ThumbActive ? mint : orange;
            thumbText.color = new Color(0.025f, 0.07f, 0.08f);
            thumbText.text = $"PULGAR {(hand.ThumbActive ? "ACTIVO" : "INACTIVO")}\n{(hand.ThumbActive ? "Raycast · daño y espera base" : $"Proyectil · daño ×{weapon.settings.projectileDamageMultiplier:0.##} / espera ×{weapon.settings.projectileCooldownMultiplier:0.##}")}";
        }
        private RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
        private RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color)
        { var rect = Rect(parent, name, x, y, w, h); var image = rect.gameObject.AddComponent<Image>(); image.color = color; return rect; }
        private Text Label(Transform parent, string text, float x, float y, float w, float h, int size, Color color)
        {
            var label = Rect(parent, "Label", x, y, w, h).gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = size; label.color = color;
            label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        private Button MakeButton(Transform parent, string text, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
        {
            var rect = Box(parent, text, x, y, w, h, new Color(0.13f, 0.24f, 0.26f));
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors; colors.highlightedColor = new Color(0.8f, 1f, 0.95f); colors.disabledColor = new Color(0.65f, 0.65f, 0.65f); button.colors = colors;
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            var label = Label(rect, text, 4, 4, w - 8, h - 8, 16, ink); label.alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(action); return button;
        }
    }
}


