using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace HandShooter.Editor
{
    public static class SniperSetup
    {
        public const string PrefabPath = "Assets/HandShooter/Prefabs/EnemySniper.prefab";
        public const string SettingsPath = "Assets/HandShooter/Settings/SniperSettings.asset";

        [MenuItem("Hand Shooter/Add Sniper")]
        public static void EnsureSniper()
        {
            if (Object.FindFirstObjectByType<SniperEnemy>()) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab) prefab = CreatePrefab();
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            enemy.name = "Enemy Sniper";
            enemy.transform.position = new Vector3(-9f, 1f, 20f);
            enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            EditorSceneManager.MarkSceneDirty(enemy.scene);
        }

        private static GameObject CreatePrefab()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SniperSettings>(SettingsPath);
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<SniperSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            settings.beamMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/HandShooter/Materials/Beam.mat");
            EditorUtility.SetDirty(settings);
            var shell = MakeMaterial("Sniper", new Color(0.27f, 0.14f, 0.45f), false);
            var dark = MakeMaterial("SniperArmor", new Color(0.075f, 0.09f, 0.13f), false);
            var glow = MakeMaterial("SniperGlow", settings.warningColor, true);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "EnemySniper";
            body.GetComponent<Renderer>().sharedMaterial = shell;
            var health = body.AddComponent<DamageTarget>();
            health.sniperSettings = settings;
            var agent = body.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            agent.radius = 0.5f;
            agent.height = 2f;
            agent.baseOffset = 1f;
            var sniper = body.AddComponent<SniperEnemy>();
            sniper.settings = settings;
            Part("Shoulder armor", PrimitiveType.Cube, body.transform, new Vector3(0f, 0.35f, 0f),
                new Vector3(1.2f, 0.35f, 0.65f), dark);
            Part("Long barrel", PrimitiveType.Cube, body.transform, new Vector3(0f, 0.35f, 0.7f),
                new Vector3(0.18f, 0.18f, 1.1f), dark);
            Part("Scope", PrimitiveType.Cube, body.transform, new Vector3(0f, 0.62f, 0.35f),
                new Vector3(0.27f, 0.22f, 0.5f), glow);
            sniper.muzzle = new GameObject("Muzzle").transform;
            sniper.muzzle.SetParent(body.transform, false);
            sniper.muzzle.localPosition = new Vector3(0f, 0.35f, 1.28f);
            sniper.chargeOrb = Part("Charge orb", PrimitiveType.Sphere, sniper.muzzle, Vector3.zero,
                Vector3.one * 0.12f, glow);
            sniper.chargeOrb.gameObject.SetActive(false);
            health.label = Label("Sniper Health", body.transform, new Vector3(0f, 1.95f, 0f), 0.03f);
            sniper.statusLabel = Label("Sniper State", body.transform, new Vector3(0f, 2.2f, 0f), 0.025f);
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
