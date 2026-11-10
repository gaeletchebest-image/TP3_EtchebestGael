using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.SceneManagement;

namespace HandShooter.Editor
{
    public static class RefuelingStationSetup
    {
        public const string SettingsPath = "Assets/HandShooter/Settings/RefuelingStationSettings.asset";
        public const string Folder = "Assets/HandShooter/Prefabs/RefuelingStation";
        public const string PrefabPath = Folder + "/RefuelingStation.prefab";
        private const string MeshPath = Folder + "/RefuelingStationMeshes.asset";
        private const string Materials = "Assets/HandShooter/Prefabs/CargoLift/Materials/";
        private const float FootprintScale = 0.5f;

        private static Vector3 Compact(Vector3 value) => new Vector3(value.x * FootprintScale, value.y, value.z * FootprintScale);

        public static RefuelingStationSettings EnsureSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<RefuelingStationSettings>(SettingsPath);
            if (settings) return settings;
            BalanceLabAssets.EnsureFolder("Assets/HandShooter/Settings");
            settings = ScriptableObject.CreateInstance<RefuelingStationSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        public static GameObject EnsurePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab) RebuildPrefab();
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        [MenuItem("Hand Shooter/Estación de llenado/Reconstruir modelo")]
        public static void RebuildPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            BalanceLabAssets.EnsureFolder(Folder);
            var meshes = AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>().ToDictionary(m => m.name);
            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Refueling Station");
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                var station = root.AddComponent<RefuelingStation>();
                station.settings = EnsureSettings();
                Material ceramic = Mat("Ceramic Enamel"), graphite = Mat("Graphite"), blue = Mat("Petrol Blue");
                Material alloy = Mat("Machined Alloy"), slate = Mat("Deck Slate"), tread = Mat("Tread");
                Material cyan = Mat("Signal Cyan"), amber = Mat("Safety Amber");
                var textMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/WorldText.mat");
                var textShader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/WorldText.shader");
                if (!textMaterial)
                {
                    textMaterial = new Material(textShader) { name = "World Text" };
                    AssetDatabase.CreateAsset(textMaterial, Folder + "/WorldText.mat");
                }
                else if (textMaterial.shader != textShader)
                {
                    textMaterial.shader = textShader;
                    EditorUtility.SetDirty(textMaterial);
                }

                GameObject Part(string name, Vector3 position, Vector3 size, Material material, float bevel = 0.025f)
                {
                    position = Compact(position);
                    size = Compact(size);
                    // Bake beveled ProBuilder geometry into persistent mesh assets.
                    var pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
                    pb.name = name;
                    pb.transform.SetParent(root.transform, false);
                    pb.transform.localPosition = position;
                    if (bevel > 0f)
                    {
                        var edges = pb.faces.SelectMany(f => f.edges).ToArray();
                        Bevel.BevelEdges(pb, edges, Mathf.Min(bevel, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.24f));
                        pb.ToMesh();
                        pb.Refresh();
                    }
                    Mesh generated = Object.Instantiate(pb.GetComponent<MeshFilter>().sharedMesh);
                    generated.name = name;
                    Object.DestroyImmediate(pb);
                    if (meshes.TryGetValue(name, out var saved))
                    {
                        EditorUtility.CopySerialized(generated, saved);
                        // CopySerialized updates serialized data but can leave the old GPU buffers
                        // visible until Unity restarts. Refresh the render buffers explicitly.
                        saved.vertices = generated.vertices;
                        saved.normals = generated.normals;
                        saved.tangents = generated.tangents;
                        saved.uv = generated.uv;
                        for (int submesh = 0; submesh < generated.subMeshCount; submesh++)
                            saved.SetIndices(generated.GetIndices(submesh), generated.GetTopology(submesh), submesh);
                        Object.DestroyImmediate(generated);
                        EditorUtility.SetDirty(saved);
                    }
                    else
                    {
                        saved = generated;
                        if (!AssetDatabase.LoadMainAssetAtPath(MeshPath)) AssetDatabase.CreateAsset(saved, MeshPath);
                        else AssetDatabase.AddObjectToAsset(saved, MeshPath);
                        meshes.Add(name, saved);
                    }
                    var go = root.transform.Find(name).gameObject;
                    go.GetComponent<MeshFilter>().sharedMesh = saved;
                    go.GetComponent<MeshRenderer>().sharedMaterial = material;
                    foreach (var collider in go.GetComponents<Collider>()) Object.DestroyImmediate(collider);
                    return go;
                }

                BoxCollider Solid(string name, Vector3 position, Vector3 size, Material material, float bevel = 0.025f)
                {
                    var go = Part(name, position, size, material, bevel);
                    var collider = go.AddComponent<BoxCollider>();
                    collider.size = Compact(size);
                    return collider;
                }

                station.deck = Solid("Low boarding deck", new Vector3(0f, 0.09f, 0f), new Vector3(3.6f, 0.18f, 3f), graphite, 0.06f);
                Part("Inset tread surface", new Vector3(0f, 0.185f, -0.08f), new Vector3(3.22f, 0.012f, 2.48f), tread, 0f);
                Solid("Service wall", new Vector3(0f, 1.56f, 1.35f), new Vector3(3.6f, 2.76f, 0.28f), blue, 0.08f);
                Part("Rear ceramic fascia", new Vector3(0f, 1.65f, 1.185f), new Vector3(2.86f, 2.16f, 0.09f), ceramic, 0.045f);
                Part("Service recess", new Vector3(0f, 1.55f, 1.12f), new Vector3(2.25f, 1.64f, 0.08f), graphite, 0.04f);
                Solid("Left frame", new Vector3(-1.61f, 1.61f, 1.14f), new Vector3(0.25f, 2.86f, 0.54f), alloy, 0.045f);
                Solid("Right frame", new Vector3(1.61f, 1.61f, 1.14f), new Vector3(0.25f, 2.86f, 0.54f), alloy, 0.045f);
                Solid("Canopy shell", new Vector3(0f, 3.04f, 0f), new Vector3(3.78f, 0.26f, 3.18f), ceramic, 0.06f);
                Part("Canopy underside", new Vector3(0f, 2.895f, -0.08f), new Vector3(3.34f, 0.08f, 2.72f), slate);
                Part("Canopy petrol brow", new Vector3(0f, 3.01f, -1.595f), new Vector3(3.28f, 0.15f, 0.045f), blue, 0.01f);
                Part("Canopy cyan light", new Vector3(0f, 2.88f, -1.28f), new Vector3(2.72f, 0.025f, 0.08f), cyan, 0f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Deck guide " + side, new Vector3(side * 1.58f, 0.198f, -0.1f), new Vector3(0.04f, 0.012f, 2.24f), cyan, 0f);
                    Part("Frame beacon " + side, new Vector3(side * 1.61f, 1.65f, 0.855f), new Vector3(0.07f, 1.8f, 0.02f), cyan, 0f);
                    for (int i = 0; i < 5; i++)
                    {
                        var stripe = Part("Boarding caution " + side + " " + i,
                            new Vector3(side * (1.48f - i * 0.11f), 0.198f, -1.34f), new Vector3(0.06f, 0.012f, 0.19f), amber, 0f);
                        stripe.transform.localRotation = Quaternion.Euler(0f, -30f, 0f);
                    }
                }
                Part("Pump housing", new Vector3(-0.6f, 1.4f, 0.99f), new Vector3(0.66f, 1.12f, 0.2f), blue, 0.055f);
                Part("Pump coupling", new Vector3(-0.6f, 1.46f, 0.85f), new Vector3(0.4f, 0.4f, 0.14f), alloy, 0.065f);
                Part("Fuel coupling core", new Vector3(-0.6f, 1.46f, 0.765f), new Vector3(0.22f, 0.22f, 0.04f), cyan, 0.04f);
                // A rigid service conduit joins the coupling to the base, clear of the boarding space.
                Part("Fuel conduit vertical", new Vector3(-0.6f, 0.59f, 1.015f), new Vector3(0.09f, 0.82f, 0.1f), alloy);
                Part("Control display", new Vector3(0.49f, 1.88f, 1.04f), new Vector3(1.12f, 0.53f, 0.08f), blue);
                var lights = new List<Renderer>();
                for (int i = 0; i < 8; i++)
                    lights.Add(Part("Charge segment " + i, new Vector3(0.04f + i * 0.13f, 1.42f, 1.035f),
                        new Vector3(0.09f, 0.14f, 0.04f), blue, 0.008f).GetComponent<Renderer>());
                station.progressLights = lights.ToArray();
                station.idleLight = blue; station.chargedLight = cyan;

                TextMesh Label(string name, string text, Vector3 position, float size, Color color)
                {
                    var label = new GameObject(name, typeof(TextMesh)).GetComponent<TextMesh>();
                    label.transform.SetParent(root.transform, false);
                    label.transform.localPosition = Compact(position);
                    label.anchor = TextAnchor.MiddleCenter;
                    label.alignment = TextAlignment.Center;
                    label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    label.fontSize = 64; label.characterSize = size * FootprintScale;
                    label.color = color; label.text = text;
                    label.GetComponent<Renderer>().sharedMaterial = textMaterial;
                    label.gameObject.AddComponent<WorldTextTexture>();
                    return label;
                }
                Label("Station identification", "FUEL / 01", new Vector3(0f, 3.015f, -1.625f), 0.032f, Color.white);
                station.statusLabel = Label("Station status", "REPOSICIÓN\nPERMANECÉ EN LA BASE", new Vector3(0.49f, 1.88f, 0.99f), 0.013f, new Color(0.35f, 1f, 0.84f));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat");
    }
}
