using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HandShooter
{
    internal sealed class EnemyVitalsDisplay
    {
        internal const float BarWidth = 140f;
        private readonly GameObject root;
        private readonly RectTransform healthFill;
        private readonly Text healthText, damageText, statusText;
        private readonly EnemyDefenseDisplay defenseDisplay;

        public EnemyVitalsDisplay(DamageTarget owner, TextMesh status)
        {
            // A separate root keeps the billboard free of the enemy's nonuniform scale.
            root = new GameObject($"{owner.name} Indicators", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, owner.gameObject.scene);
            root.layer = 2;
            root.transform.localScale = Vector3.one * 0.01f;
            // World-space UI uses the camera's depth buffer for bars and glyphs alike.
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 110f);
            SetVisible(false);

            var font = owner.label && owner.label.font ? owner.label.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            HideLegacyText(owner.label);
            HideLegacyText(status);

            healthFill = CreateBar("Health Bar", "Health Fill", root.transform, 0f, 30f,
                new Color(0.18f, 0.03f, 0.05f), new Color(0.84f, 0.08f, 0.15f));
            healthText = CreateText("Health Points", root.transform, font, 23, 0f, Color.white);
            damageText = CreateText("Last Damage", root.transform, font, 18, -26f, Color.white);
            defenseDisplay = EnemyDefenseDisplay.Create(root.transform, font);
            if (status)
                statusText = CreateText("Enemy State", root.transform, font, 21, 70f, status.color);
        }

        public void Refresh(float health, float maximum, float damage, EnemyDefense defense)
        {
            float fraction = maximum > 0f ? Mathf.Clamp01(health / maximum) : 0f;
            healthFill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, BarWidth * fraction);
            healthText.text = $"{health:0.#} / {maximum:0.#}";
            damageText.text = damage > 0f ? $"-{damage:0.#}" : "";
            defenseDisplay.Refresh(defense.Points, defense.Bar, defense.Capacity, health > 0f);
            if (statusText)
                statusText.rectTransform.anchoredPosition = new Vector2(0f, defense.Points > 0 ? 70f : 32f);
        }

        public void Place(Vector3 position, Camera camera, TextMesh status)
        {
            SetVisible(camera);
            if (!camera) return;
            root.transform.SetPositionAndRotation(position, camera.transform.rotation);
            if (statusText && status)
            {
                statusText.text = status.text;
                statusText.color = status.color;
            }
        }

        public void SetVisible(bool visible)
        {
            if (root && root.activeSelf != visible) root.SetActive(visible);
        }
        public void Destroy() => Object.Destroy(root);

        internal static RectTransform CreateBar(string name, string fillName, Transform parent, float y, float height,
            Color background, Color foreground)
        {
            var track = CreateGraphic<Image>(name, parent, background, new Vector2(BarWidth, height), y);
            var fill = CreateGraphic<Image>(fillName, track.transform, foreground, new Vector2(BarWidth, height), 0f).rectTransform;
            fill.anchorMin = fill.anchorMax = fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = Vector2.zero;
            return fill;
        }

        internal static Text CreateText(string name, Transform parent, Font font, int fontSize, float y, Color color)
        {
            var text = CreateGraphic<Text>(name, parent, color, new Vector2(220f, 30f), y);
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            return text;
        }

        private static T CreateGraphic<T>(string name, Transform parent, Color color, Vector2 size, float y) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var graphic = go.GetComponent<T>();
            graphic.color = color;
            graphic.raycastTarget = false;
            graphic.rectTransform.sizeDelta = size;
            graphic.rectTransform.anchoredPosition = new Vector2(0f, y);
            return graphic;
        }

        private static void HideLegacyText(TextMesh text)
        {
            if (text && text.TryGetComponent<MeshRenderer>(out var renderer))
                renderer.enabled = false;
        }
    }
}
