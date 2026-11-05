using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private static readonly Color PreviewColor = new Color(0.06f, 0.11f, 0.14f);
        private static readonly Color DeleteColor = new Color(0.37f, 0.17f, 0.19f);

        private sealed class SaveRow
        {
            public GameObject root;
            public Text title, date, remaining, placeholder;
            public RawImage thumbnail;
            public Texture2D texture;
        }

        private PrototypePlayer player;
        private GameSaveSession session;
        private GameObject overlay;
        private Font font;
        private CanvasGroup controls;
        private CanvasGroup listControls;
        private GameObject deleteDialog;
        private Text deleteDescription;
        private Button cancelDeleteButton;
        private SavedGame deleteTarget;
        private Button previous, next;
        private Text status, pageLabel;
        private readonly SaveRow[] rows = new SaveRow[PageSize];
        private List<SavedGame> saves = new List<SavedGame>();
        private int page;
        private bool initialized, loading;
        internal bool IsLoading => loading || GameSaveSession.IsPendingFor(gameObject.scene);
        internal bool IsConfirmingDelete => deleteTarget != null;

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
                if (IsConfirmingDelete) CancelDelete();
                if (overlay) overlay.SetActive(false);
                return;
            }
            if (IsLoading) return;
            if (!overlay) Build();
            if (overlay.activeSelf) return;
            overlay.SetActive(true);
            RefreshSaves();
        }

        private void OnDisable()
        {
            if (IsConfirmingDelete) CancelDelete();
            if (overlay) overlay.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (var row in rows)
                if (row != null && row.texture) Destroy(row.texture);
            if (overlay) Destroy(overlay);
        }

        internal void Save()
        {
            if (IsLoading || session == null || player.IsDead) return;
            try
            {
                session.Save();
                if (overlay && overlay.activeSelf) RefreshSaves("Partida guardada.");
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                if (status) status.text = "No se pudo guardar la partida.";
                Debug.LogWarning(error.Message, this);
            }
        }

        private void RefreshSaves(string feedback = "", bool keepPage = false)
        {
            if (!keepPage) page = 0;
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
            page = Mathf.Clamp(page, 0, pages - 1);
            for (int index = 0; index < PageSize; index++)
            {
                var row = rows[index];
                if (row.texture) Destroy(row.texture);
                row.texture = null;
                row.thumbnail.texture = null;
                int slot = page * PageSize + index;
                row.root.SetActive(slot < saves.Count);
                if (slot >= saves.Count) continue;
                var save = saves[slot];
                var date = new DateTime(save.savedAtUtc, DateTimeKind.Utc).ToLocalTime();
                row.title.text = Path.GetFileNameWithoutExtension(save.scene);
                row.date.text = $"{date:dd/MM/yyyy HH:mm:ss}";
                row.remaining.text = FormatRemaining(save.levelSecondsRemaining);
                row.texture = ReadThumbnail(save.ThumbnailPath);
                row.thumbnail.texture = row.texture;
                row.thumbnail.color = row.texture ? Color.white : PreviewColor;
                row.placeholder.enabled = !row.texture;
            }
            previous.interactable = page > 0;
            next.interactable = page + 1 < pages;
            pageLabel.text = saves.Count == 0 ? "Todavía no hay partidas guardadas" : $"{page + 1} / {pages}   ·   {saves.Count} partidas";
        }

        private static string FormatRemaining(float seconds)
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds)) return "—";
            double wholeSeconds = Math.Floor(seconds);
            return $"{Math.Floor(wholeSeconds / 60):00}:{wholeSeconds % 60:00}";
        }

        private static Texture2D ReadThumbnail(string path)
        {
            byte[] data;
            try
            {
                if (!File.Exists(path)) return null;
                data = File.ReadAllBytes(path);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                return null;
            }

            var texture = new Texture2D(2, 2);
            if (texture.LoadImage(data, markNonReadable: true)) return texture;
            Destroy(texture);
            return null;
        }

        private void Load(int row)
        {
            int index = page * PageSize + row;
            if (IsLoading || IsConfirmingDelete || index >= saves.Count) return;
            if (!TryLoad(saves[index])) status.text = "No se pudo cargar esa partida.";
        }

        private void RequestDelete(int row)
        {
            int index = page * PageSize + row;
            if (IsLoading || IsConfirmingDelete || index >= saves.Count) return;
            deleteTarget = saves[index];
            deleteDescription.text = $"{rows[row].title.text}  ·  {rows[row].date.text}";
            listControls.interactable = false;
            deleteDialog.SetActive(true);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(cancelDeleteButton.gameObject);
        }

        internal void CancelDelete()
        {
            deleteTarget = null;
            if (deleteDialog) deleteDialog.SetActive(false);
            if (listControls) listControls.interactable = true;
        }

        private void ConfirmDelete()
        {
            if (!IsConfirmingDelete || IsLoading) return;
            var save = deleteTarget;
            CancelDelete();
            try
            {
                bool removedThumbnail = GameSaveSession.Delete(save);
                RefreshSaves(removedThumbnail ? "Partida eliminada." : "Partida eliminada. No se pudo borrar la miniatura.",
                    keepPage: true);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                status.text = "No se pudo eliminar la partida.";
                Debug.LogWarning(error.Message, this);
            }
        }

        internal bool TryLoadLatest()
        {
            if (IsLoading) return false;
            try
            {
                foreach (var save in GameSaveSession.ReadAll(out _))
                    if (TryLoad(save)) return true;
                if (status) status.text = "No hay partidas disponibles para cargar.";
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                if (status) status.text = "No se pudo leer la lista de partidas.";
                Debug.LogWarning(error.Message, this);
            }
            return false;
        }

        private bool TryLoad(SavedGame save)
        {
            if (!GameSaveSession.BeginLoad(save)) return false;
            loading = true;
            if (controls) controls.interactable = false;
            if (status) status.text = "Cargando partida…";
            return true;
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
            scaler.referenceResolution = new Vector2(840f, 700f);
            controls = overlay.GetComponent<CanvasGroup>();

            var shade = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
            shade.transform.SetParent(overlay.transform, false);
            var shadeRect = (RectTransform)shade.transform;
            shadeRect.anchorMin = Vector2.zero;
            shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;
            shade.GetComponent<Image>().color = new Color(0.02f, 0.05f, 0.07f, 0.77f);

            var panel = CenteredPanel(shade.transform, "Panel", 592f, 656f);
            listControls = panel.gameObject.AddComponent<CanvasGroup>();
            var body = panel.transform;

            Label(body, "PAUSA", new Rect(24f, 16f, 544f, 38f), 30, Mint).fontStyle = FontStyle.Bold;
            var saveButton = ActionButton(body, "Guardar partida", new Rect(24f, 66f, 266f, 40f), Save);
            saveButton.interactable = session != null;
            ActionButton(body, "Continuar", new Rect(302f, 66f, 266f, 40f), () => player.SetMenuOpen(false));
            Label(body, "PARTIDAS GUARDADAS", new Rect(24f, 121f, 544f, 24f), 16, Ink).fontStyle = FontStyle.Bold;
            Label(body, "F5 para guardar · F9 para cargar la última partida", new Rect(24f, 145f, 544f, 24f), 14, Muted);
            for (int index = 0; index < PageSize; index++)
            {
                int row = index;
                var root = Add<Image>(body, $"Fila de guardado {index + 1}", new Rect(24f, 176f + index * 80f, 544f, 74f));
                root.color = ButtonColor;
                root.raycastTarget = false;
                var button = ActionButton(root.transform, "", new Rect(0f, 0f, 449f, 74f), () => Load(row));
                button.name = $"Partida guardada {index + 1}";
                var item = button.transform;
                var title = Label(item, "", new Rect(136f, 10f, 180f, 26f), 18, Ink);
                title.fontStyle = FontStyle.Bold;
                var date = Label(item, "", new Rect(136f, 38f, 180f, 24f), 15, Muted);
                Label(item, "TIEMPO RESTANTE", new Rect(316f, 10f, 126f, 22f), 13, Muted, TextAnchor.MiddleRight);
                var remaining = Label(item, "", new Rect(316f, 32f, 126f, 30f), 21, Mint, TextAnchor.MiddleRight);
                var thumbnail = Add<RawImage>(item, "Miniatura", new Rect(8f, 5.5f, 112f, 63f));
                thumbnail.raycastTarget = false;
                var placeholder = Label(thumbnail.transform, "Sin miniatura", new Rect(0f, 0f, 112f, 63f),
                    13, Muted, TextAnchor.MiddleCenter);
                var delete = ActionButton(root.transform, "Eliminar", new Rect(459f, 20f, 77f, 34f), () => RequestDelete(row));
                delete.image.color = DeleteColor;
                rows[index] = new SaveRow
                {
                    root = root.gameObject, title = title, date = date, remaining = remaining,
                    thumbnail = thumbnail, placeholder = placeholder
                };
            }
            previous = ActionButton(body, "‹", new Rect(24f, 584f, 46f, 32f), () => { page--; ShowPage(); });
            next = ActionButton(body, "›", new Rect(522f, 584f, 46f, 32f), () => { page++; ShowPage(); });
            pageLabel = Label(body, "", new Rect(78f, 584f, 436f, 32f), 15, Muted, TextAnchor.MiddleCenter);
            status = Label(body, "", new Rect(24f, 620f, 544f, 26f), 14, Mint);
            BuildDeleteDialog(shade.transform);
            overlay.SetActive(false);
        }

        private void BuildDeleteDialog(Transform parent)
        {
            var dim = Add<Image>(parent, "Confirmar eliminación", default);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;
            dim.color = new Color(0.02f, 0.05f, 0.07f, 0.78f);
            deleteDialog = dim.gameObject;

            var panel = CenteredPanel(dim.transform, "Diálogo", 420f, 188f);
            panel.color = ButtonColor;
            var body = panel.transform;
            Label(body, "¿Eliminar esta partida?", new Rect(22f, 16f, 376f, 34f), 23, Ink).fontStyle = FontStyle.Bold;
            deleteDescription = Label(body, "", new Rect(22f, 54f, 376f, 24f), 15, Ink);
            Label(body, "La partida se borrará de forma permanente.", new Rect(22f, 84f, 376f, 24f), 15, Muted);
            cancelDeleteButton = ActionButton(body, "Cancelar", new Rect(22f, 126f, 180f, 40f), CancelDelete);
            var confirm = ActionButton(body, "Eliminar partida", new Rect(218f, 126f, 180f, 40f), ConfirmDelete);
            confirm.image.color = DeleteColor;
            deleteDialog.SetActive(false);
        }

        private static Image CenteredPanel(Transform parent, string name, float width, float height)
        {
            var panel = Add<Image>(parent, name, new Rect(0f, 0f, width, height));
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            panel.color = PreviewColor;
            return panel;
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
            if (!string.IsNullOrEmpty(caption))
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
