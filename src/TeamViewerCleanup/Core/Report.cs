using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TeamViewerCleanup.Core
{
    public static class Report
    {
        public static string Summary(ScanResult scan)
        {
            if (scan.IsClean) return "No trace of TeamViewer was found on this computer.";

            var state = new List<string>();
            if (scan.IsRunning) state.Add("running");
            if (scan.StartsWithWindows) state.Add("set to start with Windows");
            string stateText = state.Count == 0 ? "" : string.Join(" and ", state);

            if (scan.IsInstalled)
                return stateText.Length == 0 ? "TeamViewer is installed." : $"TeamViewer is installed, {stateText}.";

            int leftovers = scan.Traces.Count;
            string text = $"TeamViewer is not installed, but {leftovers} leftover item{(leftovers == 1 ? " remains" : "s remain")}";
            return stateText.Length == 0 ? text + "." : $"{text}. Parts of it are still {stateText}.";
        }

        public static string Build(ScanResult scan, IEnumerable<RemovalResult>? removals = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"TeamViewer Cleanup Tool {AppInfo.Version}");
            sb.AppendLine($"Computer: {Environment.MachineName}    Date: {DateTime.Now:yyyy-MM-dd HH:mm}    Administrator: {(Elevation.IsAdministrator ? "yes" : "no")}");
            sb.AppendLine();
            sb.AppendLine(Summary(scan));
            if (!scan.IsClean)
            {
                sb.AppendLine();
                sb.AppendLine("Found:");
                foreach (var t in scan.Traces)
                {
                    sb.AppendLine($"  [{t.KindLabel}] {t.Name}{(t.Version.Length > 0 ? " " + t.Version : "")}");
                    sb.AppendLine($"      {t.Location}");
                }
            }
            var list = removals?.ToList();
            if (list != null && list.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Removal:");
                foreach (var r in list)
                    sb.AppendLine($"  {(r.Success ? "OK  " : "FAIL")} [{r.Trace.KindLabel}] {r.Trace.Location} - {r.Message}");
            }
            return sb.ToString();
        }
    }
}
