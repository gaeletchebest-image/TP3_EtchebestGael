using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HandShooter.Editor
{
    [InitializeOnLoad]
    public static class BalanceLabLifecycle
    {
        static BalanceLabLifecycle()
        {
            EditorApplication.playModeStateChanged += OnPlayState;
            BalanceLabSession.ExitRequested += ExitPlay;
        }

        private static void ExitPlay() { EditorApplication.isPlaying = false; }

        private static void OnPlayState(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("BalanceLab.ReturnToPanel", false)) return;
            SessionState.SetBool("BalanceLab.ReturnToPanel", false);
            bool restore = SessionState.GetBool("BalanceLab.RestoreScene", false);
            SessionState.SetBool("BalanceLab.RestoreScene", false);
            string previous = SessionState.GetString("BalanceLab.PreviousScene", "");
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (restore && !string.IsNullOrEmpty(previous) && System.IO.File.Exists(previous))
                    EditorSceneManager.OpenScene(previous);
                BalanceLabWindow.OpenResults();
            };
        }
    }

    public static class BalanceLabAssets
    {
        public const string Folder = "Assets/HandShooter/Balance";
        public const string DraftPath = Folder + "/Borrador.asset";
        public const string ScenePath = "Assets/Scenes/BalanceLab.unity";
        public const string ProfilesFolder = Folder + "/Profiles";

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        public static BalanceProfile Draft()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BalanceProfile>(DraftPath);
            if (existing) return existing;
            EnsureFolder(ProfilesFolder);
            var source = ScriptableObject.CreateInstance<BalanceProfile>();
            source.title = "Base actual";
            source.shooter = AssetDatabase.LoadAssetAtPath<ShooterSettings>(PrototypeSceneBuilder.SettingsPath);
            source.turret = AssetDatabase.LoadAssetAtPath<TurretSettings>("Assets/HandShooter/Settings/TurretSettings.asset");
            source.kamikaze = AssetDatabase.LoadAssetAtPath<KamikazeSettings>("Assets/HandShooter/Settings/KamikazeSettings.asset");
            source.sniper = AssetDatabase.LoadAssetAtPath<SniperSettings>(SniperSetup.SettingsPath);
            source.hunter = AssetDatabase.LoadAssetAtPath<HunterSettings>(HunterSetup.SettingsPath);
            var scene = SceneManager.GetSceneByPath(PrototypeSceneBuilder.ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath, OpenSceneMode.Additive);
            source.weapon = WeaponBalance.Capture(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HandWeapon>(true)).First());
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (!source.IsComplete) { UnityEngine.Object.DestroyImmediate(source); throw new InvalidOperationException("Faltan ajustes del prototipo."); }
            var draft = CreateCopy(source, DraftPath);
            CreateCopy(source, ProfilesFolder + "/Base inicial.asset");
            UnityEngine.Object.DestroyImmediate(source);
            return draft;
        }

        public static BalanceProfile CreateCopy(BalanceProfile source, string path)
        {
            var result = ScriptableObject.CreateInstance<BalanceProfile>();
            result.title = source.title; result.notes = source.notes;
            result.weapon = JsonUtility.FromJson<WeaponBalance>(JsonUtility.ToJson(source.weapon));
            AssetDatabase.CreateAsset(result, path);
            result.shooter = AddCopy(source.shooter, result);
            result.turret = AddCopy(source.turret, result);
            result.kamikaze = AddCopy(source.kamikaze, result);
            result.sniper = AddCopy(source.sniper, result);
            result.hunter = AddCopy(source.hunter, result);
            EditorUtility.SetDirty(result);
            AssetDatabase.SaveAssets();
            return result;
        }

        private static T AddCopy<T>(T source, BalanceProfile owner) where T : ScriptableObject
        {
            var copy = UnityEngine.Object.Instantiate(source);
            copy.name = source.name;
            AssetDatabase.AddObjectToAsset(copy, owner);
            return copy;
        }

        private static void CopySettings(UnityEngine.Object from, UnityEngine.Object to)
        {
            Undo.RecordObject(to, "Cambiar balance");
            string name = to.name;
            EditorUtility.CopySerialized(from, to);
            to.name = name;
            EditorUtility.SetDirty(to);
        }

        public static void LoadInto(BalanceProfile source, BalanceProfile destination)
        {
            if (source == destination || !source.IsComplete || !destination.IsComplete) return;
            CopySettings(source.shooter, destination.shooter);
            CopySettings(source.turret, destination.turret);
            CopySettings(source.kamikaze, destination.kamikaze);
            CopySettings(source.sniper, destination.sniper);
            CopySettings(source.hunter, destination.hunter);
            Undo.RecordObject(destination, "Cargar perfil");
            destination.title = source.title; destination.notes = source.notes;
            destination.weapon = JsonUtility.FromJson<WeaponBalance>(JsonUtility.ToJson(source.weapon));
            EditorUtility.SetDirty(destination);
            AssetDatabase.SaveAssets();
        }

        public static void ApplyToGame(BalanceProfile draft)
        {
            CopySettings(draft.shooter, AssetDatabase.LoadAssetAtPath<ShooterSettings>(PrototypeSceneBuilder.SettingsPath));
            CopySettings(draft.turret, AssetDatabase.LoadAssetAtPath<TurretSettings>("Assets/HandShooter/Settings/TurretSettings.asset"));
            CopySettings(draft.kamikaze, AssetDatabase.LoadAssetAtPath<KamikazeSettings>("Assets/HandShooter/Settings/KamikazeSettings.asset"));
            CopySettings(draft.sniper, AssetDatabase.LoadAssetAtPath<SniperSettings>(SniperSetup.SettingsPath));
            CopySettings(draft.hunter, AssetDatabase.LoadAssetAtPath<HunterSettings>(HunterSetup.SettingsPath));
            var scene = SceneManager.GetSceneByPath(PrototypeSceneBuilder.ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath, OpenSceneMode.Additive);
            foreach (var weapon in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HandWeapon>(true)))
            { Undo.RecordObject(weapon, "Aplicar combustible"); draft.weapon.Apply(weapon); }
            EditorSceneManager.MarkSceneDirty(scene);
            // Preserve unrelated unsaved scene edits when the prototype is already open.
            if (opened) { EditorSceneManager.SaveScene(scene); EditorSceneManager.CloseScene(scene, true); }
            AssetDatabase.SaveAssets();
        }

        public static void EnsureScene(BalanceProfile draft)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)) return;
            if (!AssetDatabase.CopyAsset(PrototypeSceneBuilder.ScenePath, ScenePath))
                throw new InvalidOperationException("No se pudo copiar la escena del prototipo.");
            var previous = SceneManager.GetActiveScene();
            var lab = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(lab);
            foreach (var root in lab.GetRootGameObjects())
            {
                if (root.GetComponent<PrototypePlayer>() || root.GetComponent<HandMenu>() ||
                    root.GetComponent<Canvas>() || root.GetComponent<Light>() || root.GetComponent<UnityEngine.EventSystems.EventSystem>()) continue;
                UnityEngine.Object.DestroyImmediate(root);
            }
            var player = lab.GetRootGameObjects().Select(g => g.GetComponent<PrototypePlayer>()).First(p => p);
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.1f, 0f), Quaternion.identity);
            player.spawnDefaultFuelPickups = false;
            var session = new GameObject("Laboratorio de balance").AddComponent<BalanceLabSession>();
            session.profile = draft; session.player = player;
            session.turretPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeSceneBuilder.TurretPrefabPath);
            session.kamikazePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeSceneBuilder.KamikazePrefabPath);
            session.sniperPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SniperSetup.PrefabPath);
            session.hunterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HunterSetup.PrefabPath);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Suelo del laboratorio";
            floor.transform.position = new Vector3(0f, -0.25f, 20f);
            floor.transform.localScale = new Vector3(64f, 0.5f, 100f);
            floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/HandShooter/Materials/Floor.mat");
            RenderSettings.fog = false;
            EditorSceneManager.SaveScene(lab);
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(lab, true);
            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
        }
    }
}
