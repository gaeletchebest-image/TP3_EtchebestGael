using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace HandShooter.Editor
{
    public static class PrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/HandShooterPrototype.unity";
        public const string SettingsPath = "Assets/HandShooter/Settings/ShooterSettings.asset";
        public const string TurretPrefabPath = "Assets/HandShooter/Prefabs/EnemyTurret.prefab";
        public const string KamikazePrefabPath = "Assets/HandShooter/Prefabs/EnemyKamikaze.prefab";

        [InitializeOnLoadMethod]
        private static void EnsureSceneObjectsOnEditorLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != ScenePath) return;
                var scene = SceneManager.GetActiveScene();
                EnsureWeaponPresetMenu();
                EnsureKamikaze();
                SniperSetup.EnsureSniper();
                HunterSetup.EnsureHunter();
                var player = Object.FindFirstObjectByType<PrototypePlayer>();
                if (player && (!player.deathScreen || !player.deathHint)) EnsureDeathScreen();
                var turret = Object.FindFirstObjectByType<TurretEnemy>();
                if (turret && !PrefabUtility.IsPartOfPrefabInstance(turret.gameObject))
                {
                    ConfigureTurretVisuals(turret);
                    ConvertTurretToPrefab(turret);
                }
                var kamikaze = Object.FindFirstObjectByType<KamikazeEnemy>();
                if (kamikaze && !PrefabUtility.IsPartOfPrefabInstance(kamikaze.gameObject))
                {
                    ConfigureKamikazeVisuals(kamikaze);
                    ConvertKamikazeToPrefab(kamikaze);
                }
                if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            };
        }

        [MenuItem("Hand Shooter/Open or Create Prototype")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode before creating the prototype."); return; }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
            {
                var existingScene = EditorSceneManager.OpenScene(ScenePath);
                EnsureWeaponPresetMenu();
                EnsureFuelPickups();
                EnsureTurret();
                EnsureKamikaze();
                SniperSetup.EnsureSniper();
                HunterSetup.EnsureHunter();
                EnsureDeathScreen();
                ConvertTurretToPrefab();
                ConvertKamikazeToPrefab();
                EditorSceneManager.SaveScene(existingScene);
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<ShooterSettings>(SettingsPath);
                return;
            }
            if (SceneManager.GetActiveScene().isDirty)
            { Debug.LogError("Save the current scene before creating the prototype."); return; }
            var settings = AssetDatabase.LoadAssetAtPath<ShooterSettings>(SettingsPath);
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<ShooterSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            Material floor = Material("Floor", new Color(0.095f, 0.13f, 0.15f));
            Material wall = Material("Wall", new Color(0.12f, 0.19f, 0.22f));
            Material grid = Material("Grid", new Color(0.15f, 0.24f, 0.27f));
            Material accent = Material("Accent", new Color(0.19f, 0.76f, 0.65f), true);
            Material targetMat = Material("Target", new Color(0.9f, 0.39f, 0.16f));
            Material handMat = Material("Hand", new Color(0.2f, 0.42f, 0.46f));
            Material fingerMat = Material("Fingers", new Color(0.4f, 0.85f, 0.74f));
            settings.beamMaterial = Material("Beam", Color.white, true, "Universal Render Pipeline/Particles/Unlit");
            var projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.name = "HandProjectile";
            Object.DestroyImmediate(projectileObject.GetComponent<Collider>());
            projectileObject.layer = 2;
            projectileObject.GetComponent<Renderer>().sharedMaterial = Material("Projectile", settings.projectileColor, true);
            projectileObject.AddComponent<HandProjectile>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(projectileObject, "Assets/HandShooter/Prefabs/HandProjectile.prefab");
            Object.DestroyImmediate(projectileObject);
            settings.projectilePrefab = prefab.GetComponent<HandProjectile>();
            EditorUtility.SetDirty(settings);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.65f, 0.75f, 0.8f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(0.055f, 0.09f, 0.12f);
            RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 24; RenderSettings.fogEndDistance = 65;
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.5f; light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            var arena = new GameObject("Test Range").transform;
            CreateFuelPickups();
            Cube("Floor", new Vector3(0, -0.25f, 10), new Vector3(32, 0.5f, 40), floor, arena);
            Cube("Backstop", new Vector3(0, 3f, 30), new Vector3(32, 6, 0.5f), wall, arena);
            Cube("Left wall", new Vector3(-16, 2, 10), new Vector3(0.5f, 4, 40), wall, arena);
            Cube("Right wall", new Vector3(16, 2, 10), new Vector3(0.5f, 4, 40), wall, arena);
            Cube("Entrance wall", new Vector3(0, 2, -10), new Vector3(32, 4, 0.5f), wall, arena);
            for (int x = -14; x <= 14; x += 2) Cube("Floor grid", new Vector3(x, 0.006f, 10), new Vector3(0.02f, 0.008f, 39), grid, arena, false);
            for (int z = -8; z < 30; z += 2) Cube("Floor grid", new Vector3(0, 0.006f, z), new Vector3(31, 0.008f, 0.02f), grid, arena, false);
            Cube("Range line", new Vector3(0, 0.014f, 2), new Vector3(31, 0.02f, 0.06f), accent, arena, false);
            Cube("Backstop accent", new Vector3(0, 3.9f, 29.72f), new Vector3(28, 0.05f, 0.04f), accent, arena, false);
            WorldText("HAND / LAB", new Vector3(0, 4.8f, 29.65f), 0.13f, Color.white, arena);
            WorldText("FINGER WEAPON TEST RANGE", new Vector3(0, 4.1f, 29.65f), 0.04f, Color.white, arena);
            Vector3[] positions = { new Vector3(0, 1.65f, 8), new Vector3(-5, 1.65f, 13), new Vector3(6, 1.65f, 20), new Vector3(-2, 1.65f, 8.5f), new Vector3(2, 1.65f, 8.5f) };
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 pos = positions[i];
                Cube("Target base", new Vector3(pos.x, 0.15f, pos.z), new Vector3(2, 0.3f, 1.5f), wall, arena);
                Cube("Target post", new Vector3(pos.x, 0.8f, pos.z), new Vector3(0.12f, 1.6f, 0.12f), wall, arena);
                var target = Cube("Target " + (i + 1), pos, i < 3 ? new Vector3(1.25f, 1.6f, 0.35f) : new Vector3(0.7f, 1.2f, 0.35f), targetMat, arena);
                var health = target.AddComponent<DamageTarget>(); health.settings = settings;
                health.label = WorldText("32 / 32", pos + new Vector3(0, 1.2f, -0.25f), 0.04f, Color.white, arena);
                WorldText($"0{i + 1} / {pos.z:0} m", pos + new Vector3(0, -1.35f, -0.85f), 0.025f, Color.white, arena);
            }
            var playerObject = new GameObject("Player"); playerObject.layer = 2;
            playerObject.transform.position = new Vector3(0, 0.1f, 0);
            var controller = playerObject.AddComponent<CharacterController>();
            controller.height = 1.8f; controller.center = new Vector3(0, 0.9f, 0); controller.radius = 0.3f;
            var player = playerObject.AddComponent<PrototypePlayer>(); player.settings = settings;
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera"; cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 1.65f, 0);
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = 0.03f; camera.fieldOfView = settings.fieldOfView;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = RenderSettings.fogColor;
            player.viewCamera = camera;
            var hand = new GameObject("Prototype Hand").transform;
            hand.SetParent(camera.transform, false); hand.localPosition = new Vector3(0.34f, -0.28f, 0.55f); hand.gameObject.layer = 2;
            Cube("Palm", Vector3.zero, new Vector3(0.33f, 0.1f, 0.24f), handMat, hand, false, true);
            Cube("Wrist", new Vector3(0, -0.025f, -0.22f), new Vector3(0.17f, 0.11f, 0.3f), handMat, hand, false, true);
            var weapon = hand.gameObject.AddComponent<HandWeapon>();
            weapon.settings = settings; weapon.player = player; weapon.viewCamera = camera;
            weapon.fingerVisuals = new Transform[4];
            for (int i = 0; i < 4; i++)
                weapon.fingerVisuals[i] = Cube("Finger " + i, new Vector3(-0.12f + i * 0.08f, 0.04f, 0.18f), new Vector3(0.05f, 0.065f, 0.29f), fingerMat, hand, false, true).transform;
            weapon.thumbVisual = Cube("Thumb", new Vector3(-0.22f, -0.01f, 0.025f), new Vector3(0.07f, 0.07f, 0.19f), fingerMat, hand, false, true).transform;
            weapon.muzzle = new GameObject("Muzzle").transform;
            weapon.muzzle.SetParent(hand, false); weapon.muzzle.localPosition = new Vector3(-0.06f, 0.04f, 0.38f);
            player.weapon = weapon;
            var menu = new GameObject("Hand Menu").AddComponent<HandMenu>(); menu.player = player; menu.weapon = weapon;
            CreateSceneHud(menu);
            EnsureDeathScreen();
            EnsureTurret();
            EnsureKamikaze();
            SniperSetup.EnsureSniper();
            HunterSetup.EnsureHunter();
            ConvertTurretToPrefab();
            ConvertKamikazeToPrefab();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets(); Selection.activeObject = settings;
            Debug.Log("Hand Shooter prototype ready: " + ScenePath);
        }


        [MenuItem("Hand Shooter/Add Death Screen")]
        public static void EnsureDeathScreen()
        {
            var player = Object.FindFirstObjectByType<PrototypePlayer>();
            if (!player) return;
            var overlay = player.deathScreen;
            if (!overlay)
            {
                var canvas = Object.FindFirstObjectByType<Canvas>();
                if (!canvas) return;
                overlay = UiImage("Death Screen", canvas.transform, Vector2.zero, Vector2.one,
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.025f, 0.035f, 0.045f, 0.96f));
                UiImage("Death Accent", overlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(0f, 102f), new Vector2(72f, 4f), new Color(1f, 0.25f, 0.23f));
                UiText("Death Eyebrow", overlay.transform, "HAND / LAB     ·     PROTOTYPE", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(500f, 28f), 15,
                    new Color(0.58f, 0.67f, 0.72f), TextAnchor.MiddleCenter);
                UiText("Death Title", overlay.transform, "ELIMINADO", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(0f, 36f), new Vector2(760f, 100f), 64,
                    new Color(0.96f, 0.97f, 0.98f), TextAnchor.MiddleCenter);
                UiText("Death Subtitle", overlay.transform, "Un enemigo terminó la partida.", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(0f, -35f), new Vector2(680f, 36f), 20,
                    new Color(0.67f, 0.73f, 0.77f), TextAnchor.MiddleCenter);
            }
            var subtitle = overlay.transform.Find("Death Subtitle")?.GetComponent<Text>();
            if (subtitle) subtitle.text = "Un enemigo terminó la partida.";
            var hint = overlay.transform.Find("Death Hint")?.GetComponent<Text>();
            if (!hint)
                hint = UiText("Death Hint", overlay.transform, "APRETÁ CUALQUIER TECLA PARA VOLVER A INTENTAR",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -120f), new Vector2(640f, 32f), 14,
                    new Color(0.27f, 0.93f, 0.75f), TextAnchor.MiddleCenter);
            else
                hint.text = "APRETÁ CUALQUIER TECLA PARA VOLVER A INTENTAR";
            overlay.SetActive(false);
            player.deathScreen = overlay;
            player.deathHint = hint;
            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Selection.activeGameObject = overlay;
        }

        [MenuItem("Hand Shooter/Add Turret")]
        public static void EnsureTurret()
        {
            var existing = Object.FindFirstObjectByType<TurretEnemy>();
            if (existing) { ConfigureTurretVisuals(existing); return; }
            const string path = "Assets/HandShooter/Settings/TurretSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<TurretSettings>(path);
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<TurretSettings>();
                AssetDatabase.CreateAsset(settings, path);
            }
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Enemy Turret";
            body.transform.position = new Vector3(0f, 1f, 12f);
            body.transform.localScale = new Vector3(1.2f, 2f, 1.2f);
            body.GetComponent<Renderer>().sharedMaterial = Material("Turret", new Color(0.7f, 0.12f, 0.15f));
            var health = body.AddComponent<DamageTarget>();
            health.turretSettings = settings;
            var turret = body.AddComponent<TurretEnemy>();
            turret.settings = settings;
            turret.muzzle = new GameObject("Muzzle").transform;
            turret.muzzle.SetParent(body.transform, false);
            turret.muzzle.localPosition = new Vector3(0f, 0.3f, -0.65f);
            ConfigureTurretVisuals(turret);
            EditorSceneManager.MarkSceneDirty(body.scene);
        }

        [MenuItem("Hand Shooter/Convert Turret to Prefab")]
        public static void ConvertTurretToPrefab()
        {
            var turret = Object.FindFirstObjectByType<TurretEnemy>();
            if (!turret)
            {
                Debug.LogWarning("No Enemy Turret exists in the active scene.");
                return;
            }
            ConvertTurretToPrefab(turret);
        }

        private static void ConvertTurretToPrefab(TurretEnemy turret)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(turret.gameObject)) return;
            PrefabUtility.SaveAsPrefabAssetAndConnect(turret.gameObject, TurretPrefabPath,
                InteractionMode.AutomatedAction, out bool success);
            if (!success)
            {
                Debug.LogError("Could not save the Enemy Turret prefab at " + TurretPrefabPath, turret);
                return;
            }
            EditorSceneManager.MarkSceneDirty(turret.gameObject.scene);
            Debug.Log("Enemy Turret saved and connected as a prefab: " + TurretPrefabPath, turret);
        }

        private static void ConfigureTurretVisuals(TurretEnemy turret)
        {
            var body = turret.gameObject;
            body.transform.localScale = new Vector3(1.25f, 1.6f, 0.35f);
            var red = Material("Turret", new Color(0.85f, 0.08f, 0.1f));
            red.SetColor("_BaseColor", new Color(0.85f, 0.08f, 0.1f));
            EditorUtility.SetDirty(red);
            body.GetComponent<Renderer>().sharedMaterial = red;
            var support = Material("Wall", new Color(0.12f, 0.19f, 0.22f));
            if (!body.transform.Find("Target base"))
            {
                var pedestal = Cube("Target base", body.transform.position + Vector3.down * 0.85f,
                    new Vector3(2f, 0.3f, 1.5f), support, null);
                pedestal.transform.SetParent(body.transform, true);
                var post = Cube("Target post", body.transform.position + Vector3.down * 0.2f,
                    new Vector3(0.12f, 1.6f, 0.12f), support, null);
                post.transform.SetParent(body.transform, true);
            }
            var health = body.GetComponent<DamageTarget>();
            health.turretSettings = turret.settings;
            if (!health.label)
                health.label = WorldText("", body.transform.position + new Vector3(0f, 1.2f, -0.25f),
                    0.04f, Color.white, null);
            health.label.name = "Turret Health";
            health.label.transform.SetParent(body.transform, true);
            if (turret.settings)
                health.label.text = $"{turret.settings.initialHealth:0.#} / {turret.settings.initialHealth:0.#}";
            EditorUtility.SetDirty(health);
            EditorSceneManager.MarkSceneDirty(body.scene);
        }

        [MenuItem("Hand Shooter/Add Kamikaze")]
        public static void EnsureKamikaze()
        {
            var existing = Object.FindFirstObjectByType<KamikazeEnemy>();
            if (existing) { ConfigureKamikazeVisuals(existing); return; }

            const string settingsPath = "Assets/HandShooter/Settings/KamikazeSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<KamikazeSettings>(settingsPath);
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<KamikazeSettings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
            }
            if (!settings.explosionMaterial)
            {
                settings.explosionMaterial = Material("Beam", Color.white, true, "Universal Render Pipeline/Particles/Unlit");
                EditorUtility.SetDirty(settings);
            }

            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Enemy Kamikaze";
            body.transform.position = new Vector3(5f, 0.75f, 18f);
            body.transform.localScale = Vector3.one * 1.2f;
            body.GetComponent<Renderer>().sharedMaterial = Material("Kamikaze", new Color(0.95f, 0.24f, 0.06f), true);
            var health = body.AddComponent<DamageTarget>();
            health.kamikazeSettings = settings;
            var kamikaze = body.AddComponent<KamikazeEnemy>();
            kamikaze.settings = settings;
            ConfigureKamikazeVisuals(kamikaze);
            EditorSceneManager.MarkSceneDirty(body.scene);
        }

        [MenuItem("Hand Shooter/Convert Kamikaze to Prefab")]
        public static void ConvertKamikazeToPrefab()
        {
            var kamikaze = Object.FindFirstObjectByType<KamikazeEnemy>();
            if (!kamikaze)
            {
                Debug.LogWarning("No Enemy Kamikaze exists in the active scene.");
                return;
            }
            ConvertKamikazeToPrefab(kamikaze);
        }

        [MenuItem("Hand Shooter/Update Kamikaze Prefab")]
        public static void UpdateKamikazePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(KamikazePrefabPath);
            if (!root)
            {
                Debug.LogError("Could not load the Enemy Kamikaze prefab at " + KamikazePrefabPath);
                return;
            }

            var kamikaze = root.GetComponent<KamikazeEnemy>();
            if (!kamikaze)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.LogError("The Enemy Kamikaze prefab has no KamikazeEnemy component.");
                return;
            }

            ConfigureKamikazeVisuals(kamikaze);
            PrefabUtility.SaveAsPrefabAsset(root, KamikazePrefabPath, out bool success);
            PrefabUtility.UnloadPrefabContents(root);
            if (success) AssetDatabase.SaveAssets();
        }

        private static void ConvertKamikazeToPrefab(KamikazeEnemy kamikaze)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(kamikaze.gameObject)) return;
            PrefabUtility.SaveAsPrefabAssetAndConnect(kamikaze.gameObject, KamikazePrefabPath,
                InteractionMode.AutomatedAction, out bool success);
            if (!success)
            {
                Debug.LogError("Could not save the Enemy Kamikaze prefab at " + KamikazePrefabPath, kamikaze);
                return;
            }
            EditorSceneManager.MarkSceneDirty(kamikaze.gameObject.scene);
            Debug.Log("Enemy Kamikaze saved and connected as a prefab: " + KamikazePrefabPath, kamikaze);
        }

        private static void ConfigureKamikazeVisuals(KamikazeEnemy kamikaze)
        {
            var body = kamikaze.gameObject;
            body.transform.localScale = Vector3.one * 1.2f;
            var shell = Material("Kamikaze", new Color(0.95f, 0.24f, 0.06f), true);
            shell.SetColor("_BaseColor", new Color(0.95f, 0.24f, 0.06f));
            EditorUtility.SetDirty(shell);
            body.GetComponent<Renderer>().sharedMaterial = shell;

            Transform directionMarker = body.transform.Find("Direction marker");
            if (!directionMarker)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = "Direction marker";
                marker.transform.SetParent(body.transform, false);
                marker.transform.localPosition = new Vector3(0f, 0.08f, 0.72f);
                marker.transform.localScale = new Vector3(0.35f, 0.3f, 0.75f);
                Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.layer = 2;
                directionMarker = marker.transform;
            }
            directionMarker.GetComponent<Renderer>().sharedMaterial = Material("KamikazeDirection", new Color(0.48f, 0.48f, 0.48f));

            var bandMaterial = Material("KamikazeBand", new Color(0.16f, 0.08f, 0.06f));
            Transform band = body.transform.Find("Warning band");
            if (!band)
            {
                var bandObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bandObject.name = "Warning band";
                bandObject.transform.SetParent(body.transform, false);
                bandObject.transform.localPosition = Vector3.zero;
                bandObject.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                bandObject.transform.localScale = new Vector3(0.62f, 0.22f, 0.62f);
                Object.DestroyImmediate(bandObject.GetComponent<Collider>());
                bandObject.layer = 2;
                band = bandObject.transform;
            }
            band.GetComponent<Renderer>().sharedMaterial = bandMaterial;

            var health = body.GetComponent<DamageTarget>();
            health.kamikazeSettings = kamikaze.settings;
            if (!health.label)
                health.label = WorldText("", body.transform.position + Vector3.up * 0.95f,
                    0.035f, Color.white, null);
            health.label.name = "Kamikaze Health";
            health.label.transform.SetParent(body.transform, true);
            if (kamikaze.settings)
                health.label.text = $"{kamikaze.settings.initialHealth:0.#} / {kamikaze.settings.initialHealth:0.#}";
            EditorUtility.SetDirty(health);
            EditorSceneManager.MarkSceneDirty(body.scene);
        }
        private static void CreateSceneHud(HandMenu menu)
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1366, 768); scaler.matchWidthOrHeight = 0.5f;
            menu.canvas = canvas;
            Color ink = new Color(0.9f, 0.95f, 0.97f), muted = new Color(0.53f, 0.64f, 0.69f);
            Color mint = new Color(0.27f, 0.93f, 0.75f), orange = new Color(1f, 0.59f, 0.31f);
            var track = UiImage("Fuel Track", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(360f, 22f), new Color(0.08f, 0.13f, 0.15f, 0.95f));
            var fill = UiImage("Fuel Fill", track.transform, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero, mint);
            menu.fuelFill = fill.GetComponent<Image>(); menu.fuelFillRect = fill.GetComponent<RectTransform>();
            var dashTrack = UiImage("Dash Cooldown Track", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 38f), new Vector2(280f, 14f), new Color(0.08f, 0.13f, 0.15f, 0.95f));
            var dashFill = UiImage("Dash Cooldown Fill", dashTrack.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, mint);
            menu.dashCooldownFill = dashFill.GetComponent<Image>();
            menu.fuelLabel = UiText("Fuel Label", canvas.transform, "COMBUSTIBLE", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(360f, 28f), 20, mint, TextAnchor.MiddleCenter);
            menu.tankLabel = UiText("Tank Label", canvas.transform, "100/100   ·   TANQUES ×2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(420f, 28f), 16, ink, TextAnchor.MiddleCenter);
            menu.debugPanel = UiImage("Debug Panel", canvas.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(26f, 26f), new Vector2(460f, 116f), new Color(0.03f, 0.07f, 0.09f, 0.94f));
            menu.debugText = UiText("Debug Text", menu.debugPanel.transform, "", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 16, ink, TextAnchor.UpperLeft);
            RectTransform debugRect = menu.debugText.rectTransform; debugRect.offsetMin = new Vector2(16f, 12f); debugRect.offsetMax = new Vector2(-16f, -12f);
            menu.debugPanel.SetActive(false);
            menu.controls = UiText("Controls", canvas.transform, "", Vector2.right, Vector2.right, Vector2.one, new Vector2(-26f, 28f), new Vector2(850f, 26f), 13, muted, TextAnchor.MiddleRight);
            menu.crosshair = UiText("Crosshair", canvas.transform, "+", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(32f, 32f), 25, mint, TextAnchor.MiddleCenter);
            menu.hitText = UiText("Hit Feedback", canvas.transform, "", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), new Vector2(240f, 45f), 19, orange, TextAnchor.MiddleCenter);
            CreateSceneHandMenu(canvas.transform, ink, muted, mint, orange);
        }
        private static void CreateSceneHandMenu(Transform canvas, Color ink, Color muted, Color mint, Color orange)
        {
            var overlay = UiImage("Hand Menu Overlay", canvas, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.015f, 0.03f, 0.045f, 0.86f));
            var panel = UiImage("Hand Menu Panel", overlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 650f), new Color(0.045f, 0.075f, 0.095f, 1f));
            UiImage("Menu Accent", panel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(980f, 3f), mint);
            UiText("Menu Title", panel.transform, "CONFIGURAR MANO", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -28f), new Vector2(700f, 42f), 32, ink, TextAnchor.UpperLeft);
            UiText("Menu Subtitle", panel.transform, "Juntá los dedos para ganar potencia. Liberalos para disparar más rápido.", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -78f), new Vector2(900f, 30f), 17, muted, TextAnchor.UpperLeft);
            UiText("Finger Section Label", panel.transform, "01 / DEDOS PRINCIPALES", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -135f), new Vector2(420f, 30f), 15, mint, TextAnchor.UpperLeft);
            UiText("Result Section Label", panel.transform, "02 / RESULTADO", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(526f, -135f), new Vector2(400f, 30f), 15, mint, TextAnchor.UpperLeft);
            string[] names = { "ÍNDICE", "MEDIO", "ANULAR", "MEÑIQUE" };
            float[] heights = { 158f, 188f, 170f, 128f };
            for (int i = 0; i < names.Length; i++)
                UiButton($"Finger Button {i + 1}", $"Finger Label {i + 1}", panel.transform, new Vector2(72f + i * 92f, -(372f - heights[i])), new Vector2(82f, heights[i]), new Color(0.13f, 0.24f, 0.26f), $"{i + 1:00}\n\n{names[i]}\n\nACTIVO", 12, ink);
            var palm = UiImage("Palm", panel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(66f, -380f), new Vector2(374f, 86f), new Color(0.09f, 0.16f, 0.18f));
            var palmText = UiText("Palm Label", palm.transform, "", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 19, ink, TextAnchor.MiddleCenter);
            palmText.rectTransform.offsetMin = new Vector2(20f, 12f); palmText.rectTransform.offsetMax = new Vector2(-20f, -12f);
            UiButton("Thumb Button", "Thumb Label", panel.transform, new Vector2(66f, -482f), new Vector2(374f, 72f), mint, "PULGAR", 17, new Color(0.025f, 0.07f, 0.08f));
            UiText("Menu Mode", panel.transform, "RAYCAST / INSTANTÁNEO", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(526f, -184f), new Vector2(410f, 40f), 26, mint, TextAnchor.UpperLeft);
            UiText("Menu Stats", panel.transform, "", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(526f, -240f), new Vector2(410f, 150f), 20, ink, TextAnchor.UpperLeft);
            UiText("Rules Header", panel.transform, "REGLAS DE LA MANO", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(526f, -405f), new Vector2(410f, 25f), 13, mint, TextAnchor.UpperLeft);
            UiText("Rules Text", panel.transform, "", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(526f, -441f), new Vector2(415f, 102f), 15, muted, TextAnchor.UpperLeft);
            UiText("Menu Footer", panel.transform, "Los botones atenuados romperían el grupo activo.", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -574f), new Vector2(460f, 42f), 13, muted, TextAnchor.UpperLeft);
            UiButton("Resume Button", "Resume Label", panel.transform, new Vector2(526f, -574f), new Vector2(410f, 44f), new Color(0.13f, 0.24f, 0.26f), "VOLVER A DISPARAR", 16, ink);
            CreatePresetControls(panel.transform, ink, muted, mint);
            overlay.SetActive(false);
        }
        public static void EnsureWeaponPresetMenu()
        {
            var menu = Object.FindFirstObjectByType<HandMenu>();
            if (!menu || !menu.canvas) return;
            Transform panel = menu.canvas.transform.Find("Hand Menu Overlay/Hand Menu Panel");
            if (!panel || panel.Find("Preset Section Label")) return;
            CreatePresetControls(panel, new Color(0.9f, 0.95f, 0.97f),
                new Color(0.53f, 0.64f, 0.69f), new Color(0.27f, 0.93f, 0.75f));
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        }
        private static void CreatePresetControls(Transform panel, Color ink, Color muted, Color mint)
        {
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(980f, 740f);
            UiText("Preset Section Label", panel,
                "03 / GUARDAR CONFIGURACIÓN · Clic o tecla 1–9 / 0 para guardar o reemplazar",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(36f, -568f), new Vector2(900f, 24f), 14, mint, TextAnchor.UpperLeft);
            for (int i = 0; i < HandPresetStore.SlotCount; i++)
            {
                int digit = (i + 1) % HandPresetStore.SlotCount;
                UiButton($"Preset Button {digit}", $"Preset Label {digit}", panel,
                    new Vector2(36f + i * 90f, -602f), new Vector2(84f, 52f),
                    new Color(0.13f, 0.2f, 0.23f), $"[{digit}]\nVACÍO", 11, ink);
            }
            Text footer = panel.Find("Menu Footer").GetComponent<Text>();
            footer.rectTransform.anchoredPosition = new Vector2(36f, -672f);
            footer.rectTransform.sizeDelta = new Vector2(460f, 42f);
            footer.fontSize = 12;
            footer.color = muted;
            footer.horizontalOverflow = HorizontalWrapMode.Wrap;
            footer.text = "Se conservan al reiniciar. En partida: 1–9 / 0 para cambiar.";
            panel.Find("Resume Button").GetComponent<RectTransform>().anchoredPosition = new Vector2(526f, -672f);
        }
        private static void UiButton(string name, string labelName, Transform parent, Vector2 position, Vector2 size, Color background, string label, int fontSize, Color textColor)
        {
            var buttonObject = UiImage(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), position, size, background);
            var button = buttonObject.AddComponent<Button>(); button.targetGraphic = buttonObject.GetComponent<Image>();
            button.targetGraphic.raycastTarget = true;
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            var text = UiText(labelName, buttonObject.transform, label, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, fontSize, textColor, TextAnchor.MiddleCenter);
            text.rectTransform.offsetMin = new Vector2(4f, 4f); text.rectTransform.offsetMax = new Vector2(-4f, -4f);
        }
        private static GameObject UiImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            return go;
        }
        private static Text UiText(string name, Transform parent, string value, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text = value;
            text.fontSize = fontSize; text.color = color; text.alignment = alignment; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
        private static void EnsureFuelPickups()
        {
            if (Object.FindObjectsByType<FuelTankPickup>(FindObjectsSortMode.None).Length > 0) return;
            CreateFuelPickups();
        }
        private static void CreateFuelPickups()
        {
            Material tank = Material("FuelTank", new Color(1f, 0.48f, 0.12f), true);
            Material cap = Material("FuelTankCap", new Color(0.25f, 0.94f, 0.72f), true);
            CreateFuelPickup("Fuel Tank 01", new Vector3(3f, 0.9f, 3.5f), tank, cap);
            CreateFuelPickup("Fuel Tank 02", new Vector3(-6f, 0.9f, 11f), tank, cap);
            CreateFuelPickup("Fuel Tank 03", new Vector3(4f, 0.9f, 19f), tank, cap);
        }
        private static void CreateFuelPickup(string name, Vector3 position, Material tankMaterial, Material capMaterial)
        {
            var pickup = new GameObject(name);
            pickup.transform.position = position;
            var trigger = pickup.AddComponent<SphereCollider>();
            trigger.isTrigger = true; trigger.radius = 0.7f;
            var body = pickup.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
            var fuel = pickup.AddComponent<FuelTankPickup>(); fuel.tanksGranted = 1;
            var visuals = new GameObject("Visuals").transform;
            visuals.SetParent(pickup.transform, false); fuel.visualRoot = visuals;
            var canister = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            canister.name = "Canister"; canister.transform.SetParent(visuals, false);
            canister.transform.localScale = new Vector3(0.38f, 0.38f, 0.38f);
            canister.GetComponent<Renderer>().sharedMaterial = tankMaterial;
            Object.DestroyImmediate(canister.GetComponent<Collider>());
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cap.name = "Fuel stripe"; cap.transform.SetParent(visuals, false);
            cap.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            cap.transform.localScale = new Vector3(0.4f, 0.1f, 0.4f);
            cap.GetComponent<Renderer>().sharedMaterial = capMaterial;
            Object.DestroyImmediate(cap.GetComponent<Collider>());
        }
        private static Material Material(string name, Color color, bool unlit = false, string shaderName = null)
        {
            string path = "Assets/HandShooter/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat) return mat;
            var shader = Shader.Find(shaderName ?? (unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            mat = new Material(shader); mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path); return mat;
        }
        private static GameObject Cube(string name, Vector3 position, Vector3 scale, Material material, Transform parent, bool collider = true, bool local = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent, false);
            if (local) go.transform.localPosition = position; else go.transform.position = position;
            go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) { Object.DestroyImmediate(go.GetComponent<Collider>()); go.layer = 2; }
            return go;
        }
        private static TextMesh WorldText(string text, Vector3 position, float size, Color color, Transform parent)
        {
            var go = new GameObject(text); go.transform.SetParent(parent, false); go.transform.position = position;
            var label = go.AddComponent<TextMesh>(); label.text = text; label.fontSize = 72; label.characterSize = size;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = color;
            return label;
        }
    }
}
