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
        public Canvas canvas;
        public Image fuelFill;
        public RectTransform fuelFillRect;
        public Text fuelLabel, tankLabel, debugText, controls, crosshair, hitText;
        public Image dashCooldownFill;
        public GameObject debugPanel;
        public bool showDebugStats;
        public GameObject MenuRoot { get; private set; }
        private Text menuStats, mode, palmText, rules, presetFeedback;
        private readonly Button[] presetButtons = new Button[HandPresetStore.SlotCount];
        private readonly Text[] presetLabels = new Text[HandPresetStore.SlotCount];
        private float presetFeedbackUntil;
        private readonly Button[] fingers = new Button[4];
        private readonly Text[] fingerText = new Text[4];
        private Button thumb;
        private Text thumbText;
        private readonly Color ink = new Color(0.9f, 0.95f, 0.97f);
        private readonly Color muted = new Color(0.53f, 0.64f, 0.69f);
        private readonly Color mint = new Color(0.27f, 0.93f, 0.75f);
        private readonly Color orange = new Color(1f, 0.59f, 0.31f);
        private Transform ui;
        private void OnValidate()
        {
            if (debugPanel && !Application.isPlaying) debugPanel.SetActive(showDebugStats);
        }
        private void Start()
        {
            if (!canvas) canvas = GetComponentInChildren<Canvas>();
            if (!canvas)
            {
                Debug.LogError("HandMenu requires a Canvas assigned in the scene.", this);
                enabled = false;
                return;
            }
            ui = canvas.transform;
            EnsureEventSystem();
            if (!fuelFill || !fuelFillRect || !fuelLabel || !tankLabel || !debugPanel || !debugText)
            {
                Debug.LogError("Assign the fuel HUD and debug panel references on HandMenu in the Inspector.", this);
                enabled = false;
                return;
            }
            if (crosshair) crosshair.text = "+";
            BuildMenu();
        }
        internal static void EnsureEventSystem()
        {
            if (EventSystem.current) return;
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        private void BuildMenu()
        {
            Transform root = canvas.transform.Find("Hand Menu Overlay");
            if (!root)
            {
                Debug.LogError("Canvas is missing the Hand Menu Overlay scene object.", this);
                enabled = false;
                return;
            }
            MenuRoot = root.gameObject;
            for (int i = 0; i < fingers.Length; i++)
            {
                Transform buttonObject = root.Find($"Hand Menu Panel/Finger Button {i + 1}");
                if (!buttonObject) { Debug.LogError($"Canvas is missing Finger Button {i + 1}.", this); enabled = false; return; }
                fingers[i] = buttonObject.GetComponent<Button>();
                fingerText[i] = buttonObject.GetComponentInChildren<Text>();
                int index = i;
                fingers[i].onClick.AddListener(() => weapon.Hand.TryToggle(index));
            }
            palmText = root.Find("Hand Menu Panel/Palm/Palm Label")?.GetComponent<Text>();
            thumb = root.Find("Hand Menu Panel/Thumb Button")?.GetComponent<Button>();
            thumbText = root.Find("Hand Menu Panel/Thumb Button/Thumb Label")?.GetComponent<Text>();
            mode = root.Find("Hand Menu Panel/Menu Mode")?.GetComponent<Text>();
            menuStats = root.Find("Hand Menu Panel/Menu Stats")?.GetComponent<Text>();
            rules = root.Find("Hand Menu Panel/Rules Text")?.GetComponent<Text>();
            Button resume = root.Find("Hand Menu Panel/Resume Button")?.GetComponent<Button>();
            if (!palmText || !thumb || !thumbText || !mode || !menuStats || !rules || !resume)
            {
                Debug.LogError("Canvas is missing one or more Hand Menu scene references.", this);
                enabled = false;
                return;
            }
            // Button graphics were authored with raycastTarget disabled, so the
            // EventSystem could not hit them even though the Buttons were enabled.
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.targetGraphic is Graphic graphic) graphic.raycastTarget = true;
            thumb.onClick.AddListener(() => weapon.Hand.ThumbActive = !weapon.Hand.ThumbActive);
            resume.onClick.AddListener(() => player.SetMenuOpen(false));
            presetFeedback = root.Find("Hand Menu Panel/Menu Footer")?.GetComponent<Text>();
            for (int i = 0; i < HandPresetStore.SlotCount; i++)
            {
                int digit = (i + 1) % HandPresetStore.SlotCount;
                Transform slot = root.Find($"Hand Menu Panel/Preset Button {digit}");
                if (!slot)
                {
                    Debug.LogError($"Canvas is missing Preset Button {digit}.", this);
                    enabled = false;
                    return;
                }
                presetButtons[digit] = slot.GetComponent<Button>();
                presetLabels[digit] = slot.GetComponentInChildren<Text>();
                presetButtons[digit].onClick.AddListener(() => weapon.SavePreset(digit));
            }
            weapon.PresetSaved += ShowPresetSaved;
        }
        private void ShowPresetSaved(int digit)
        {
            if (presetFeedback) presetFeedback.text = $"GUARDADO EN [{digit}] · En partida, pulsá {digit} para activarlo.";
            presetFeedbackUntil = Time.unscaledTime + 3f;
        }
        private void OnDestroy()
        {
            if (weapon) weapon.PresetSaved -= ShowPresetSaved;
        }
        private void LateUpdate()
        {
            if (!MenuRoot || weapon.Hand == null) return;
            MenuRoot.SetActive(player.IsMenuOpen && !player.IsPauseMenuOpen);
            if (debugPanel.activeSelf != showDebugStats) debugPanel.SetActive(showDebugStats);
            var stats = weapon.Stats;
            var hand = weapon.Hand;
            var blast = weapon.settings.EvaluateExplosion(stats.Damage);
            Color accent = stats.Hitscan ? mint : orange;
            float fuelFraction = Mathf.Clamp01(weapon.FuelFraction);
            fuelFillRect.anchorMin = Vector2.zero;
            fuelFillRect.anchorMax = new Vector2(fuelFraction, 1f);
            fuelFillRect.offsetMin = fuelFillRect.offsetMax = Vector2.zero;
            fuelFill.color = fuelFraction <= 0.25f ? orange : mint;
            if (dashCooldownFill)
            {
                dashCooldownFill.transform.parent.gameObject.SetActive(!player.IsMenuOpen);
                float dashFraction = player.DashCooldownFraction;
                dashCooldownFill.rectTransform.localScale = new Vector3(dashFraction, 1f, 1f);
                dashCooldownFill.color = dashFraction >= 1f ? mint : orange;
            }
            fuelLabel.text = "COMBUSTIBLE";
            tankLabel.text = $"{weapon.Fuel:0}/{weapon.tankCapacity:0}   ·   TANQUES ×{weapon.SpareTankCount}";
            if (controls) controls.text = $"{player.settings.forwardKey}{player.settings.leftKey}{player.settings.backwardKey}{player.settings.rightKey} MOVER   {player.settings.dashKey} DASH   CLIC DISPARAR   R RECARGAR   CTRL INSPECCIONAR   1–9 / 0 CONFIG.   {player.settings.menuKey} MANO";
            debugText.text = $"{(stats.Hitscan ? "RAYCAST" : "PROYECTIL")}  /  {hand.ActiveCount} DEDO(S)\nDAÑO {stats.Damage:0.##}  ·  ESPERA {stats.Interval:0.##} s  ·  VELOCIDAD {weapon.settings.moveSpeed:0.##} m/s\nCOSTO {weapon.settings.EvaluateFuelCost(stats, weapon.fuelPerDamage):0.##} COMBUSTIBLE / DISPARO";
            if (!stats.Hitscan) debugText.text += $"\nÁREA {blast.Damage:0.##} DAÑO / {blast.Radius:0.##} m";
            if (hitText) hitText.text = Time.time - weapon.LastHitTime < 0.5f ? $"IMPACTO  -{weapon.LastHitDamage:0.##}" : "";
            if (!MenuRoot.activeSelf) return;
            for (int digit = 0; digit < HandPresetStore.SlotCount; digit++)
            {
                HandPreset preset = weapon.Presets.Get(digit);
                bool matches = preset.Matches(hand);
                presetButtons[digit].image.color = matches ? mint : new Color(0.13f, 0.2f, 0.23f);
                presetLabels[digit].color = matches ? new Color(0.02f, 0.09f, 0.08f) : ink;
                string group = "";
                for (int finger = 0; finger < 4; finger++)
                    if ((preset.ActiveMask & (1 << finger)) != 0) group += (finger + 1).ToString();
                presetLabels[digit].text = preset.IsSaved
                    ? $"[{digit}]  {(matches ? "ACTUAL" : "GUARDADO")}\n{group} · {(preset.ThumbActive ? "RAYO" : "PROY.")}"
                    : $"[{digit}]\nVACÍO";
            }
            if (presetFeedback && Time.unscaledTime >= presetFeedbackUntil)
                presetFeedback.text = "Se conservan al reiniciar. En partida: 1–9 / 0 para cambiar.";
            mode.text = stats.Hitscan ? "RAYCAST / INSTANTÁNEO" : "PROYECTIL / EXPLOSIVO";
            mode.color = accent;
            rules.text = $"Siempre queda al menos un cañón activo.\nLos dedos activos forman un solo grupo.\nPulgar activo: raycast, valores base.\nProyectil: daño ×{weapon.settings.projectileDamageMultiplier:0.##}, espera ×{weapon.settings.projectileCooldownMultiplier:0.##}, combustible −{weapon.settings.projectileFuelEfficiency:P0}.\nExplosión: {weapon.settings.explosionDamageFraction:P0} del daño directo.";
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

    }
}


