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
        public Image refuelPreview;
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
            EnsureRefuelPreview();
            BuildMenu();
        }
        public void EnsureRefuelPreview()
        {
            if (refuelPreview || !fuelFillRect) return;
            var preview = new GameObject("Refuel Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            preview.transform.SetParent(fuelFillRect.parent, false);
            preview.transform.SetSiblingIndex(fuelFillRect.GetSiblingIndex());
            refuelPreview = preview.GetComponent<Image>();
            refuelPreview.raycastTarget = false;
            refuelPreview.enabled = false;
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
                fingers[i].onClick.AddListener(() => ShooterAudio.Ui(weapon.Hand.TryToggle(index) ? SoundCue.FingerToggle : SoundCue.Invalid));
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
                {
                    UiAudioFeedback.Attach(button);
                    if (button.targetGraphic is Graphic graphic) graphic.raycastTarget = true;
                }
            thumb.onClick.AddListener(weapon.CycleMode);
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
            bool equipped = weapon.IsEquipped;
            bool unlimitedFuel = weapon.HasUnlimitedFuel;
            MenuRoot.SetActive(equipped && player.IsMenuOpen && !player.IsPauseMenuOpen);
            debugPanel.SetActive(equipped && showDebugStats);
            fuelFillRect.parent.gameObject.SetActive(equipped);
            fuelLabel.gameObject.SetActive(equipped);
            tankLabel.gameObject.SetActive(equipped);
            if (crosshair) crosshair.gameObject.SetActive(equipped);
            if (hitText) hitText.gameObject.SetActive(equipped);
            if (dashCooldownFill)
            {
                dashCooldownFill.transform.parent.gameObject.SetActive(player.DashUnlocked && !player.IsMenuOpen);
                float dashFraction = player.DashCooldownFraction;
                dashCooldownFill.rectTransform.localScale = new Vector3(dashFraction, 1f, 1f);
                dashCooldownFill.color = dashFraction >= 1f ? mint : orange;
            }
            if (controls)
            {
                var settings = player.settings;
                string movement = $"{settings.forwardKey}{settings.leftKey}{settings.backwardKey}{settings.rightKey} MOVER";
                if (player.DashUnlocked) movement += $"   {settings.dashKey} DASH";
                string reload = unlimitedFuel ? "" : "   R RECARGAR";
                controls.text = equipped
                    ? $"{movement}   CLIC DISPARAR{reload}   CTRL INSPECCIONAR   1–9 / 0 CONFIG.   {settings.menuKey} MANO"
                    : $"{movement}   ACERCATE A LA MANO PARA EQUIPARLA";
            }
            if (!equipped) return;

            var stats = weapon.Stats;
            var hand = weapon.Hand;
            bool grenade = stats.Mode == WeaponMode.Grenade;
            var blast = grenade ? weapon.settings.EvaluateGrenadeExplosion(stats.Damage) : weapon.settings.EvaluateExplosion(stats.Damage);
            Color accent = grenade ? weapon.settings.grenadeColor : stats.Hitscan ? mint : orange;
            float fuelFraction = Mathf.Clamp01(weapon.FuelFraction);
            fuelFillRect.anchorMin = Vector2.zero;
            fuelFillRect.anchorMax = new Vector2(fuelFraction, 1f);
            fuelFillRect.offsetMin = fuelFillRect.offsetMax = Vector2.zero;
            fuelFill.color = fuelFraction <= 0.25f ? orange : mint;
            var station = weapon.ActiveRefuelingStation;
            if (refuelPreview)
            {
                refuelPreview.enabled = station;
                var rect = refuelPreview.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(weapon.RefuelPreviewFraction, 1f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                float brightness = station ? station.settings.previewBrightness : 0.55f;
                Color color = fuelFill.color;
                refuelPreview.color = new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
            }
            fuelLabel.text = station ? $"CARGANDO COMBUSTIBLE · {station.Progress:P0}" : "COMBUSTIBLE";
            string fuel = unlimitedFuel ? "INFINITO" : $"{weapon.Fuel:0}/{weapon.tankCapacity:0}";
            tankLabel.text = $"{fuel}   ·   TANQUES ×{weapon.SpareTankCount}";
            debugText.text = $"{(grenade ? "GRANADA" : stats.Hitscan ? "RAYCAST" : "PROYECTIL")}  /  {hand.ActiveCount} DEDO(S)\nDAÑO {stats.Damage:0.##}  ·  ESPERA {stats.Interval:0.##} s  ·  VELOCIDAD {weapon.settings.moveSpeed:0.##} m/s\nCOSTO {weapon.GetFuelCost(stats):0.##} COMBUSTIBLE / DISPARO";
            if (!stats.Hitscan) debugText.text += $"\nÁREA {blast.Damage:0.##} DAÑO / {blast.Radius:0.##} m";
            if (hitText) hitText.text = Time.time - weapon.LastHitTime < 0.5f ? $"IMPACTO  -{weapon.LastHitDamage:0.##}" : "";
            if (!MenuRoot.activeSelf) return;
            for (int digit = 0; digit < HandPresetStore.SlotCount; digit++)
            {
                HandPreset preset = weapon.Presets.Get(digit);
                WeaponMode presetMode = weapon.settings.ResolveMode(preset.Mode);
                bool matches = preset.IsSaved && preset.ActiveMask == hand.ActiveMask &&
                    (!hand.ThumbUnlocked || presetMode == stats.Mode);
                presetButtons[digit].image.color = matches ? mint : new Color(0.13f, 0.2f, 0.23f);
                presetLabels[digit].color = matches ? new Color(0.02f, 0.09f, 0.08f) : ink;
                string group = "";
                for (int finger = 0; finger < 4; finger++)
                    if ((preset.ActiveMask & (1 << finger)) != 0) group += (finger + 1).ToString();
                presetLabels[digit].text = preset.IsSaved
                    ? $"[{digit}]  {(matches ? "ACTUAL" : "GUARDADO")}\n{group} · {(presetMode == WeaponMode.Grenade && hand.ThumbUnlocked ? "GRAN." : presetMode == WeaponMode.Ray || !hand.ThumbUnlocked ? "RAYO" : "PROY.")}"
                    : $"[{digit}]\nVACÍO";
            }
            if (presetFeedback && Time.unscaledTime >= presetFeedbackUntil)
                presetFeedback.text = "Se conservan al reiniciar. En partida: 1–9 / 0 para cambiar.";
            mode.text = grenade ? "GRANADA / TEMPORIZADA" : stats.Hitscan ? "RAYCAST / INSTANTÁNEO" : "PROYECTIL / EXPLOSIVO";
            mode.color = accent;
            float projectileTotalMultiplier = weapon.settings.projectileDamageMultiplier *
                (1f + weapon.settings.explosionDamageFraction);
            rules.text = "Siempre queda al menos un cañón activo.\nLos dedos activos forman un solo grupo.\n" +
                (hand.ThumbUnlocked
                    ? weapon.settings.grenadeEnabled
                        ? $"Selector: Rayo → Proyectil → Granada.\nProyectil: daño ×{projectileTotalMultiplier:0.##} (con explosión).\nGranada: {weapon.settings.grenadeFuseDuration:0.##} s desde tocar el suelo.\nMás potencia: más daño, área, costo y espera."
                        : $"Pulgar activo: raycast, valores base.\nProyectil: daño ×{projectileTotalMultiplier:0.##}, espera ×{weapon.settings.projectileCooldownMultiplier:0.##}, combustible −{weapon.settings.projectileFuelEfficiency:P0}.\nDaño total con explosión ({weapon.settings.explosionDamageFraction:P0} del directo)."
                    : "La mano dispara en modo láser hasta que recojas el pulgar.");
            menuStats.text = $"{stats.Damage:0.##}    DAÑO POR DISPARO\n\n{stats.Interval:0.##} s    ENTRE DISPAROS\n{1f / stats.Interval:0.##}    DISPAROS / SEGUNDO";
            if (!stats.Hitscan) menuStats.text += $"\nEXPLOSIÓN {blast.Damage:0.##} DAÑO / {blast.Radius:0.##} m";
            if (grenade) menuStats.text += $"\nMECHA {weapon.settings.grenadeFuseDuration:0.##} s · COSTO {weapon.GetFuelCost(stats):0.##}";
            palmText.text = $"{hand.ActiveCount:00} ACTIVO(S) / {4 - hand.ActiveCount:00} INACTIVO(S)\nUN SOLO GRUPO / UN CAÑÓN";
            string[] names = { "ÍNDICE", "MEDIO", "ANULAR", "MEÑIQUE" };
            for (int i = 0; i < 4; i++)
            {
                fingers[i].interactable = hand.CanToggle(i);
                fingers[i].image.color = hand.IsActive(i) ? mint : new Color(0.13f, 0.2f, 0.23f);
                fingerText[i].color = hand.IsActive(i) ? new Color(0.02f, 0.09f, 0.08f) : ink;
                fingerText[i].text = $"{i + 1:00}\n\n{names[i]}\n\n{(hand.IsActive(i) ? "ACTIVO" : "INACTIVO")}";
            }
            thumb.interactable = hand.ThumbUnlocked;
            thumb.image.color = hand.ThumbUnlocked ? accent : muted;
            thumbText.color = new Color(0.025f, 0.07f, 0.08f);
            thumbText.text = hand.ThumbUnlocked
                ? grenade ? $"GRANADA · CAMBIAR MODO\nExplota {weapon.settings.grenadeFuseDuration:0.##} s después de tocar el suelo"
                    : $"{(weapon.settings.grenadeEnabled ? "CAMBIAR MODO" : $"PULGAR {(hand.ThumbActive ? "ACTIVO" : "INACTIVO")}")}\n{(hand.ThumbActive ? "Raycast · daño y espera base" : $"Proyectil · daño ×{projectileTotalMultiplier:0.##} / espera ×{weapon.settings.projectileCooldownMultiplier:0.##}")}"
                : "PULGAR SIN EQUIPAR\nModo láser";
        }

    }
}


