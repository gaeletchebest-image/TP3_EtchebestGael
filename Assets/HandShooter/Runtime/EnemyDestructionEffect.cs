using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HandShooter
{
    internal sealed class EnemyDestructionEffect : MonoBehaviour
    {
        private const float SilhouetteDuration = 0.075f;
        private const int MinParticlesPerMesh = 10;
        private const int MaxParticlesPerMesh = 240;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColor = Shader.PropertyToID("_Color");
        private static readonly System.Random Random = new System.Random();
        private static Material particleMaterial;

        private readonly struct SurfaceTriangle
        {
            public readonly Vector3 a, b, c;
            public readonly Color color;
            public readonly float cumulativeArea;

            public SurfaceTriangle(Vector3 a, Vector3 b, Vector3 c, Color color, float cumulativeArea)
            {
                this.a = a;
                this.b = b;
                this.c = c;
                this.color = color;
                this.cumulativeArea = cumulativeArea;
            }
        }

        public static void Spawn(Renderer body, Color bodyColor)
        {
            if (!particleMaterial) particleMaterial = Resources.Load<Material>("EnemyDestructionParticles");
            if (!particleMaterial) return;

            var parts = body.GetComponentsInChildren<MeshFilter>();
            var particles = new List<ParticleSystem.EmitParams>();
            var triangles = new List<SurfaceTriangle>();
            var materials = new List<Material>();
            var properties = new MaterialPropertyBlock();
            Vector3 center = body.bounds.center;
            foreach (var part in parts)
            {
                var mesh = part.sharedMesh;
                if (!mesh || !mesh.isReadable || !part.TryGetComponent<MeshRenderer>(out var renderer) ||
                    !renderer.enabled || renderer.forceRenderingOff) continue;

                triangles.Clear();
                renderer.GetSharedMaterials(materials);
                renderer.GetPropertyBlock(properties);
                var vertices = mesh.vertices;
                Matrix4x4 localToWorld = part.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = localToWorld.MultiplyPoint3x4(vertices[i]);

                float area = 0f;
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    if (mesh.GetTopology(submesh) != MeshTopology.Triangles) continue;
                    var material = submesh < materials.Count ? materials[submesh] : renderer.sharedMaterial;
                    // The body can be flashing white from a previous hit. Keep its normal tint.
                    Color color = renderer == body && submesh == 0 ? bodyColor : ReadTint(material, properties);
                    var indices = mesh.GetTriangles(submesh);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                        float triangleArea = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                        if (triangleArea <= 0f) continue;
                        area += triangleArea;
                        triangles.Add(new SurfaceTriangle(a, b, c, color, area));
                    }
                }

                if (triangles.Count > 0) SampleSurface(triangles, area, center, particles);
            }

            if (particles.Count == 0) return;

            var root = new GameObject("Enemy destruction");
            root.SetActive(false);
            root.layer = 2;
            root.transform.position = center;
            SceneManager.MoveGameObjectToScene(root, body.gameObject.scene);
            var system = root.AddComponent<ParticleSystem>();
            Configure(system, particles.Count);
            var effect = root.AddComponent<EnemyDestructionEffect>();
            root.SetActive(true);
            system.Play(false);
            foreach (var particle in particles) system.Emit(particle, 1);
            system.Pause(false);
            effect.StartCoroutine(effect.Scatter(system));
        }

        private static Color ReadTint(Material material, MaterialPropertyBlock properties)
        {
            if (!material) return Color.white;
            int colorProperty = material.HasColor(BaseColor) ? BaseColor : LegacyColor;
            if (properties.HasColor(colorProperty)) return properties.GetColor(colorProperty);
            return material.HasColor(colorProperty) ? material.GetColor(colorProperty) : Color.white;
        }

        private static void SampleSurface(List<SurfaceTriangle> triangles, float area, Vector3 center,
            List<ParticleSystem.EmitParams> particles)
        {
            int count = Mathf.Clamp(Mathf.CeilToInt(area * 36f), MinParticlesPerMesh, MaxParticlesPerMesh);
            float size = Mathf.Clamp(Mathf.Sqrt(area / count) * 0.8f, 0.025f, 0.15f);
            int index = 0;
            for (int i = 0; i < count; i++)
            {
                // Spread samples across equal portions of the cumulative surface area.
                float sample = area * (i + Next()) / count;
                while (triangles[index].cumulativeArea < sample && index + 1 < triangles.Count) index++;
                var triangle = triangles[index];
                float u = Mathf.Sqrt(Next()), v = Next();
                Vector3 position = (1f - u) * triangle.a + u * ((1f - v) * triangle.b + v * triangle.c);
                Vector3 outward = (position - center).normalized;
                Vector3 drift = new Vector3(Next() - 0.5f, Next() - 0.3f, Next() - 0.5f);
                Color tint = Color.Lerp(triangle.color, Color.white, Next() * 0.20f);
                particles.Add(new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = outward * Mathf.Lerp(1.1f, 3.6f, Next()) + drift * 1.4f + Vector3.up * 0.8f,
                    startColor = tint,
                    startSize = size * Mathf.Lerp(0.5f, 1.15f, Next()),
                    startLifetime = Mathf.Lerp(0.48f, 0.85f, Next()),
                    rotation = Next() * 360f
                });
            }
        }

        private static float Next() => (float)Random.NextDouble();

        private static void Configure(ParticleSystem system, int count)
        {
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.9f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.gravityModifier = 0.65f;
            main.maxParticles = count;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var tint = system.colorOverLifetime;
            tint.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            tint.color = fade;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
        }

        private IEnumerator Scatter(ParticleSystem system)
        {
            yield return new WaitForSeconds(SilhouetteDuration);
            system.Play(false);
        }
    }
}
