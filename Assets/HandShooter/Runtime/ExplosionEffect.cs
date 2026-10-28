using UnityEngine;

namespace HandShooter
{
    // Three expanding rings communicate the actual spherical damage radius without adding physics colliders.
    public sealed class ExplosionEffect : MonoBehaviour
    {
        private const int Segments = 64;
        private readonly LineRenderer[] rings = new LineRenderer[3];
        private float radius, duration, elapsed;
        private Color color;
        public static void Spawn(Vector3 position, float radius, float duration, Material material, Color color)
        {
            var effect = new GameObject("Projectile explosion").AddComponent<ExplosionEffect>();
            effect.gameObject.layer = 2;
            effect.transform.position = position;
            effect.radius = radius;
            effect.duration = Mathf.Max(0.05f, duration);
            effect.color = color;
            for (int axis = 0; axis < 3; axis++)
            {
                var ring = new GameObject("Blast ring").AddComponent<LineRenderer>();
                ring.gameObject.layer = 2;
                ring.transform.SetParent(effect.transform, false);
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.positionCount = Segments;
                ring.sharedMaterial = material;
                ring.startWidth = ring.endWidth = 1f;
                ring.widthMultiplier = 0.045f;
                ring.startColor = ring.endColor = color;
                for (int i = 0; i < Segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / Segments;
                    float x = Mathf.Cos(angle), y = Mathf.Sin(angle);
                    ring.SetPosition(i, axis == 0 ? new Vector3(x, y, 0) : axis == 1 ? new Vector3(x, 0, y) : new Vector3(0, x, y));
                }
                effect.rings[axis] = ring;
            }
            effect.transform.localScale = Vector3.one * radius * 0.2f;
        }
        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.one * radius * Mathf.Lerp(0.2f, 1f, Mathf.Sqrt(t));
            Color faded = color; faded.a *= 1f - t;
            foreach (var ring in rings)
            {
                ring.startColor = ring.endColor = faded;
                ring.widthMultiplier = 0.045f * Mathf.Lerp(1f, 0.05f, t);
            }
            if (t >= 1f) Destroy(gameObject);
        }
    }
}

