using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HandShooter
{
    [DefaultExecutionOrder(-100)]
    public sealed class BalanceLabSession : MonoBehaviour
    {
        public BalanceProfile profile;
        public PrototypePlayer player;
        public GameObject turretPrefab, kamikazePrefab, sniperPrefab, hunterPrefab;
        public BalanceScenario scenario;
        [Min(2f)] public float distance = 12f;
        [Range(1, 8)] public int enemyCount = 3;
        [Min(1.5f)] public float spacing = 2.5f;
        public bool cover;
        [Min(5f)] public float timeLimit = 60f;
        [Range(1, 4)] public int fingers = 1;
        public bool ray = true;
        [NonSerialized] public bool publishResults = true;
        public static event Action<BalanceTrialResult> TrialFinished;
        public static event Action ExitRequested;
        public BalanceTrial Trial { get; private set; }
        public BalanceProfile RuntimeProfile { get; private set; }
        private readonly List<DamageTarget> targets = new List<DamageTarget>();
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private float startedAt;
        private bool ready;
        private GUIStyle hudText;

        [Serializable]
        private sealed class Setup
        {
            public BalanceScenario scenario;
            public float distance, spacing, timeLimit;
            public int enemyCount, fingers;
            public bool cover, ray;
        }

        private T Clone<T>(T source) where T : UnityEngine.Object
        {
            var copy = Instantiate(source);
            owned.Add(copy);
            return copy;
        }

        private void Awake()
        {
            if (!profile || !profile.IsComplete || !player)
            { Debug.LogError("El laboratorio necesita un perfil completo y un jugador.", this); enabled = false; return; }
            RuntimeProfile = Clone(profile);
            RuntimeProfile.shooter = Clone(profile.shooter);
            RuntimeProfile.turret = Clone(profile.turret);
            RuntimeProfile.kamikaze = Clone(profile.kamikaze);
            RuntimeProfile.sniper = Clone(profile.sniper);
            RuntimeProfile.hunter = Clone(profile.hunter);
            RuntimeProfile.weapon = JsonUtility.FromJson<WeaponBalance>(JsonUtility.ToJson(profile.weapon));
            player.settings = player.weapon.settings = RuntimeProfile.shooter;
            player.spawnDefaultFuelPickups = false;
            RuntimeProfile.weapon.Apply(player.weapon);
            RuntimeProfile.shooter.initialActiveFingers = fingers;
            RuntimeProfile.shooter.initialThumbActive = ray;
        }

        private IEnumerator Start()
        {
            // Let player/weapon/HUD initialize before subscribing or starting the clock.
            yield return null;
            enemyCount = Mathf.Clamp(enemyCount, 1, 8);
            var setup = new Setup { scenario = scenario, distance = distance, spacing = spacing,
                timeLimit = timeLimit, enemyCount = enemyCount, fingers = fingers, cover = cover, ray = ray };
            Trial = new BalanceTrial(new BalanceTrialResult
            {
                id = Guid.NewGuid().ToString("N"), utc = DateTime.UtcNow.ToString("O"),
                profile = profile.title, scenario = scenario.ToString(), setup = JsonUtility.ToJson(setup),
                balanceSnapshot = RuntimeProfile.Snapshot()
            });
            player.weapon.ShotFired += OnShot;
            player.weapon.DamageDealt += OnDamage;
            player.weapon.Reloaded += OnReload;
            player.Died += OnDeath;
            SpawnEncounter();
            startedAt = Time.time;
            ready = true;
        }

        private void SpawnEncounter()
        {
            var root = new GameObject("Encuentro");
            root.transform.SetParent(transform);
            root.SetActive(false);
            if (cover)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Cobertura";
                wall.transform.SetParent(root.transform);
                wall.transform.position = new Vector3(0f, 1.5f, distance * 0.5f);
                wall.transform.localScale = new Vector3(3f, 3f, 0.5f);
            }
            int count = enemyCount;
            for (int i = 0; i < count; i++)
            {
                BalanceScenario kind = scenario == BalanceScenario.GrupoMixto ?
                    new[] { BalanceScenario.Hunter, BalanceScenario.Sniper, BalanceScenario.Kamikazes, BalanceScenario.Torreta }[i % 4] : scenario;
                GameObject prefab = kind == BalanceScenario.Hunter ? hunterPrefab : kind == BalanceScenario.Sniper ? sniperPrefab :
                    kind == BalanceScenario.Kamikazes ? kamikazePrefab : turretPrefab;
                var enemy = Instantiate(prefab, root.transform);
                enemy.transform.position = new Vector3((i - (count - 1) * 0.5f) * spacing, 1f, distance);
                enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                var health = enemy.GetComponent<DamageTarget>();
                health.settings = RuntimeProfile.shooter;
                health.turretSettings = null; health.kamikazeSettings = null;
                health.sniperSettings = null; health.hunterSettings = null;
                var turret = enemy.GetComponent<TurretEnemy>();
                var kamikaze = enemy.GetComponent<KamikazeEnemy>();
                var sniper = enemy.GetComponent<SniperEnemy>();
                var hunter = enemy.GetComponent<HunterEnemy>();
                if (kind == BalanceScenario.Blanco)
                {
                    turret.enabled = false;
                    enemy.name = "Blanco de balance";
                }
                else if (turret) { health.turretSettings = turret.settings = RuntimeProfile.turret; turret.target = player; }
                else if (kamikaze) { health.kamikazeSettings = kamikaze.settings = RuntimeProfile.kamikaze; kamikaze.target = player; }
                else if (sniper) { health.sniperSettings = sniper.settings = RuntimeProfile.sniper; sniper.target = player; }
                else if (hunter) { health.hunterSettings = hunter.settings = RuntimeProfile.hunter; hunter.target = player; }
                targets.Add(health);
            }
            root.SetActive(true);
            Physics.SyncTransforms();
        }

        private void OnShot(int id, float fuel) => Trial.Shot(id, fuel);
        private void OnDamage(int id, DamageTarget target, float amount, bool killed) => Trial.Damage(id, amount, killed);
        private void OnReload() => Trial.Reload();
        private void OnDeath(string cause) => Finish("Muerte", cause);

        private void Update()
        {
            if (!ready) return;
            if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame) { RestartTrial(); return; }
            if (Trial.Finished) return;
            Trial.Result.seconds = Time.time - startedAt;
            bool cleared = targets.Count > 0 && targets.TrueForAll(t => !t || t.Health <= 0f);
            if (cleared) Finish("Superado");
            else if (Trial.Result.seconds >= timeLimit) Finish("Tiempo agotado");
        }

        public void Finish(string outcome = "Interrumpido", string cause = "")
        {
            if (Trial == null || !Trial.Finish(outcome, Time.time - startedAt, cause)) return;
            Trial.Result.finalBalanceSnapshot = RuntimeProfile.Snapshot();
            if (publishResults) TrialFinished?.Invoke(Trial.Result);
            if (outcome != "Muerte" && outcome != "Interrumpido")
            {
                player.SetMenuOpen(false);
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void RestartTrial()
        {
            Finish();
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameObject.scene.path);
        }

        // Only runtime copies are changed. Health, defense and initial fuel take effect on the next trial.
        public void ApplyLive(BalanceProfile source)
        {
            if (!RuntimeProfile || !source || !source.IsComplete) return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source.shooter), RuntimeProfile.shooter);
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source.turret), RuntimeProfile.turret);
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source.kamikaze), RuntimeProfile.kamikaze);
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source.sniper), RuntimeProfile.sniper);
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source.hunter), RuntimeProfile.hunter);
            RuntimeProfile.weapon = JsonUtility.FromJson<WeaponBalance>(JsonUtility.ToJson(source.weapon));
            RuntimeProfile.title = source.title;
            RuntimeProfile.notes = source.notes;
            RuntimeProfile.weapon.Apply(player.weapon);
            if (Trial != null && !Trial.Finished) Trial.Result.settingsChanged = true;
        }

        private void OnGUI()
        {
            if (!ready || Trial == null) return;
            if (hudText == null) hudText = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, normal = { textColor = Color.white } };
            GUI.depth = -100;
            // Match the reference height of the existing HUD at higher Game View resolutions.
            float scale = Mathf.Max(1f, Screen.height / 768f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUILayout.BeginArea(new Rect(16, 16, 325, Trial.Finished ? 310 : 200), GUI.skin.box);
            GUILayout.Label("LABORATORIO / " + scenario, hudText);
            var r = Trial.Result;
            GUILayout.Label($"{r.seconds:0.0} s   ·   {r.kills} bajas\n{r.shots} disparos   ·   {r.Accuracy:P0} aciertos\n{r.fuelSpent:0.##} combustible   ·   {r.reloads} recargas", hudText);
            if (r.settingsChanged) GUILayout.Label("Ajustes cambiados durante esta prueba", hudText);
            if (Trial.Finished)
            {
                GUILayout.Label(r.outcome + (string.IsNullOrEmpty(r.deathCause) ? "" : " / " + r.deathCause), hudText);
                if (GUILayout.Button("Repetir prueba (F5)", GUILayout.Height(32))) RestartTrial();
                if (ExitRequested != null && GUILayout.Button("Volver al laboratorio", GUILayout.Height(28))) ExitRequested.Invoke();
            }
            else if (GUILayout.Button("Finalizar y ver resultado")) Finish("Finalizada manualmente");
            GUILayout.Label("F5: repetir · Tab: mano / pausa\nResultados y perfiles en la ventana de Unity", hudText);
            GUILayout.EndArea();
        }

        private void OnDisable()
        {
            if (!ready) return;
            Finish();
            player.weapon.ShotFired -= OnShot;
            player.weapon.DamageDealt -= OnDamage;
            player.weapon.Reloaded -= OnReload;
            player.Died -= OnDeath;
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            foreach (var obj in owned) if (obj) Destroy(obj);
        }
    }
}
