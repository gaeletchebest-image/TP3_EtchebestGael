using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HandShooter.Editor
{
    public sealed class BalanceLabWindow : EditorWindow
    {
        [SerializeField] private BalanceProfile draft, reference;
        [SerializeField] private int tab, settingsTab, targetKind;
        [SerializeField] private float customHealth = 24f, distance = 12f;
        [SerializeField] private int group = 3, fingers = 1;
        [SerializeField] private bool ray = true, cover;
        [SerializeField] private BalanceScenario scenario;
        [SerializeField] private float spacing = 2.5f, timeLimit = 60f;
        [SerializeField, Range(0f, 1f)] private float stationStartingFuel = 0.25f;
        [SerializeField] private string profileName = "Mi balance";
        [SerializeField] private Vector2 scroll, tableScroll;
        private string notice;
        private static readonly string[] Tabs = { "Ajustes", "Comparar", "Pruebas e historial", "Perfiles" };
        private static readonly string[] TargetNames = { "Torreta", "Kamikaze", "Sniper", "Hunter", "Blanco", "Vida personalizada" };
        private static readonly string[] ScenarioNames = { "Blanco estático", "Hunter", "Sniper", "Grupo de kamikazes", "Torreta", "Grupo mixto", "Estación de llenado" };

        [MenuItem("Hand Shooter/Laboratorio de balance")]
        public static void Open()
        {
            var window = GetWindow<BalanceLabWindow>("Balance / Lab");
            window.minSize = new Vector2(720f, 520f);
            if (window.position.width <= 725f) window.position = new Rect(100f, 45f, 1100f, 630f);
            window.Show();
        }

        public static void OpenResults()
        {
            Open();
            var window = GetWindow<BalanceLabWindow>();
            window.tab = 2;
            window.scroll = Vector2.zero;
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndo;
            EditorApplication.playModeStateChanged += OnPlayState;
        }
        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo;
            EditorApplication.playModeStateChanged -= OnPlayState;
        }
        private void OnInspectorUpdate() { Repaint(); }
        private void OnUndo() { ApplyLive(); Repaint(); }
        private void OnPlayState(PlayModeStateChange state)
        {
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.43f, 260f, 360f);
            if (!draft)
            {
                if (EditorApplication.isPlaying)
                { EditorGUILayout.HelpBox("Detené Play para preparar el laboratorio por primera vez.", MessageType.Info); return; }
                draft = BalanceLabAssets.Draft();
            }
            if (!draft.IsComplete)
            { EditorGUILayout.HelpBox("El borrador está incompleto. Revisá sus referencias en el Inspector.", MessageType.Error); return; }
            if (!EditorApplication.isPlaying && !draft.refuelingStation) BalanceLabAssets.EnsureStationSettings(draft);
            GUILayout.Space(10);
            GUILayout.Label("LABORATORIO DE BALANCE", new GUIStyle(EditorStyles.boldLabel) { fontSize = 19 });
            EditorGUILayout.LabelField("BORRADOR  /  " + draft.title + "     ·     Editar → comparar → probar → guardar", EditorStyles.miniLabel);
            GUILayout.Space(8);
            tab = GUILayout.Toolbar(tab, Tabs, GUILayout.Height(28));
            if (!string.IsNullOrEmpty(notice)) EditorGUILayout.HelpBox(notice, MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(8);
            switch (tab)
            {
                case 0: DrawSettings(); break;
                case 1: DrawComparison(); break;
                case 2: DrawTrials(); break;
                case 3: DrawProfiles(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        private static void Heading(string title)
        { GUILayout.Space(10); EditorGUILayout.LabelField(title, EditorStyles.boldLabel); }

        private void DrawSettings()
        {
            EditorGUILayout.HelpBox("Editás una copia de trabajo que se conserva al cerrar Unity. El juego original cambia al pulsar «Aplicar al juego». En una prueba activa, los ajustes se leen en vivo; vida, defensa y combustible inicial se actualizan al repetir.", MessageType.Info);
            settingsTab = GUILayout.Toolbar(settingsTab, new[] { "Mano", "Combustible", "Movimiento", "Torreta", "Kamikaze", "Sniper", "Hunter" });
            EditorGUI.BeginChangeCheck();
            switch (settingsTab)
            {
                case 0:
                    Fields(draft.shooter, "Daño y ritmo", "baseDamage|Daño base", "additionalFingerMultiplier|Multiplicador por dedo adicional", "cooldownByInactiveFingers|Espera: 4, 3, 2 y 1 dedos activos (s)", "range|Alcance del rayo (m)", "automaticFire|Disparo automático", "targetHealth|Vida del blanco");
                    Fields(draft.shooter, "Proyectil", "projectileDamageMultiplier|Multiplicador de daño", "projectileCooldownMultiplier|Multiplicador de espera", "projectileFuelEfficiency|Descuento de combustible", "projectileSpeed|Velocidad base (m/s)", "projectileSpeedDamageExponent|Reducción de velocidad por daño", "projectileRadius|Radio del proyectil (m)", "projectileRange|Alcance del proyectil (m)");
                    Fields(draft.shooter, "Explosión", "explosionDamageFraction|Fracción del daño directo", "explosionBaseRadius|Radio base (m)", "explosionReferenceDamage|Daño de referencia", "explosionRadiusExponent|Crecimiento del radio", "explosionMaxRadius|Radio máximo (m)");
                    Fields(draft.shooter, "Granada opcional", "grenadeEnabled|Habilitar tercer modo", "grenadeDamageMultiplier|Multiplicador de daño", "grenadeCooldownMultiplier|Multiplicador de espera", "grenadeFuelMultiplier|Consumo respecto al rayo", "grenadeRadiusMultiplier|Radio respecto al proyectil", "grenadeFuseDuration|Mecha desde tocar el suelo (s)", "grenadeLaunchSpeed|Velocidad de lanzamiento (m/s)", "grenadeUpwardSpeed|Impulso vertical (m/s)", "grenadeGravity|Gravedad", "grenadeRadius|Radio del cuerpo (m)", "grenadeColor|Color");
                    break;
                case 1:
                    Fields(draft, "Combustible y recarga", "weapon.tankCapacity|Capacidad del tanque", "weapon.fuelPerDamage|Combustible por daño base", "weapon.startingSpareTanks|Tanques de repuesto iniciales", "weapon.reloadDuration|Duración de recarga (s)");
                    if (draft.refuelingStation)
                        Fields(draft.refuelingStation, "Estación de llenado", "fillDuration|Tiempo de 0% a 100% (s)", "interruptedFillFraction|Fracción conservada al salir (0–1)", "previewBrightness|Brillo de la barra de progreso (0–1)");
                    EditorGUILayout.HelpBox("La estación no consume ni añade tanques. Al salir antes de completar entrega la fracción configurada del combustible acumulado.", MessageType.None);
                    break;
                case 2:
                    Fields(draft.shooter, "Movimiento", "moveSpeed|Velocidad (m/s)", "dashSpeed|Velocidad del dash (m/s)", "dashDuration|Duración del dash (s)", "dashCooldown|Espera del dash (s)", "gravity|Gravedad", "fieldOfView|Campo de visión");
                    EditorGUILayout.LabelField($"Distancia aproximada del dash: {draft.shooter.dashSpeed * draft.shooter.dashDuration:0.##} m");
                    break;
                case 3:
                    Fields(draft.turret, "Torreta", "initialHealth|Vida", "detectionRange|Detección (m)", "fireInterval|Espera entre disparos (s)", "projectileSpeed|Velocidad del proyectil (m/s)", "projectileRadius|Radio del proyectil (m)", "projectileRange|Alcance del proyectil (m)"); break;
                case 4:
                    Fields(draft.kamikaze, "Kamikaze", "initialHealth|Vida", "detectionRange|Detección (m)", "moveSpeed|Velocidad (m/s)", "detonationRange|Distancia para detonar (m)", "blastRadius|Radio de explosión (m)", "blastDamage|Daño a otros enemigos"); break;
                case 5:
                    Fields(draft.sniper, "Sniper", "initialHealth|Vida", "detectionRange|Detección (m)", "moveSpeed|Velocidad (m/s)", "minimumAttackRange|Alcance mínimo (m)", "maximumAttackRange|Alcance máximo (m)", "settleDuration|Preparación (s)", "chargeDuration|Carga completa (s)", "aimLockDuration|Tiempo con dirección fija (s)", "shotCooldown|Espera entre ataques (s)"); break;
                case 6:
                    Fields(draft.hunter, "Hunter", "initialHealth|Vida", "range|Alcance de detección y ataque (m)", "chaseSpeed|Velocidad de persecución (m/s)", "acceleration|Aceleración");
                    Fields(draft.hunter, "Patrulla", "patrolRadius|Radio alrededor del centro (m)", "patrolSpeed|Velocidad de patrulla (m/s)", "patrolWaitMin|Espera mínima por punto (s)", "patrolWaitMax|Espera máxima por punto (s)");
                    break;
            }
            if (settingsTab >= 3)
            {
                UnityEngine.Object enemy = settingsTab == 3 ? (UnityEngine.Object)draft.turret : settingsTab == 4 ? draft.kamikaze : settingsTab == 5 ? draft.sniper : draft.hunter;
                Fields(enemy, "Defensa", "initialDefensePoints|Puntos de defensa iniciales", "defenseBarCapacity|Disparos por punto de defensa");
                EditorGUILayout.HelpBox("Cada punto divide el daño por 2. Cada disparo recibido resta 1 a la barra; al vaciarse se pierde un punto y se rellena para el siguiente. Cero puntos desactiva la defensa.", MessageType.None);
            }
            if (EditorGUI.EndChangeCheck()) ApplyLive();
            Heading("Márgenes de reacción a " + distance.ToString("0.#") + " m");
            string turretTravel = distance <= Mathf.Max(0.1f, draft.turret.projectileRange)
                ? $"viaje nominal {distance / Mathf.Max(0.1f, draft.turret.projectileSpeed):0.##} s"
                : "fuera del alcance del proyectil";
            EditorGUILayout.LabelField($"Torreta: {turretTravel}  ·  Sniper: dirección fija {draft.sniper.aimLockDuration:0.##} s");
            EditorGUILayout.LabelField($"Kamikaze: aproximación ideal {Mathf.Max(0f, distance - draft.kamikaze.detonationRange) / Mathf.Max(0.1f, draft.kamikaze.moveSpeed):0.##} s");
            EditorGUILayout.LabelField($"Hunter alertado: aproximación sin obstáculos ≈ {Mathf.Max(0f, distance - draft.hunter.range) / Mathf.Max(0.1f, draft.hunter.chaseSpeed):0.##} s");
            EditorGUILayout.HelpBox("Los impactos enemigos son letales. Estos tiempos orientan la prueba; cobertura, aceleración, navegación y movimiento del jugador alteran el margen real.", MessageType.None);
        }

        private static void Fields(UnityEngine.Object obj, string heading, params string[] fields)
        {
            Heading(heading);
            var serialized = new SerializedObject(obj);
            serialized.Update();
            foreach (string field in fields)
            {
                var parts = field.Split('|');
                var property = serialized.FindProperty(parts[0]);
                if (property == null) continue;
                if (parts[0] == "cooldownByInactiveFingers")
                {
                    EditorGUILayout.LabelField("Espera entre disparos", EditorStyles.boldLabel);
                    for (int i = 0; i < property.arraySize; i++)
                        EditorGUILayout.PropertyField(property.GetArrayElementAtIndex(i), new GUIContent((4 - i) + " dedos activos (s)"));
                }
                else EditorGUILayout.PropertyField(property, new GUIContent(parts[1]), true);
            }
            serialized.ApplyModifiedProperties();
        }

        private float Health(BalanceProfile p)
        {
            switch (targetKind)
            {
                case 0: return p.turret.initialHealth;
                case 1: return p.kamikaze.initialHealth;
                case 2: return p.sniper.initialHealth;
                case 3: return p.hunter.initialHealth;
                case 4: return p.shooter.targetHealth;
                default: return customHealth;
            }
        }

        private static string Number(float value, string suffix = "") => float.IsPositiveInfinity(value) ? "No viable" : value.ToString("0.##") + suffix;

        private int DefensePoints(BalanceProfile p) => targetKind == 0 ? p.turret.initialDefensePoints : targetKind == 1 ? p.kamikaze.initialDefensePoints :
            targetKind == 2 ? p.sniper.initialDefensePoints : targetKind == 3 ? p.hunter.initialDefensePoints : 0;
        private int DefenseCapacity(BalanceProfile p) => targetKind == 0 ? p.turret.defenseBarCapacity : targetKind == 1 ? p.kamikaze.defenseBarCapacity :
            targetKind == 2 ? p.sniper.defenseBarCapacity : targetKind == 3 ? p.hunter.defenseBarCapacity : 1;

        private void DrawComparison()
        {
            targetKind = EditorGUILayout.Popup("Enemigo", targetKind, TargetNames);
            if (targetKind == 5) customHealth = Mathf.Max(0.01f, EditorGUILayout.FloatField("Vida", customHealth));
            distance = Mathf.Clamp(EditorGUILayout.FloatField("Distancia (m)", distance), 0f, 100f);
            group = EditorGUILayout.IntSlider("Blancos dentro de la explosión", group, 1, 8);
            reference = (BalanceProfile)EditorGUILayout.ObjectField("Comparar con perfil", reference, typeof(BalanceProfile), false);
            EditorGUILayout.LabelField($"Vida del objetivo con el borrador: {Health(draft):0.##}");
            if (DefensePoints(draft) > 0)
                EditorGUILayout.LabelField($"Defensa: {DefensePoints(draft)} puntos · barra de {DefenseCapacity(draft)} disparos por punto");
            EditorGUILayout.HelpBox("Daño y DPS muestran la defensa inicial. Impactos, tiempo y combustible por baja incluyen la pérdida progresiva de defensa. El disparo que rompe un punto todavía recibe su reducción.", MessageType.None);
            EditorGUILayout.HelpBox("Tiempo ideal: primer disparo listo, todos los impactos directos acertados, viaje y recargas incluidos. El daño directo suma la explosión cuando tiene radio. El daño a grupos supone blancos juntos y sin cobertura. DPS sostenido supone tanques disponibles. «No viable» indica alcance de impacto directo o reservas insuficientes.", MessageType.Info);
            tableScroll = EditorGUILayout.BeginScrollView(tableScroll, GUILayout.Height(reference && reference.IsComplete ? 345f : 225f));
            string[] columns = { "Mano", "Directo", "Área / radio", "Espera", "DPS", "DPS + rec.", "Impactos", "Tiempo", "Fuel/baja", "Tiros/tanque", "Grupo/tiro" };
            int[] widths = { 105, 65, 100, 65, 60, 80, 65, 85, 75, 85, 85 };
            Row(columns, widths, true);
            for (int mode = 0; mode < 2; mode++)
                for (int count = 1; count <= 4; count++)
                {
                    var e = BalanceCalculator.Evaluate(draft, count, mode == 0, Health(draft), distance, group, DefensePoints(draft), DefenseCapacity(draft));
                    Row(new[] { count + (count == 1 ? " dedo / " : " dedos / ") + (mode == 0 ? "rayo" : "proy."), Number(e.DirectDamage),
                        Number(e.SplashDamage) + " / " + Number(e.Radius, "m"), Number(e.Interval, "s"), Number(e.DamagePerSecond),
                        Number(e.SustainedDps), e.ShotsToKill.ToString(), Number(e.TimeToKill, "s"), Number(e.FuelPerKill),
                        e.ShotsPerTank == int.MaxValue ? "∞" : e.ShotsPerTank.ToString(), Number(e.GroupDamage) }, widths, false);
                    if (reference && reference.IsComplete)
                    {
                        var b = BalanceCalculator.Evaluate(reference, count, mode == 0, Health(reference), distance, group, DefensePoints(reference), DefenseCapacity(reference));
                        EditorGUILayout.LabelField($"    Frente a {reference.title}: tiempo {Number(b.TimeToKill, "s")} → {Number(e.TimeToKill, "s")} · impactos {b.ShotsToKill} → {e.ShotsToKill} · combustible {b.FuelPerKill:0.##} → {e.FuelPerKill:0.##}", EditorStyles.miniLabel, GUILayout.Width(990));
                    }
                }
            EditorGUILayout.EndScrollView();
            if (reference && reference.IsComplete)
                EditorGUILayout.LabelField($"El perfil de referencia usa su propia vida del enemigo ({Health(reference):0.##}); «Vida personalizada» fija la misma vida para ambos.", EditorStyles.wordWrappedMiniLabel);
        }

        private static void Row(string[] cells, int[] widths, bool header)
        {
            EditorGUILayout.BeginHorizontal(header ? EditorStyles.toolbar : GUIStyle.none);
            for (int i = 0; i < cells.Length; i++) GUILayout.Label(cells[i], header ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.Width(widths[i]));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTrials()
        {
            Heading("Encuentro repetible");
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                scenario = (BalanceScenario)EditorGUILayout.Popup("Escenario", (int)scenario, ScenarioNames);
                using (new EditorGUI.DisabledScope(scenario == BalanceScenario.EstacionDeLlenado))
                {
                    distance = EditorGUILayout.Slider("Distancia inicial (m)", distance, 3f, 40f);
                    group = EditorGUILayout.IntSlider("Cantidad de enemigos", group, 1, 8);
                    spacing = EditorGUILayout.Slider("Separación (m)", spacing, 1.5f, 5f);
                    cover = EditorGUILayout.Toggle("Cobertura intermedia", cover);
                }
                if (scenario == BalanceScenario.EstacionDeLlenado)
                    stationStartingFuel = EditorGUILayout.Slider("Combustible inicial (fracción)", stationStartingFuel, 0f, 1f);
                timeLimit = EditorGUILayout.Slider("Límite (s)", timeLimit, 5f, 180f);
                fingers = EditorGUILayout.IntSlider("Dedos iniciales", fingers, 1, 4);
                ray = EditorGUILayout.Toggle("Pulgar activo / rayo", ray);
                if (GUILayout.Button("Probar borrador", GUILayout.Height(34))) StartTrial();
            }
            var live = FindFirstObjectByType<BalanceLabSession>();
            if (EditorApplication.isPlaying && live)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Repetir encuentro")) live.RestartTrial();
                if (GUILayout.Button("Finalizar prueba")) live.Finish("Finalizada manualmente");
                if (GUILayout.Button("Volver a edición")) EditorApplication.isPlaying = false;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.HelpBox("WASD + mouse, clic para disparar, R para recargar, Tab para cambiar la mano y F5 para repetir. Cada repetición restablece posiciones, enemigos, combustible y espera del arma. Se conservan las últimas 100 pruebas localmente.", MessageType.Info);
            if (scenario == BalanceScenario.Hunter) EditorGUILayout.HelpBox("El hunter patrulla alrededor de su punto inicial y espera un tiempo aleatorio en cada destino. Al recibir daño te persigue; al verte dentro de su alcance dispara.", MessageType.None);
            if (scenario == BalanceScenario.EstacionDeLlenado) EditorGUILayout.HelpBox("Estación a 4 m, sin enemigos. Entrá en la base y observá la barra oscura detrás del combustible. Salí antes de completar para probar la carga parcial. Dispará para gastar combustible y repetí; F5 restablece la prueba.", MessageType.None);
            if (scenario == BalanceScenario.Blanco) EditorGUILayout.HelpBox("Los blancos tienen la vida configurada en Mano. Usan el cuerpo de la torreta y permanecen inmóviles; acercalos para probar explosiones.", MessageType.None);
            Heading("Historial de pruebas");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Exportar CSV")) Export(false);
            if (GUILayout.Button("Exportar JSON con ajustes")) Export(true);
            EditorGUILayout.EndHorizontal();
            var results = BalanceLabHistory.Trials;
            if (results.Count == 0) EditorGUILayout.LabelField("Todavía no hay pruebas registradas.");
            foreach (var r in results.Take(20))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                string date = DateTimeOffset.TryParse(r.utc, out var parsed) ? parsed.ToOffset(TimeSpan.FromHours(-3)).ToString("dd/MM HH:mm") + " (UTC−3)" : r.utc;
                EditorGUILayout.LabelField($"{date} · {r.profile} · {r.scenario} · {r.outcome}" + (string.IsNullOrEmpty(r.deathCause) ? "" : " / " + r.deathCause), EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{r.seconds:0.00}s  ·  {r.shots} disparos  ·  {r.Accuracy:P0} aciertos  ·  {r.kills} bajas  ·  {r.fuelSpent:0.##} combustible  ·  {r.reloads} recargas");
                if (r.settingsChanged) EditorGUILayout.LabelField("Ajustes editados durante la prueba: no usar para una comparación A/B directa.", EditorStyles.wordWrappedMiniLabel);
                var previous = results.SkipWhile(t => t.id != r.id).Skip(1).FirstOrDefault(t => t.setup == r.setup && !t.settingsChanged && !r.settingsChanged && t.outcome == r.outcome);
                if (previous != null) EditorGUILayout.LabelField($"Frente a la prueba anterior con igual escenario y resultado: tiempo {r.seconds - previous.seconds:+0.00;-0.00;0}s · combustible {r.fuelSpent - previous.fuelSpent:+0.##;-0.##;0}", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
            }
        }

        public void StartTrial()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            // Unity's normal scene-save prompt protects any unrelated scene edits.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.SaveAssets();
            BalanceLabAssets.EnsureScene(draft);
            string previous = SceneManager.GetActiveScene().path;
            SessionState.SetString("BalanceLab.PreviousScene", previous);
            SessionState.SetBool("BalanceLab.RestoreScene", previous != BalanceLabAssets.ScenePath);
            SessionState.SetBool("BalanceLab.ReturnToPanel", true);
            var lab = EditorSceneManager.OpenScene(BalanceLabAssets.ScenePath);
            var session = lab.GetRootGameObjects().Select(g => g.GetComponent<BalanceLabSession>()).First(s => s);
            session.profile = draft; session.scenario = scenario; session.distance = distance;
            session.enemyCount = group; session.spacing = spacing; session.cover = cover;
            session.timeLimit = timeLimit; session.fingers = fingers; session.ray = ray;
            session.stationStartingFuel = stationStartingFuel;
            EditorUtility.SetDirty(session);
            EditorSceneManager.SaveScene(lab);
            EditorApplication.isPlaying = true;
            Close();
            EditorApplication.ExecuteMenuItem("Window/General/Game");
        }

        private void Export(bool json)
        {
            string path = EditorUtility.SaveFilePanel("Exportar pruebas", Application.dataPath + "/../", "BalanceTrials", json ? "json" : "csv");
            if (!string.IsNullOrEmpty(path)) { File.WriteAllText(path, json ? BalanceLabHistory.Json() : BalanceLabHistory.Csv(), new System.Text.UTF8Encoding(true)); notice = "Pruebas exportadas a " + path; }
        }

        private void ApplyLive()
        {
            if (EditorApplication.isPlaying)
            {
                var live = FindFirstObjectByType<BalanceLabSession>();
                if (live) live.ApplyLive(draft);
            }
        }

        private void DrawProfiles()
        {
            Fields(draft, "Borrador actual", "title|Nombre", "notes|Notas / hipótesis a probar");
            Heading("Guardar una versión");
            profileName = EditorGUILayout.TextField("Nombre del archivo", profileName);
            if (GUILayout.Button("Guardar como nuevo perfil"))
            {
                string name = string.Concat(profileName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
                if (string.IsNullOrEmpty(name)) name = "Balance";
                string path = AssetDatabase.GenerateUniqueAssetPath(BalanceLabAssets.ProfilesFolder + "/" + name + ".asset");
                reference = BalanceLabAssets.CreateCopy(draft, path);
                notice = "Perfil guardado: " + path;
            }
            Heading("Versiones guardadas");
            foreach (var guid in AssetDatabase.FindAssets("t:BalanceProfile", new[] { BalanceLabAssets.ProfilesFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<BalanceProfile>(path);
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(path) + " · " + profile.title);
                if (GUILayout.Button("Comparar", GUILayout.Width(85))) { reference = profile; tab = 1; }
                if (GUILayout.Button("Cargar", GUILayout.Width(70))) { BalanceLabAssets.LoadInto(profile, draft); ApplyLive(); notice = "Borrador cargado desde " + profile.title; }
                EditorGUILayout.EndHorizontal();
            }
            Heading("Diferencias frente a un perfil");
            reference = (BalanceProfile)EditorGUILayout.ObjectField("Referencia", reference, typeof(BalanceProfile), false);
            if (reference && reference.IsComplete)
            {
                foreach (string diff in Differences(reference, draft)) EditorGUILayout.LabelField(diff, EditorStyles.wordWrappedLabel);
            }
            Heading("Aplicar al juego");
            EditorGUILayout.HelpBox("Copia el borrador a los ajustes del juego, incluida la estación de llenado, y al combustible del arma de la escena original. Los ajustes se guardan; si la escena original está abierta, guardala para conservar el combustible. Podés volver atrás cargando y aplicando «Base inicial».", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Aplicar borrador al juego", GUILayout.Height(32)))
                { BalanceLabAssets.ApplyToGame(draft); notice = "Balance aplicado al juego. Si la escena original está abierta, guardala para conservar el combustible."; }
        }

        public static IEnumerable<string> Differences(BalanceProfile baseline, BalanceProfile current)
        {
            UnityEngine.Object[] a = { baseline.shooter, baseline.turret, baseline.kamikaze, baseline.sniper, baseline.hunter, baseline, baseline.refuelingStation };
            UnityEngine.Object[] b = { current.shooter, current.turret, current.kamikaze, current.sniper, current.hunter, current, current.refuelingStation };
            string[] names = { "Mano", "Torreta", "Kamikaze", "Sniper", "Hunter", "Combustible", "Estación" };
            int count = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i] || !b[i]) continue;
                var left = new SerializedObject(a[i]);
                var right = new SerializedObject(b[i]);
                var p = left.GetIterator();
                while (p.NextVisible(true))
                {
                    if (p.propertyPath.StartsWith("m_") || (i == 5 && !p.propertyPath.StartsWith("weapon."))) continue;
                    var other = right.FindProperty(p.propertyPath);
                    if (other == null) continue;
                    string before = PropertyValue(p), after = PropertyValue(other);
                    if (before == null || before == after) continue;
                    count++;
                    yield return $"{names[i]} / {p.propertyPath}: {before} → {after}";
                }
            }
            if (count == 0) yield return "Sin diferencias de parámetros.";
        }

        private static string PropertyValue(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Float: return p.floatValue.ToString("G9");
                case SerializedPropertyType.Integer: return p.intValue.ToString();
                case SerializedPropertyType.Boolean: return p.boolValue ? "Sí" : "No";
                case SerializedPropertyType.ArraySize: return p.intValue.ToString();
                default: return null;
            }
        }
    }
}
