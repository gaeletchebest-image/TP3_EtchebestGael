using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HandShooter
{
    internal sealed class UiAudioFeedback : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        public void OnPointerEnter(PointerEventData data) => Select();
        public void OnSelect(BaseEventData data) => Select();
        private void Select()
        {
            var selectable = GetComponent<Selectable>();
            if (selectable && selectable.IsInteractable()) ShooterAudio.Ui(SoundCue.UiSelect);
        }
        internal static void Attach(Selectable control)
        {
            if (control && !control.GetComponent<UiAudioFeedback>()) control.gameObject.AddComponent<UiAudioFeedback>();
        }
    }
}
