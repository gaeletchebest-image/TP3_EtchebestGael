using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HandShooter.Editor
{
    [InitializeOnLoad]
    public static class BalanceLabHistory
    {
        [Serializable] private sealed class History { public List<BalanceTrialResult> trials = new List<BalanceTrialResult>(); }
        private const string Path = "UserSettings/HandShooterBalanceHistory.json";
        private static History history;
        public static IReadOnlyList<BalanceTrialResult> Trials { get { Load(); return history.trials; } }

        static BalanceLabHistory() { BalanceLabSession.TrialFinished += Add; }

        private static void Load()
        {
            if (history != null) return;
            try { history = File.Exists(Path) ? JsonUtility.FromJson<History>(File.ReadAllText(Path)) : new History(); }
            catch (Exception e) { Debug.LogWarning("No se pudo leer el historial de balance: " + e.Message); }
            if (history == null || history.trials == null) history = new History();
        }

        private static void Add(BalanceTrialResult result)
        {
            Load();
            if (history.trials.Any(t => t.id == result.id)) return;
            history.trials.Insert(0, result);
            if (history.trials.Count > 100) history.trials.RemoveAt(100);
            Directory.CreateDirectory("UserSettings");
            File.WriteAllText(Path, JsonUtility.ToJson(history, true));
        }

        public static string Json() { Load(); return JsonUtility.ToJson(history, true); }
        public static string Csv()
        {
            Load();
            var csv = new StringBuilder("Fecha UTC,Perfil,Escenario,Resultado,Causa,Segundos,Disparos,Aciertos,Precision,Bajas,Combustible,Recargas,Daño efectivo,Ajustes cambiados,Configuracion,Snapshot\r\n");
            foreach (var r in history.trials)
            {
                object[] cells = { r.utc, r.profile, r.scenario, r.outcome, r.deathCause, r.seconds, r.shots,
                    r.hittingShots, r.Accuracy, r.kills, r.fuelSpent, r.reloads, r.effectiveDamage, r.settingsChanged, r.setup, r.balanceSnapshot };
                csv.AppendLine(string.Join(",", cells.Select(v => "\"" + Convert.ToString(v, CultureInfo.InvariantCulture).Replace("\"", "\"\"") + "\"")));
            }
            return csv.ToString();
        }
    }
}
