using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HandShooter
{
    internal sealed class EnemyInspection
    {
        private const int RingSegments = 128;
        private const float BarWidth = 220f;
        private static readonly Color AttackColor = new Color(1f, 0.46f, 0.18f);
        private static readonly Color VisionColor = new Color(0.25f, 0.85f, 1f);
        private static readonly Color SharedColor = new Color(0.79f, 0.65f, 1f);
        private static readonly Color DefenseColor = new Color(0.40f, 0.86f, 0.90f);
        private static readonly Vector3[] UnitCircle = CreateUnitCircle();

        private readonly GameObject root, canvasObject;
        private readonly RectTransform panel, healthFill, resistanceFill;
        private readonly Text title, healthText, shieldText, resistanceText, attackText, visionText, pinHint;
        private readonly LineRenderer attackRing, visionRing, minimumRing;
        private readonly Material ringMaterial;
        private readonly Vector3[] ringPositions = new Vector3[RingSegments];
        private DamageTarget displayedTarget;
        private bool displayedAsPinned;
        private (float health, float maxHealth, int shields, int resistance, int capacity,
            float attack, float vision, float minimum) displayedValues;

        public EnemyInspection(Transform player)
        {
            root = new GameObject("Enemy Inspection");
            root.transform.SetParent(player, false);
            canvasObject = new GameObject("Inspection HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(canvasObject, player.gameObject.scene);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1366f, 768f);
            scaler.matchWidthOrHeight = 0.5f;

            panel = EnemyVitalsDisplay.CreateGraphic<Image>("Target Panel", canvas.transform,
                new Color(0.03f, 0.07f, 0.09f, 0.96f), new Vector2(252f, 196f), 0f).rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
            panel.anchoredPosition = new Vector2(-26f, -30f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var ink = new Color(0.92f, 0.96f, 1f);
            var mint = new Color(0.27f, 0.93f, 0.75f);
            var accent = EnemyVitalsDisplay.CreateGraphic<Image>("Accent", panel, mint, new Vector2(252f, 3f), 0f);
            accent.rectTransform.anchorMin = accent.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title = EnemyVitalsDisplay.CreateText("Enemy Type", panel, font, 19, 0f, mint);
            title.fontStyle = FontStyle.Bold;
            healthText = EnemyVitalsDisplay.CreateText("Health Points", panel, font, 15, 0f, ink);
            healthFill = EnemyVitalsDisplay.CreateBar("Health Bar", "Health Fill", panel, 0f, 7f,
                new Color(0.21f, 0.10f, 0.13f), new Color(0.98f, 0.30f, 0.39f), BarWidth);
            shieldText = EnemyVitalsDisplay.CreateText("Shields", panel, font, 15, 0f, DefenseColor);
            resistanceText = EnemyVitalsDisplay.CreateText("Resistance Points", panel, font, 15, 0f, DefenseColor);
            resistanceFill = EnemyVitalsDisplay.CreateBar("Resistance Bar", "Resistance Fill", panel, 0f, 7f,
                new Color(0.07f, 0.19f, 0.24f), DefenseColor, BarWidth);
            attackText = EnemyVitalsDisplay.CreateText("Attack Range", panel, font, 14, 0f, AttackColor);
            visionText = EnemyVitalsDisplay.CreateText("Vision Range", panel, font, 14, 0f, VisionColor);
            pinHint = EnemyVitalsDisplay.CreateText("Pin Hint", panel, font, 13, 0f, mint);

            ringMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            attackRing = CreateRing("Attack Radius", AttackColor);
            visionRing = CreateRing("Vision Radius", VisionColor);
            minimumRing = CreateRing("Minimum Attack Radius", AttackColor);
            Hide();
        }

        public void Show(DamageTarget target, bool pinned)
        {
            var (name, attack, vision, minimum) = GetRanges(target);
            var values = (target.Health, target.MaximumHealth, target.DefensePoints, target.DefenseBar,
                target.DefenseBarCapacity, attack, vision, minimum);
            bool sharedRange = attack > 0f && Mathf.Approximately(attack, vision);
            if (displayedTarget != target || displayedValues != values || displayedAsPinned != pinned)
            {
                displayedTarget = target;
                displayedValues = values;
                displayedAsPinned = pinned;
                RefreshPanel(name, sharedRange);
                SetRingColor(attackRing, sharedRange ? SharedColor : AttackColor);
            }

            if (!root.activeSelf) root.SetActive(true);
            if (!canvasObject.activeSelf) canvasObject.SetActive(true);
            Vector3 center = target.RangeCenter;
            DrawRing(attackRing, center, attack);
            DrawRing(visionRing, center, sharedRange ? 0f : vision);
            DrawRing(minimumRing, center, minimum);
        }

        public void Hide()
        {
            if (root && root.activeSelf) root.SetActive(false);
            if (canvasObject && canvasObject.activeSelf) canvasObject.SetActive(false);
        }

        public void Destroy()
        {
            Object.Destroy(canvasObject);
            Object.Destroy(ringMaterial);
        }

        private void RefreshPanel(string name, bool sharedRange)
        {
            var (health, maxHealth, shields, resistance, capacity, attack, vision, minimum) = displayedValues;
            bool hasDefense = shields > 0;
            bool showVision = !sharedRange && vision > 0f;
            float height = 124f + (hasDefense ? 63f : 0f) + (showVision ? 21f : 0f);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height + 22f);
            title.text = name;
            PlaceFromTop(title.rectTransform, 23f);
            healthText.text = $"VIDA   {health:0.#} / {maxHealth:0.#}";
            PlaceFromTop(healthText.rectTransform, 51f);
            SetBar(healthFill, health, maxHealth, 69f);
            shieldText.gameObject.SetActive(hasDefense);
            resistanceText.gameObject.SetActive(hasDefense);
            resistanceFill.parent.gameObject.SetActive(hasDefense);
            if (hasDefense)
            {
                shieldText.text = $"ESCUDOS   ×{shields}";
                PlaceFromTop(shieldText.rectTransform, 91f);
                resistanceText.text = $"RESISTENCIA   {resistance} / {capacity}";
                PlaceFromTop(resistanceText.rectTransform, 112f);
                SetBar(resistanceFill, resistance, capacity, 130f);
            }

            string attackDistance = minimum > 0f ? $"{minimum:0.#}–{attack:0.#} m" : $"{attack:0.#} m";
            attackText.color = sharedRange ? SharedColor : AttackColor;
            attackText.text = $"{(sharedRange ? "ATAQUE Y VISIÓN" : "ATAQUE")}   {attackDistance}";
            PlaceFromTop(attackText.rectTransform, height - (showVision ? 43f : 22f));
            visionText.gameObject.SetActive(showVision);
            if (showVision)
            {
                visionText.text = $"VISIÓN   {vision:0.#} m";
                PlaceFromTop(visionText.rectTransform, height - 22f);
            }
            pinHint.text = displayedAsPinned ? "FIJADO  ·  CTRL PARA SOLTAR" : "CTRL + CLIC DERECHO PARA FIJAR";
            PlaceFromTop(pinHint.rectTransform, height);
        }

        private void PlaceFromTop(RectTransform element, float offset) =>
            element.anchoredPosition = new Vector2(0f, panel.rect.height * 0.5f - offset);

        private void SetBar(RectTransform fill, float value, float maximum, float offset)
        {
            float fraction = maximum > 0f ? Mathf.Clamp01(value / maximum) : 0f;
            fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, BarWidth * fraction);
            PlaceFromTop((RectTransform)fill.parent, offset);
        }

        private LineRenderer CreateRing(string name, Color color)
        {
            var ring = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>();
            ring.transform.SetParent(root.transform, false);
            ring.sharedMaterial = ringMaterial;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = RingSegments;
            ring.widthMultiplier = 0.10f;
            SetRingColor(ring, color);
            return ring;
        }

        private static void SetRingColor(LineRenderer ring, Color color)
        {
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            ring.SetPropertyBlock(properties);
        }

        private void DrawRing(LineRenderer ring, Vector3 center, float radius)
        {
            ring.enabled = radius > 0f;
            if (!ring.enabled) return;
            for (int i = 0; i < RingSegments; i++)
                ringPositions[i] = center + UnitCircle[i] * radius;
            ring.SetPositions(ringPositions);
        }

        private static Vector3[] CreateUnitCircle()
        {
            var positions = new Vector3[RingSegments];
            for (int i = 0; i < positions.Length; i++)
            {
                float angle = i * (2f * Mathf.PI / positions.Length);
                positions[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
            return positions;
        }

        private static (string name, float attack, float vision, float minimum) GetRanges(DamageTarget target)
        {
            if (target.hunterSettings)
                return ("CAZADOR", target.hunterSettings.range, target.hunterSettings.range, 0f);
            if (target.sniperSettings)
            {
                var sniper = target.sniperSettings;
                return ("FRANCOTIRADOR", sniper.maximumAttackRange, sniper.detectionRange, sniper.minimumAttackRange);
            }
            if (target.kamikazeSettings)
                return ("KAMIKAZE", target.kamikazeSettings.blastRadius, target.kamikazeSettings.detectionRange, 0f);
            return ("TORRETA", target.turretSettings.detectionRange, target.turretSettings.detectionRange, 0f);
        }
    }
}
