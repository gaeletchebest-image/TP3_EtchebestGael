using System.Collections.Generic;
using HandShooter.CRT;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HandShooter
{
    internal sealed class PlayerDeathPresentation
    {
        private const float FlightDuration = 0.85f;
        private const float CameraRadius = 0.18f;
        private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
        private readonly GameObject overlay, footer;
        private readonly Image shade;
        private readonly Camera view, handCamera;
        private readonly bool handCameraWasEnabled;
        private readonly CRTCameraEffect crt;
        private readonly CRTDeathTransition style;
        private readonly Pose initial, destination;
        private readonly Vector3 approach;
        private readonly bool flightBlocked;
        private readonly float startedAt, signalDuration, wallCutTime;
        private readonly AudioClip transitionSound;
        private bool settled;

        public PlayerDeathPresentation(PrototypePlayer player, string cause, string detail, Transform source,
            Vector3? origin, CRTDeathTransition transitionStyle)
        {
            view = player.viewCamera;
            handCamera = player.weapon ? player.weapon.FirstPersonCamera : null;
            handCameraWasEnabled = handCamera && handCamera.enabled;
            if (handCamera) handCamera.enabled = false;
            initial = new Pose(view.transform.position, view.transform.rotation);
            Vector3 playerCenter = player.GetComponent<CharacterController>().bounds.center;
            destination = PlaceCamera(origin ?? playerCenter, playerCenter, source);
            approach = StopBeforeWall(initial.position, destination.position, player.transform);
            flightBlocked = (approach - destination.position).sqrMagnitude > 0.001f;
            crt = view.GetComponent<CRTCameraEffect>();
            style = transitionStyle;
            signalDuration = style == CRTDeathTransition.PowerOff ? 0.54f : 0.70f;
            wallCutTime = signalDuration * (style == CRTDeathTransition.PowerOff ? 0.94f : 0.39f);
            startedAt = Time.unscaledTime;

            var scene = player.gameObject.scene;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.gameObject.scene != scene || !canvas.isRootCanvas || !canvas.enabled ||
                    canvas.renderMode == RenderMode.WorldSpace) continue;
                hiddenCanvases.Add(canvas);
                canvas.enabled = false;
            }

            overlay = new GameObject("Player Death Review", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(AudioSource));
            SceneManager.MoveGameObjectToScene(overlay, scene);
            var rootCanvas = overlay.GetComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = 100;
            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1366f, 768f);
            scaler.matchWidthOrHeight = 0.5f;
            transitionSound = MakeTransitionSound(style);
            overlay.GetComponent<AudioSource>().PlayOneShot(transitionSound, 0.6f);

            shade = Panel(overlay.transform, "Death Shade", new Color(0f, 0f, 0f, 0.14f));
            var panel = Panel(overlay.transform, "Death Summary", new Color(0.026f, 0.036f, 0.043f, 0.97f));
            footer = panel.gameObject;
            panel.rectTransform.anchorMax = Vector2.right;
            panel.rectTransform.offsetMax = new Vector2(0f, 116f);
            var rule = Panel(footer.transform, "Accent", new Color(1f, 0.39f, 0.23f));
            rule.rectTransform.anchorMin = Vector2.up;
            rule.rectTransform.offsetMin = new Vector2(0f, -2f);
            rule.rectTransform.offsetMax = Vector2.zero;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Label(footer.transform, font, "Cause", $"ELIMINADO  /  {cause?.ToUpperInvariant() ?? "ENEMIGO"}", 27,
                Color.white, 66f, 104f).fontStyle = FontStyle.Bold;
            Label(footer.transform, font, "Attack Details", detail, 19,
                new Color(0.77f, 0.84f, 0.87f), 39f, 68f);
            Label(footer.transform, font, "Review Controls", "ENTER  REINTENTAR", 15,
                new Color(0.50f, 0.95f, 0.82f), 10f, 37f);
            footer.SetActive(false);
        }

        public bool Tick()
        {
            if (settled || !view) return true;

            float elapsed = Time.unscaledTime - startedAt;
            float flight = Mathf.SmoothStep(0f, 1f, elapsed / FlightDuration);
            Vector3 position = Vector3.Lerp(initial.position, destination.position, flight);
            Quaternion rotation = Quaternion.Slerp(initial.rotation, destination.rotation, flight);
            if (flightBlocked)
            {
                // Cut while the CRT signal is weakest if the direct path crosses scenery.
                position = elapsed < wallCutTime
                    ? Vector3.Lerp(initial.position, approach, Mathf.SmoothStep(0f, 1f, elapsed / wallCutTime))
                    : destination.position;
                if (elapsed >= wallCutTime) rotation = destination.rotation;
            }
            view.transform.SetPositionAndRotation(position, rotation);

            float signal = Mathf.Clamp01(elapsed / signalDuration);
            if (crt && crt.IsActive)
            {
                if (signal < 1f) crt.SetDeathTransition(style, signal);
                else crt.ClearDeathTransition();
            }
            else shade.color = new Color(0f, 0f, 0f, 0.14f + 0.72f * Mathf.Sin(signal * Mathf.PI));

            if (elapsed < FlightDuration) return false;
            settled = true;
            shade.color = new Color(0f, 0f, 0f, 0.18f);
            footer.SetActive(true);
            return true;
        }

        private Pose PlaceCamera(Vector3 subject, Vector3 playerCenter, Transform source)
        {
            Vector3 forward = Vector3.ProjectOnPlane(playerCenter - subject, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(initial.rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 anchor = subject + Vector3.up * 0.48f;
            Vector3 back = -forward * 3.6f + Vector3.up * 0.9f;
            Vector3 position = StopBeforeWall(anchor, anchor + back + right * 1.35f, source);
            if ((position - anchor).sqrMagnitude < 9f)
            {
                Vector3 other = StopBeforeWall(anchor, anchor + back - right * 1.35f, source);
                if ((other - anchor).sqrMagnitude > (position - anchor).sqrMagnitude) position = other;
                if ((position - anchor).sqrMagnitude < 1f)
                {
                    Vector3 above = StopBeforeWall(anchor, anchor + Vector3.up * 2.2f - forward * 0.38f, source);
                    if ((above - anchor).sqrMagnitude > (position - anchor).sqrMagnitude) position = above;
                }
            }
            float lookAhead = Mathf.Clamp(Vector3.Distance(subject, playerCenter) * 0.30f, 0.8f, 2.8f);
            Vector3 aim = subject + Vector3.up * 0.2f + forward * lookAhead;
            return new Pose(position, Quaternion.LookRotation(aim - position, Vector3.up));
        }

        private static Vector3 StopBeforeWall(Vector3 start, Vector3 end, Transform excluded)
        {
            Vector3 offset = end - start;
            float length = offset.magnitude;
            if (length < 0.001f) return end;

            Vector3 direction = offset / length;
            float safeDistance = length;
            foreach (var hit in Physics.SphereCastAll(start, CameraRadius, direction, length,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (excluded && hit.transform.IsChildOf(excluded)) continue;
                safeDistance = Mathf.Min(safeDistance, Mathf.Max(0f, hit.distance - 0.06f));
            }
            return start + direction * safeDistance;
        }

        internal static string Describe(string cause, Camera camera, Vector3? origin)
        {
            if (!origin.HasValue || !camera) return cause == "Tiempo agotado" ? "Se agotó el tiempo." : "Fin de la partida.";

            Vector3 offset = origin.Value - camera.transform.position;
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, camera.transform.eulerAngles.y, 0f)) * offset;
            float horizontal = new Vector2(local.x, local.z).magnitude;
            if (offset.sqrMagnitude < 0.01f) return "Ataque a corta distancia.";
            if (Mathf.Abs(local.y) > horizontal * 1.4f) return local.y > 0f ? "Ataque desde arriba." : "Ataque desde abajo.";

            int sector = (Mathf.RoundToInt(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg / 45f) + 8) % 8;
            string direction = sector switch
            {
                0 => "el frente", 1 => "adelante a la derecha", 2 => "la derecha", 3 => "atrás a la derecha",
                4 => "atrás", 5 => "atrás a la izquierda", 6 => "la izquierda", _ => "adelante a la izquierda"
            };
            return $"Ataque desde {direction}.";
        }

        public void Dispose()
        {
            if (crt) crt.ClearDeathTransition();
            if (view) view.transform.SetPositionAndRotation(initial.position, initial.rotation);
            if (handCamera) handCamera.enabled = handCameraWasEnabled;
            foreach (var canvas in hiddenCanvases)
                if (canvas) canvas.enabled = true;
            if (transitionSound) Object.Destroy(transitionSound);
            if (overlay) Object.Destroy(overlay);
        }

        private static Image Panel(Transform parent, string name, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text Label(Transform parent, Font font, string name, string value, int size, Color tint,
            float bottom, float top)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            RectTransform rect = label.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.right;
            rect.offsetMin = new Vector2(34f, bottom);
            rect.offsetMax = new Vector2(-34f, top);
            label.font = font;
            label.fontSize = size;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = size;
            label.color = tint;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            label.text = value;
            return label;
        }

        private static AudioClip MakeTransitionSound(CRTDeathTransition style)
        {
            const int rate = 22050;
            var samples = new float[rate / 6];
            var noise = new System.Random(422);
            bool powerOff = style == CRTDeathTransition.PowerOff;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)rate;
                float hiss = (float)noise.NextDouble() * 2f - 1f;
                float attack = Mathf.Min(time * 300f, 1f);
                float envelope = attack * Mathf.Exp(-time * (powerOff ? 39f : 24f));
                float hum = Mathf.Sin(time * (powerOff ? 96f : 180f) * 2f * Mathf.PI);
                samples[i] = (hiss * (powerOff ? 0.17f : 0.54f) + hum * 0.27f) * envelope;
            }
            var clip = AudioClip.Create("CRT Signal Cut", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
