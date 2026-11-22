using UnityEngine;
using UnityEngine.UI;

namespace HandShooter
{
    internal sealed partial class PauseMenu
    {
        private GameObject audioDialog;
        internal bool IsAudioOpen => audioDialog && audioDialog.activeSelf;

        private void OpenAudio()
        {
            listControls.interactable = false;
            audioDialog.SetActive(true);
        }

        internal void CloseAudio()
        {
            if (audioDialog) audioDialog.SetActive(false);
            if (listControls) listControls.interactable = true;
            PlayerPrefs.Save();
            ShooterAudio.Ui(SoundCue.UiCancel);
        }

        private void BuildAudioDialog(Transform parent)
        {
            var dim = Add<Image>(parent, "Ajustes de audio", default);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;
            dim.color = new Color(0.02f, 0.05f, 0.07f, 0.9f);
            audioDialog = dim.gameObject;
            var panel = CenteredPanel(dim.transform, "Audio", 452f, 360f);
            var body = panel.transform;
            Label(body, "AUDIO", new Rect(24f, 16f, 404f, 36f), 27, Mint).fontStyle = FontStyle.Bold;
            string[] names = { "General", "Efectos", "Ambiente", "Música" };
            for (int i = 0; i < names.Length; i++)
            {
                AudioBus bus = (AudioBus)i;
                float y = 68f + i * 50f;
                Label(body, names[i], new Rect(24f, y, 122f, 28f), 17, Ink);
                var percent = Label(body, "", new Rect(366f, y, 62f, 28f), 16, Mint, TextAnchor.MiddleRight);
                var track = Add<Image>(body, names[i] + " volumen", new Rect(154f, y + 5f, 200f, 20f));
                track.color = ButtonColor;
                var area = new GameObject("Recorrido", typeof(RectTransform)).GetComponent<RectTransform>();
                area.SetParent(track.transform, false);
                area.anchorMin = new Vector2(0f, 0.5f);
                area.anchorMax = new Vector2(1f, 0.5f);
                area.offsetMin = new Vector2(8f, 0f);
                area.offsetMax = new Vector2(-8f, 0f);
                var handle = Add<Image>(area, "Control", new Rect(0f, 0f, 16f, 26f));
                handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                handle.rectTransform.anchoredPosition = Vector2.zero;
                handle.color = Mint;
                var slider = track.gameObject.AddComponent<Slider>();
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.direction = Slider.Direction.LeftToRight;
                slider.handleRect = handle.rectTransform;
                slider.targetGraphic = handle;
                slider.SetValueWithoutNotify(ShooterAudio.Volume(bus));
                percent.text = $"{slider.value:P0}";
                slider.onValueChanged.AddListener(value =>
                {
                    ShooterAudio.SetVolume(bus, value);
                    percent.text = $"{value:P0}";
                });
                UiAudioFeedback.Attach(slider);
            }
            Label(body, "Los niveles se conservan entre partidas.", new Rect(24f, 270f, 404f, 24f), 14, Muted);
            ActionButton(body, "Volver", new Rect(24f, 306f, 404f, 34f), CloseAudio);
            audioDialog.SetActive(false);
        }
    }
}
