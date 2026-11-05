using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HandShooter
{
    internal sealed class GameSaveSession
    {
        private static SavedGame pendingLoad;
        private static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");

        private readonly PrototypePlayer player;
        private readonly LevelLiftTransition countdown;
        private readonly Dictionary<string, DamageTarget> enemies = new Dictionary<string, DamageTarget>();
        private readonly Dictionary<string, SlidingDoor> doors = new Dictionary<string, SlidingDoor>();
        private readonly Dictionary<string, CargoLift> lifts = new Dictionary<string, CargoLift>();
        private readonly Dictionary<string, ArenaEncounter> arenas = new Dictionary<string, ArenaEncounter>();
        private readonly Dictionary<string, FuelTankPickup> pickups = new Dictionary<string, FuelTankPickup>();

        internal GameSaveSession(PrototypePlayer player)
        {
            this.player = player;
            var roots = player.gameObject.scene.GetRootGameObjects();
            countdown = roots.Select(root => root.GetComponentInChildren<LevelLiftTransition>())
                .FirstOrDefault(transition => transition && transition.SecondsRemaining > 0f);
            Collect(roots, enemies, target => target.IsEnemy);
            Collect(roots, doors);
            Collect(roots, lifts);
            Collect(roots, arenas);
            Collect(roots, pickups);
        }

        internal static bool CanSaveScene(Scene scene) =>
            scene.buildIndex >= 0 && scene.name != "BalanceLab";

        internal static bool IsPendingFor(Scene scene) => pendingLoad != null && pendingLoad.scene == scene.path;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPendingLoad() => pendingLoad = null;

        internal SavedGame Capture()
        {
            var snapshot = new SavedGame
            {
                version = SavedGame.CurrentVersion,
                savedAtUtc = DateTime.UtcNow.Ticks,
                scene = player.gameObject.scene.path,
                levelSecondsRemaining = countdown ? countdown.SecondsRemaining : 0f,
                player = player.CaptureState(),
                enemies = new List<SavedGame.Enemy>(),
                doors = new List<SavedGame.Door>(),
                lifts = new List<SavedGame.Lift>(),
                arenas = new List<SavedGame.Arena>(),
                consumedPickups = new List<string>()
            };
            foreach (var pair in enemies)
            {
                var state = pair.Value ? pair.Value.CaptureState(pair.Key) : new SavedGame.Enemy { id = pair.Key, defeated = true };
                if (state != null) snapshot.enemies.Add(state);
            }
            foreach (var pair in doors)
                if (pair.Value) snapshot.doors.Add(pair.Value.CaptureState(pair.Key));
            foreach (var pair in lifts)
                if (pair.Value) snapshot.lifts.Add(pair.Value.CaptureState(pair.Key));
            foreach (var pair in arenas)
                if (pair.Value) snapshot.arenas.Add(new SavedGame.Arena { id = pair.Key, state = pair.Value.State });
            foreach (var pair in pickups)
                if (!pair.Value || pair.Value.IsCollected) snapshot.consumedPickups.Add(pair.Key);
            return snapshot;
        }

        internal bool ApplyPendingLoad()
        {
            if (!IsPendingFor(player.gameObject.scene)) return false;
            var snapshot = pendingLoad;
            pendingLoad = null;
            Restore(snapshot);
            return true;
        }

        internal void Restore(SavedGame snapshot)
        {
            foreach (var state in snapshot.arenas)
                if (arenas.TryGetValue(state.id, out var arena) && arena) arena.State = state.state;
            foreach (var state in snapshot.doors)
                if (doors.TryGetValue(state.id, out var door) && door) door.RestoreState(state);
            foreach (var pair in lifts)
                if (pair.Value) pair.Value.RestoreState(snapshot.lifts?.Find(state => state.id == pair.Key));
            if (countdown) countdown.RestoreCountdown(snapshot.levelSecondsRemaining);
            foreach (var id in snapshot.consumedPickups)
                if (pickups.TryGetValue(id, out var pickup) && pickup) Remove(pickup.gameObject);

            player.RestoreState(snapshot.player);
            Physics.SyncTransforms();
            foreach (var state in snapshot.enemies)
            {
                if (!enemies.TryGetValue(state.id, out var enemy) || !enemy) continue;
                if (state.defeated) Remove(enemy.gameObject);
                else enemy.RestoreState(state);
            }
            Physics.SyncTransforms();
        }

        internal static void Write(SavedGame snapshot)
        {
            Directory.CreateDirectory(SaveDirectory);
            string path = Path.Combine(SaveDirectory, Guid.NewGuid().ToString("N") + ".json");
            string temporary = path + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(snapshot));
                File.Move(temporary, path);
            }
            finally
            {
                File.Delete(temporary);
            }
        }

        internal static List<SavedGame> ReadAll(out int skipped)
        {
            var saves = new List<SavedGame>();
            skipped = 0;
            if (!Directory.Exists(SaveDirectory)) return saves;
            foreach (string path in Directory.EnumerateFiles(SaveDirectory, "*.json"))
            {
                try
                {
                    var snapshot = JsonUtility.FromJson<SavedGame>(File.ReadAllText(path));
                    if (IsValid(snapshot)) saves.Add(snapshot);
                    else skipped++;
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
                {
                    skipped++;
                }
            }
            saves.Sort((a, b) => b.savedAtUtc.CompareTo(a.savedAtUtc));
            return saves;
        }

        internal static bool BeginLoad(SavedGame snapshot)
        {
            if (pendingLoad != null || !IsValid(snapshot) || SceneUtility.GetBuildIndexByScenePath(snapshot.scene) < 0)
                return false;

            pendingLoad = snapshot;
            try
            {
                if (SceneManager.LoadSceneAsync(snapshot.scene, LoadSceneMode.Single) != null) return true;
            }
            catch (ArgumentException)
            {
                // Unity can reject a scene path even after the build list check.
            }
            pendingLoad = null;
            return false;
        }

        private static bool IsValid(SavedGame snapshot) =>
            snapshot != null && snapshot.version == SavedGame.CurrentVersion &&
            snapshot.savedAtUtc > 0 && snapshot.savedAtUtc <= DateTime.MaxValue.Ticks &&
            !string.IsNullOrEmpty(snapshot.scene) && snapshot.player?.weapon != null &&
            HandConfiguration.IsValidMask(snapshot.player.weapon.mask) &&
            snapshot.enemies != null && snapshot.enemies.All(entry => !string.IsNullOrEmpty(entry?.id)) &&
            snapshot.doors != null && snapshot.doors.All(entry => !string.IsNullOrEmpty(entry?.id)) &&
            (snapshot.lifts == null || snapshot.lifts.All(entry => !string.IsNullOrEmpty(entry?.id))) &&
            snapshot.arenas != null && snapshot.arenas.All(entry => !string.IsNullOrEmpty(entry?.id)) &&
            snapshot.consumedPickups != null && snapshot.consumedPickups.All(id => !string.IsNullOrEmpty(id));

        private static void Remove(GameObject target)
        {
            target.SetActive(false);
            UnityEngine.Object.Destroy(target);
        }

        // Resolve scene objects before gameplay can destroy them. Names plus same-name
        // ordinals keep IDs stable when unrelated siblings are added or removed.
        private static void Collect<T>(GameObject[] roots, Dictionary<string, T> entries, Func<T, bool> include = null)
            where T : Component
        {
            foreach (var root in roots)
                foreach (var item in root.GetComponentsInChildren<T>(true))
                    if (include == null || include(item))
                        entries.Add(SceneId(item.transform, roots), item);
        }

        private static string SceneId(Transform item, GameObject[] roots)
        {
            var parent = item.parent;
            int ordinal = 0;
            if (parent)
            {
                for (int index = 0; index < item.GetSiblingIndex(); index++)
                    if (parent.GetChild(index).name == item.name) ordinal++;
            }
            else
            {
                foreach (var root in roots)
                {
                    if (root == item.gameObject) break;
                    if (root.name == item.name) ordinal++;
                }
            }
            string segment = $"{item.name.Length}:{item.name}:{ordinal}/";
            return parent ? SceneId(parent, roots) + segment : segment;
        }
    }
}
