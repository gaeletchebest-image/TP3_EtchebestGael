using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HandShooter
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    public sealed class LevelLiftTransition : MonoBehaviour
    {
        [SerializeField] private CargoLift arrival;
        [SerializeField] private CargoLift departure;
        [Tooltip("Si se deja vacío, usa el jugador de esta escena.")]
        [SerializeField] private PrototypePlayer player;
        [Tooltip("Tiempo en negro antes de volver a cargar el nivel.")]
        [SerializeField, Min(0f)] private float blackHoldSeconds = 1f;

        private Image shade;
        private float? reloadAt;
        private bool loading;

        private void Awake()
        {
            var scene = gameObject.scene;
            if (!player)
            {
                foreach (var candidate in FindObjectsByType<PrototypePlayer>(FindObjectsSortMode.None))
                {
                    if (candidate.gameObject.scene != scene) continue;
                    player = candidate;
                    break;
                }
            }

            if (!arrival || !departure || !player || scene.buildIndex < 0)
            {
                Debug.LogError("LevelLiftTransition needs both lifts and the player in a scene included in the build.", this);
                enabled = false;
                return;
            }

            var overlay = new GameObject("Level Fade", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(overlay, scene);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20; // Above the HUD and below the pause menu.

            shade = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = shade.rectTransform;
            rect.SetParent(overlay.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            shade.color = Color.black;
            shade.raycastTarget = false;
        }

        private void LateUpdate()
        {
            if (loading) return;

            // Derive opacity from the lift poses so it also follows restored saves.
            float departureFade = departure.DescentProgress;
            float opacity = Mathf.Max(1f - arrival.DescentProgress, departureFade);
            shade.color = new Color(0f, 0f, 0f, opacity);
            shade.enabled = opacity > 0f && !player.IsDead && (!player.IsMenuOpen || player.IsPauseMenuOpen);

            if (departureFade < 1f)
            {
                reloadAt = null;
                return;
            }

            reloadAt ??= Time.time + blackHoldSeconds;
            if (Time.time < reloadAt.Value || Time.deltaTime <= 0f) return;

            loading = true;
            SceneManager.LoadSceneAsync(gameObject.scene.buildIndex, LoadSceneMode.Single);
        }

        private void OnDisable()
        {
            if (shade) shade.enabled = false;
        }

        private void OnDestroy()
        {
            if (shade) Destroy(shade.transform.parent.gameObject);
        }
    }
}
