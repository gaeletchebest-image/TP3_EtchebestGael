using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HandShooter.Editor
{
    public static class PrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/HandShooterPrototype.unity";
        public const string SettingsPath = "Assets/HandShooter/Settings/ShooterSettings.asset";
        [MenuItem("Hand Shooter/Open or Create Prototype")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode before creating the prototype."); return; }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
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
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets(); Selection.activeObject = settings;
            Debug.Log("Hand Shooter prototype ready: " + ScenePath);
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

