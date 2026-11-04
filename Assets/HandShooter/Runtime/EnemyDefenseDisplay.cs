using UnityEngine;
using UnityEngine.UI;

namespace HandShooter
{
    public sealed class EnemyDefenseDisplay : MonoBehaviour
    {
        private Text caption;
        private RectTransform fill;

        public static EnemyDefenseDisplay Create(Transform parent, Font font)
        {
            var go = new GameObject("Enemy Defense", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var display = go.AddComponent<EnemyDefenseDisplay>();
            display.caption = EnemyVitalsDisplay.CreateText("Defense Points", go.transform, font, 22, 49f,
                new Color(0.35f, 0.85f, 1f));
            display.fill = EnemyVitalsDisplay.CreateBar("Defense Bar", "Defense Fill", go.transform, 26f, 9f,
                new Color(0.03f, 0.12f, 0.2f), new Color(0.15f, 0.7f, 1f));
            return display;
        }

        public void Refresh(int points, int bar, int capacity, bool alive)
        {
            gameObject.SetActive(alive && points > 0);
            if (!gameObject.activeSelf) return;
            caption.text = $"DEF {points}  ·  {bar}/{capacity}";
            fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, EnemyVitalsDisplay.BarWidth * bar / capacity);
        }
    }
}
