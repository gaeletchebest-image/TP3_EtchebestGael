using UnityEngine;
using UnityEngine.UI;

namespace HandShooter
{
    // Lives beside the existing health label, so it follows the same movement and rotation.
    public sealed class EnemyDefenseDisplay : MonoBehaviour
    {
        private Text caption;
        private RectTransform fill;

        public static EnemyDefenseDisplay Create(TextMesh healthLabel)
        {
            var go = new GameObject("Enemy Defense", typeof(RectTransform), typeof(Canvas));
            go.layer = 2;
            go.transform.SetParent(healthLabel.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            go.transform.localScale = Vector3.one * 0.01f;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 46f);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var display = go.AddComponent<EnemyDefenseDisplay>();

            var textObject = new GameObject("Defense Points", typeof(RectTransform), typeof(Text));
            textObject.layer = 2;
            textObject.transform.SetParent(go.transform, false);
            display.caption = textObject.GetComponent<Text>();
            display.caption.font = healthLabel.font ? healthLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            display.caption.fontSize = 22;
            display.caption.alignment = TextAnchor.MiddleCenter;
            display.caption.color = new Color(0.35f, 0.85f, 1f);
            display.caption.raycastTarget = false;
            display.caption.rectTransform.sizeDelta = new Vector2(160f, 30f);
            display.caption.rectTransform.anchoredPosition = new Vector2(0f, 8f);

            var background = Image("Defense Bar", go.transform, new Color(0.03f, 0.12f, 0.2f));
            background.rectTransform.sizeDelta = new Vector2(140f, 9f);
            background.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            var foreground = Image("Defense Fill", background.transform, new Color(0.15f, 0.7f, 1f));
            display.fill = foreground.rectTransform;
            display.fill.anchorMin = display.fill.anchorMax = new Vector2(0f, 0.5f);
            display.fill.pivot = new Vector2(0f, 0.5f);
            display.fill.anchoredPosition = Vector2.zero;
            return display;
        }

        public void Refresh(int points, int bar, int capacity, bool alive)
        {
            gameObject.SetActive(alive && points > 0);
            if (!gameObject.activeSelf) return;
            caption.text = $"DEF {points}  ·  {bar}/{capacity}";
            fill.sizeDelta = new Vector2(140f * bar / Mathf.Max(1, capacity), 9f);
        }

        private static Image Image(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = 2;
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
