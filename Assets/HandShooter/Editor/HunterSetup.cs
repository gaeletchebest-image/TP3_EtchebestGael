using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace HandShooter.Editor
{
    public static class HunterSetup
    {
        public const string PrefabPath = "Assets/HandShooter/Prefabs/EnemyHunter.prefab";
        public const string SettingsPath = "Assets/HandShooter/Settings/HunterSettings.asset";

        [MenuItem("Hand Shooter/Add Hunter")]
        public static void EnsureHunter()
        {
            if (Object.FindFirstObjectByType<HunterEnemy>()) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab) prefab = CreatePrefab();
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            enemy.name = "Enemy Hunter";
            enemy.transform.position = new Vector3(10f, 1f, 12f);
            enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Undo.RegisterCreatedObjectUndo(enemy, "Add Hunter");
            EditorSceneManager.MarkSceneDirty(enemy.scene);
        }

        private static GameObject CreatePrefab()
        {
            var settings = AssetDatabase.LoadAssetAtPath<HunterSettings>(SettingsPath);
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<HunterSettings>();
                // Match the existing sniper's flash without its charge sequence.
                var sniper = AssetDatabase.LoadAssetAtPath<SniperSettings>(SniperSetup.SettingsPath);
                if (sniper)
                {
                    settings.beamMaterial = sniper.beamMaterial;
                    settings.beamColor = sniper.lockedColor;
                    settings.beamWidth = sniper.beamWidth;
                    settings.beamDuration = sniper.beamDuration;
                }
                if (!settings.beamMaterial)
                    settings.beamMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/HandShooter/Materials/Beam.mat");
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            var shell = MakeMaterial("Hunter", new Color(0.12f, 0.4f, 0.37f), false);
            var dark = MakeMaterial("HunterArmor", new Color(0.06f, 0.1f, 0.12f), false);
            var glow = MakeMaterial("HunterEye", settings.normalColor, true);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.SetActive(false);
            body.name = "EnemyHunter";
            body.GetComponent<Renderer>().sharedMaterial = shell;
            var health = body.AddComponent<DamageTarget>();
            health.hunterSettings = settings;
            var agent = body.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            agent.radius = 0.5f;
            agent.height = 2f;
            agent.baseOffset = 1f;
            agent.speed = settings.chaseSpeed;
            agent.acceleration = settings.acceleration;
            var hunter = body.AddComponent<HunterEnemy>();
            hunter.settings = settings;
            Part("Shoulder armor", PrimitiveType.Cube, body.transform, new Vector3(0f, 0.25f, 0f),
                new Vector3(1.15f, 0.4f, 0.65f), dark);
            Part("Eye housing", PrimitiveType.Cube, body.transform, new Vector3(0f, 0.55f, 0.35f),
                new Vector3(0.8f, 0.32f, 0.4f), dark);
            hunter.eye = Part("Alert eye", PrimitiveType.Sphere, body.transform, new Vector3(0f, 0.55f, 0.58f),
                new Vector3(0.5f, 0.17f, 0.12f), glow).GetComponent<Renderer>();
            Part("Short emitter", PrimitiveType.Cube, body.transform, new Vector3(0f, 0.35f, 0.55f),
                new Vector3(0.22f, 0.18f, 0.45f), dark);
            hunter.muzzle = new GameObject("Muzzle").transform;
            hunter.muzzle.SetParent(body.transform, false);
            hunter.muzzle.localPosition = new Vector3(0f, 0.35f, 0.8f);
            health.label = Label("Hunter Health", body.transform, new Vector3(0f, 1.55f, 0f), 0.03f);
            hunter.statusLabel = Label("Hunter State", body.transform, new Vector3(0f, 1.85f, 0f), 0.025f);
            hunter.statusLabel.color = settings.normalColor;
            body.SetActive(true);
            var prefab = PrefabUtility.SaveAsPrefabAsset(body, PrefabPath);
            Object.DestroyImmediate(body);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        private static Transform Part(string name, PrimitiveType primitive, Transform parent, Vector3 position,
            Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.transform;
        }

        private static TextMesh Label(string name, Transform parent, Vector3 position, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var label = go.AddComponent<TextMesh>();
            label.text = "";
            label.fontSize = 72;
            label.characterSize = size;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;
            return label;
        }

        private static Material MakeMaterial(string name, Color color, bool unlit)
        {
            string path = "Assets/HandShooter/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
