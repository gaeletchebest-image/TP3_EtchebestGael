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
        private Text menuStats, mode, rules, powerLabel, hudPowerLabel;
        private Transform statusHud;
        private static readonly string[] ModeNames = { "RAYO", "PROYECTIL", "GRANADA" };
        private readonly Text[] powerNumbers = new Text[4];
        private readonly Image[] powerSegments = new Image[4];
        private readonly Image[] hudPowerSegments = new Image[4];
        private readonly Button[] modeButtons = new Button[3];
        private readonly Text[] modeLabels = new Text[3];
        private readonly Image[] hudModes = new Image[3];
        private readonly Text[] hudModeLabels = new Text[3];
        private readonly Color ink = new Color(0.9f, 0.95f, 0.97f);
        private readonly Color muted = new Color(0.53f, 0.64f, 0.69f);
        private readonly Color mint = new Color(0.27f, 0.93f, 0.75f);
        private readonly Color orange = new Color(1f, 0.59f, 0.31f);
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
            Transform panel = root.Find("Hand Menu Panel");
            statusHud = canvas.transform.Find("Weapon Status");
            if (!panel || !statusHud)
            {
                Debug.LogError("Canvas is missing the weapon configuration UI. Update the scene with PrototypeSceneBuilder.EnsureWeaponMenu.", this);
                enabled = false;
                return;
            }
            powerLabel = panel.Find("Power Label").GetComponent<Text>();
            hudPowerLabel = statusHud.Find("Power Label").GetComponent<Text>();
            for (int i = 0; i < powerSegments.Length; i++)
            {
                int level = i + 1;
                Button segment = panel.Find($"Power Segment {level}").GetComponent<Button>();
                powerSegments[i] = segment.image;
                powerNumbers[i] = segment.GetComponentInChildren<Text>();
                hudPowerSegments[i] = statusHud.Find($"Power Segment {level}").GetComponent<Image>();
                segment.onClick.AddListener(() => weapon.SetPowerLevel(level));
            }
            for (int i = 0; i < modeButtons.Length; i++)
            {
                WeaponMode selectedMode = (WeaponMode)i;
                modeButtons[i] = panel.Find($"Mode Button {i + 1}").GetComponent<Button>();
                modeLabels[i] = modeButtons[i].GetComponentInChildren<Text>();
                modeButtons[i].onClick.AddListener(() => weapon.SelectMode(selectedMode));
                hudModes[i] = statusHud.Find($"Mode {i + 1}").GetComponent<Image>();
                hudModeLabels[i] = hudModes[i].GetComponentInChildren<Text>();
            }
            mode = panel.Find("Menu Mode").GetComponent<Text>();
            menuStats = panel.Find("Menu Stats").GetComponent<Text>();
            rules = panel.Find("Rules Text").GetComponent<Text>();
            panel.Find("Resume Button").GetComponent<Button>().onClick.AddListener(() => player.SetMenuOpen(false));
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                UiAudioFeedback.Attach(button);
                if (button.targetGraphic is Graphic graphic) graphic.raycastTarget = true;
            }
        }
        private void LateUpdate()
        {
            if (!MenuRoot || weapon.Hand == null) return;
            bool equipped = weapon.IsEquipped;
            bool unlimitedFuel = weapon.HasUnlimitedFuel;
            MenuRoot.SetActive(equipped && player.IsMenuOpen && !player.IsPauseMenuOpen);
            debugPanel.SetActive(equipped && showDebugStats && !player.IsMenuOpen);
            statusHud.gameObject.SetActive(equipped && !player.IsMenuOpen && !player.IsDead);
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
                    ? $"{movement}   CLIC DISPARAR{reload}   CTRL INSPECCIONAR\nRUEDA POTENCIA   1/2/3 MODO   {settings.menuKey} ARMA"
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
            debugText.text = $"{(grenade ? "GRANADA" : stats.Hitscan ? "RAYCAST" : "PROYECTIL")}  /  POTENCIA {weapon.PowerLevel}/4\nDAÑO {stats.Damage:0.##}  ·  ESPERA {stats.Interval:0.##} s  ·  VELOCIDAD {weapon.settings.moveSpeed:0.##} m/s\nCOSTO {weapon.GetFuelCost(stats):0.##} COMBUSTIBLE / DISPARO";
            if (!stats.Hitscan) debugText.text += $"\nÁREA {blast.Damage:0.##} DAÑO / {blast.Radius:0.##} m";
            if (hitText) hitText.text = Time.time - weapon.LastHitTime < 0.5f ? $"IMPACTO  -{weapon.LastHitDamage:0.##}" : "";
            UpdateConfigurationDisplay(stats);
            if (!MenuRoot.activeSelf) return;
            mode.text = grenade ? "GRANADA / TEMPORIZADA" : stats.Hitscan ? "RAYO / INSTANTÁNEO" : "PROYECTIL / EXPLOSIVO";
            mode.color = accent;
            rules.text = "Más potencia aumenta el daño, el consumo y la espera entre disparos.\n\n" +
                (hand.ThumbUnlocked
                    ? grenade ? $"La granada explota {weapon.settings.grenadeFuseDuration:0.##} s después de tocar el suelo."
                        : stats.Hitscan ? "El rayo impacta al instante."
                        : "El proyectil causa daño directo y una explosión al impactar."
                    : "Recogé el pulgar para desbloquear los otros tipos de disparo.");
            menuStats.text = $"{stats.Damage:0.##}    DAÑO POR DISPARO\n\n{stats.Interval:0.##} s    ENTRE DISPAROS\n\n{weapon.GetFuelCost(stats):0.##}    COMBUSTIBLE / DISPARO";
            if (!stats.Hitscan) menuStats.text += $"\n\n{blast.Radius:0.##} m    RADIO DE EXPLOSIÓN";
        }
        private void UpdateConfigurationDisplay(ShotStats stats)
        {
            Color inactive = new Color(0.13f, 0.2f, 0.23f);
            Color selectedInk = new Color(0.02f, 0.09f, 0.08f);
            int level = weapon.PowerLevel;
            powerLabel.text = $"NIVEL {level} / 4";
            hudPowerLabel.text = $"POTENCIA {level}/4  ·  RUEDA";
            for (int i = 0; i < powerSegments.Length; i++)
            {
                powerSegments[i].color = i < level ? mint : inactive;
                powerNumbers[i].color = i < level ? selectedInk : ink;
                hudPowerSegments[i].color = i < level ? mint : inactive;
            }
            for (int i = 0; i < modeButtons.Length; i++)
            {
                bool available = weapon.IsModeAvailable((WeaponMode)i);
                bool selected = stats.Mode == (WeaponMode)i;
                Color background = selected ? mint : inactive;
                Color foreground = selected ? selectedInk : available ? ink : muted;
                modeButtons[i].interactable = available;
                modeButtons[i].image.color = hudModes[i].color = background;
                modeLabels[i].color = hudModeLabels[i].color = foreground;
                modeLabels[i].text = $"[{i + 1}]  {ModeNames[i]}\n{(selected ? "ACTIVO" : available ? "INACTIVO" : "BLOQUEADO")}";
                hudModeLabels[i].text = $"{i + 1} {ModeNames[i]}" + (available ? "" : " ×");
            }
        }
    }
}
