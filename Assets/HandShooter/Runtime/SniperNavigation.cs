using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace HandShooter
{
    // One owned mesh per scene, shared by snipers and hunters and removed on scene unload.
    // Build from colliders so walls match both movement and hitscan cover.
    public sealed class SniperNavigation : MonoBehaviour
    {
        private NavMeshData data;
        private NavMeshDataInstance instance;

        public static void EnsureReady(Scene scene)
        {
            foreach (var existing in FindObjectsByType<SniperNavigation>(FindObjectsSortMode.None))
                if (existing.gameObject.scene == scene) return;

            var go = new GameObject("Sniper Navigation");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<SniperNavigation>().Build(scene);
        }

        private void Build(Scene scene)
        {
            var exclusions = new List<NavMeshBuildMarkup>();
            foreach (var health in FindObjectsByType<DamageTarget>(FindObjectsSortMode.None))
                exclusions.Add(new NavMeshBuildMarkup { root = health.transform, ignoreFromBuild = true });
            foreach (var player in FindObjectsByType<PrototypePlayer>(FindObjectsSortMode.None))
                exclusions.Add(new NavMeshBuildMarkup { root = player.transform, ignoreFromBuild = true });
            foreach (var pickup in FindObjectsByType<FuelTankPickup>(FindObjectsSortMode.None))
                exclusions.Add(new NavMeshBuildMarkup { root = pickup.transform, ignoreFromBuild = true });

            var sources = new List<NavMeshBuildSource>();
            var rootSources = new List<NavMeshBuildSource>();
            var bounds = new Bounds(transform.position, Vector3.one);
            foreach (var root in scene.GetRootGameObjects())
            {
                rootSources.Clear();
                NavMeshBuilder.CollectSources(root.transform, ~0, NavMeshCollectGeometry.PhysicsColliders,
                    0, exclusions, rootSources);
                sources.AddRange(rootSources);
                foreach (var collider in root.GetComponentsInChildren<Collider>())
                    if (collider.enabled && !collider.isTrigger) bounds.Encapsulate(collider.bounds);
            }
            bounds.Expand(4f);
            var build = NavMesh.GetSettingsByID(0);
            build.agentRadius = 0.55f;
            build.agentHeight = 2f;
            build.agentClimb = 0.25f;
            data = NavMeshBuilder.BuildNavMeshData(build, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data) instance = NavMesh.AddNavMeshData(data);
            else Debug.LogError("Could not build sniper navigation from scene colliders.", this);
        }

        private void OnDestroy()
        {
            if (instance.valid) instance.Remove();
            if (data) Destroy(data);
        }
    }
}
