using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace HandShooter
{
    [DefaultExecutionOrder(1000)]
    internal sealed class PauseMenu : MonoBehaviour
    {
        private const int PageSize = 5;
        private static readonly Color Ink = new Color(0.90f, 0.95f, 0.97f);
        private static readonly Color Muted = new Color(0.61f, 0.72f, 0.76f);
        private static readonly Color Mint = new Color(0.27f, 0.93f, 0.75f);
        private static readonly Color ButtonColor = new Color(0.12f, 0.23f, 0.27f);

        private PrototypePlayer player;
        private GameSaveSession session;
        private GameObject overlay;
        private Font font;
        private CanvasGroup controls;
        private Button previous, next;
        private Text status, pageLabel;
        private readonly Button[] rows = new Button[PageSize];
        private readonly Text[] rowLabels = new Text[PageSize];
        private List<SavedGame> saves = new List<SavedGame>();
        private int page;
        private bool initialized, loading;
        internal bool IsLoading => loading || GameSaveSession.IsPendingFor(gameObject.scene);

        private void Awake() => player = GetComponent<PrototypePlayer>();

        private void LateUpdate()
        {
            if (!initialized)
            {
                initialized = true;
                if (player.weapon && player.viewCamera)
                {
                    // Scene Start callbacks can still set arena doors and place NavMesh agents.
                    session = new GameSaveSession(player);
                    if (session.ApplyPendingLoad()) player.SetMenuOpen(false);
                }
            }
            if (!player.IsPauseMenuOpen)
            {
                if (overlay) overlay.SetActive(false);
                return;
            }
            if (!overlay) Build();
            if (overlay.activeSelf) return;
            overlay.SetActive(true);
            RefreshSaves();
        }

        private void OnDisable()
        {
            if (overlay) overlay.SetActive(false);
        }

        private void OnDestroy()
        {
            if (overlay) Destroy(overlay);
        }

        private void Save()
        {
            if (IsLoading || session == null || player.IsDead) return;
            try
            {
                GameSaveSession.Write(session.Capture());
                RefreshSaves("Partida guardada.");
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                status.text = "No se pudo guardar la partida.";
                Debug.LogWarning(error.Message, this);
            }
        }

        private void RefreshSaves(string feedback = "")
        {
            page = 0;
            try
            {
                saves = GameSaveSession.ReadAll(out int skipped);
                status.text = skipped == 0 ? feedback : "Hay archivos de guardado que no se pudieron leer.";
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                saves.Clear();
                status.text = "No se pudo leer la lista de partidas.";
                Debug.LogWarning(error.Message, this);
            }
            ShowPage();
        }

        private void ShowPage()
        {
            int pages = Mathf.Max(1, (saves.Count + PageSize - 1) / PageSize);
            for (int index = 0; index < PageSize; index++)
            {
                int slot = page * PageSize + index;
                rows[index].gameObject.SetActive(slot < saves.Count);
                if (slot >= saves.Count) continue;
                var save = saves[slot];
                var date = new DateTime(save.savedAtUtc, DateTimeKind.Utc).ToLocalTime();
                rowLabels[index].text = $"  {Path.GetFileNameWithoutExtension(save.scene)}   ·   {date:dd/MM/yyyy HH:mm:ss}";
            }
            previous.interactable = page > 0;
            next.interactable = page + 1 < pages;
            pageLabel.text = saves.Count == 0 ? "Todavía no hay partidas guardadas" : $"{page + 1} / {pages}   ·   {saves.Count} partidas";
        }

        private void Load(int row)
        {
            int index = page * PageSize + row;
            if (IsLoading || index >= saves.Count) return;
            if (!GameSaveSession.BeginLoad(saves[index]))
            {
                status.text = "No se pudo cargar esa partida.";
                return;
            }
            loading = true;
            controls.interactable = false;
            status.text = "Cargando partida…";
        }

        private void Build()
        {
            HandMenu.EnsureEventSystem();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            overlay = new GameObject("Menú de pausa", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(overlay, gameObject.scene);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referenceResolution = new Vector2(840f, 560f);
            controls = overlay.GetComponent<CanvasGroup>();

            var shade = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
            shade.transform.SetParent(overlay.transform, false);
            var shadeRect = (RectTransform)shade.transform;
            shadeRect.anchorMin = Vector2.zero;
            shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;
            shade.GetComponent<Image>().color = new Color(0.02f, 0.05f, 0.07f, 0.77f);

            var panel = Add<Image>(shade.transform, "Panel", new Rect(0f, 0f, 512f, 470f));
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            panel.color = new Color(0.06f, 0.11f, 0.14f);
            var body = panel.transform;

            Label(body, "PAUSA", new Rect(24f, 16f, 464f, 38f), 30, Mint).fontStyle = FontStyle.Bold;
            var saveButton = ActionButton(body, "Guardar partida", new Rect(24f, 66f, 228f, 40f), Save);
            saveButton.interactable = session != null;
            ActionButton(body, "Continuar", new Rect(260f, 66f, 228f, 40f), () => player.SetMenuOpen(false));
            Label(body, "PARTIDAS GUARDADAS", new Rect(24f, 121f, 464f, 24f), 16, Ink).fontStyle = FontStyle.Bold;
            Label(body, "Elegí una partida para cargarla", new Rect(24f, 145f, 464f, 24f), 14, Muted);
            for (int index = 0; index < PageSize; index++)
            {
                int row = index;
                rows[index] = ActionButton(body, "", new Rect(24f, 176f + index * 42f, 464f, 36f), () => Load(row));
                rowLabels[index] = rows[index].GetComponentInChildren<Text>();
                rowLabels[index].alignment = TextAnchor.MiddleLeft;
            }
            previous = ActionButton(body, "‹", new Rect(24f, 391f, 46f, 32f), () => { page--; ShowPage(); });
            next = ActionButton(body, "›", new Rect(442f, 391f, 46f, 32f), () => { page++; ShowPage(); });
            pageLabel = Label(body, "", new Rect(78f, 391f, 356f, 32f), 15, Muted, TextAnchor.MiddleCenter);
            status = Label(body, "", new Rect(24f, 427f, 464f, 27f), 14, Mint);
            overlay.SetActive(false);
        }

        private Button ActionButton(Transform parent, string caption, Rect bounds, UnityEngine.Events.UnityAction action)
        {
            var background = Add<Image>(parent, caption, bounds);
            background.color = ButtonColor;
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.83f, 1f, 0.96f);
            colors.colorMultiplier = 1.3f;
            button.colors = colors;
            button.onClick.AddListener(action);
            Label(background.transform, caption, new Rect(0f, 0f, bounds.width, bounds.height), 17, Ink, TextAnchor.MiddleCenter);
            return button;
        }

        private Text Label(Transform parent, string value, Rect bounds, int size, Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = Add<Text>(parent, "Texto", bounds);
            label.font = font;
            label.text = value;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        private static T Add<T>(Transform parent, string name, Rect bounds) where T : Graphic
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(T));
            child.transform.SetParent(parent, false);
            var rect = (RectTransform)child.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(bounds.x, -bounds.y);
            rect.sizeDelta = bounds.size;
            return child.GetComponent<T>();
        }
    }
}
