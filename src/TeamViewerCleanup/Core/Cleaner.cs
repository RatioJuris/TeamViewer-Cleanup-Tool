using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using Microsoft.Win32;

namespace TeamViewerCleanup.Core
{
    public sealed class RemovalResult
    {
        public RemovalResult(Trace trace, bool success, string message)
        {
            Trace = trace;
            Success = success;
            Message = message;
        }

        public Trace Trace { get; }
        public bool Success { get; }
        public string Message { get; }
    }

    /// <summary>Removes traces found by <see cref="Scanner"/>, in a safe order.</summary>
    public sealed class Cleaner
    {
        // Stop things that are running before deleting the files they use.
        private static readonly TraceKind[] Order =
        {
            TraceKind.Process, TraceKind.Service, TraceKind.AutoStart,
            TraceKind.Shortcut, TraceKind.Folder, TraceKind.RegistryKey,
        };

        public List<RemovalResult> Remove(IEnumerable<Trace> traces, Action<RemovalResult>? progress = null)
        {
            var list = new List<Trace>(traces);
            var results = new List<RemovalResult>();
            foreach (var kind in Order)
            {
                foreach (var trace in list)
                {
                    if (trace.Kind != kind) continue;
                    var r = RemoveOne(trace);
                    results.Add(r);
                    progress?.Invoke(r);
                }
            }
            return results;
        }

        public RemovalResult RemoveOne(Trace trace)
        {
            try
            {
                switch (trace.Kind)
                {
                    case TraceKind.Process: return StopProcess(trace);
                    case TraceKind.Service: return DeleteService(trace);
                    case TraceKind.AutoStart: return DeleteRunValue(trace);
                    case TraceKind.Shortcut:
                    case TraceKind.Folder: return DeletePath(trace);
                    case TraceKind.RegistryKey: return DeleteKey(trace);
                    default: return new RemovalResult(trace, false, "Use TeamViewer's own uninstaller for this.");
                }
            }
            catch (UnauthorizedAccessException)
            {
                return new RemovalResult(trace, false, "Access denied. Run the tool as administrator.");
            }
            catch (System.Security.SecurityException)
            {
                return new RemovalResult(trace, false, "Access denied. Run the tool as administrator.");
            }
            catch (Exception ex)
            {
                return new RemovalResult(trace, false, ex.Message);
            }
        }

        private static RemovalResult StopProcess(Trace trace)
        {
            using (var p = Process.GetProcessById(trace.ProcessId))
            {
                if (!p.ProcessName.StartsWith("TeamViewer", StringComparison.OrdinalIgnoreCase)
                    && !p.ProcessName.StartsWith("tv_", StringComparison.OrdinalIgnoreCase))
                    return new RemovalResult(trace, false, "Skipped: not a TeamViewer process.");
                p.Kill();
                p.WaitForExit(10000);
            }
            return new RemovalResult(trace, true, "Stopped.");
        }

        private static RemovalResult DeleteService(Trace trace)
        {
            var name = trace.ServiceName ?? "";
            if (!name.StartsWith("TeamViewer", StringComparison.OrdinalIgnoreCase) && trace.Name.IndexOf("TeamViewer", StringComparison.OrdinalIgnoreCase) < 0)
                return new RemovalResult(trace, false, "Skipped: not a TeamViewer service.");

            using (var sc = new ServiceController(name))
            {
                if (sc.Status != ServiceControllerStatus.Stopped)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                }
            }

            var psi = new ProcessStartInfo("sc.exe", $"delete \"{name}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            using (var p = Process.Start(psi)!)
            {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return p.ExitCode == 0
                    ? new RemovalResult(trace, true, "Stopped and removed.")
                    : new RemovalResult(trace, false, output.Trim().Length > 0 ? output.Trim() : "Could not remove the service.");
            }
        }

        private static RemovalResult DeleteRunValue(Trace trace)
        {
            using (var root = RegistryKey.OpenBaseKey(trace.Hive, trace.View))
            using (var key = root.OpenSubKey(trace.SubKey!, writable: true))
            {
                if (key == null) return new RemovalResult(trace, true, "Already gone.");
                var command = key.GetValue(trace.ValueName!) as string;
                if (!Safety.IsTeamViewerCommand(command) && !Safety.IsTeamViewerCommand(trace.ValueName))
                    return new RemovalResult(trace, false, "Skipped: entry no longer refers to TeamViewer.");
                key.DeleteValue(trace.ValueName!, throwOnMissingValue: false);
            }
            return new RemovalResult(trace, true, "Removed from startup.");
        }

        private static RemovalResult DeletePath(Trace trace)
        {
            var path = trace.Path!;
            if (!Safety.IsTeamViewerPath(path))
                return new RemovalResult(trace, false, "Skipped by safety check: not a TeamViewer path.");

            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                return new RemovalResult(trace, true, "Deleted.");
            }
            if (!Directory.Exists(path)) return new RemovalResult(trace, true, "Already gone.");

            // Files can stay locked for a moment after TeamViewer stops.
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    ClearReadOnly(path);
                    Directory.Delete(path, recursive: true);
                    return new RemovalResult(trace, true, "Deleted.");
                }
                catch (IOException) when (attempt < 3)
                {
                    Thread.Sleep(1000);
                }
            }
        }

        private static void ClearReadOnly(string folder)
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReadOnly) != 0) File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            }
        }

        private static RemovalResult DeleteKey(Trace trace)
        {
            if (!Safety.IsTeamViewerKey(trace.SubKey))
                return new RemovalResult(trace, false, "Skipped by safety check: not a TeamViewer key.");
            using (var root = RegistryKey.OpenBaseKey(trace.Hive, trace.View))
            {
                root.DeleteSubKeyTree(trace.SubKey!, throwOnMissingSubKey: false);
            }
            return new RemovalResult(trace, true, "Deleted.");
        }

        /// <summary>Runs TeamViewer's own uninstaller and waits for it to finish.</summary>
        public static bool RunUninstaller(Trace installation, bool silent)
        {
            var command = installation.UninstallCommand;
            if (string.IsNullOrWhiteSpace(command)) return false;
            SplitCommand(command!, out var file, out var arguments);
            if (file.IndexOf("TeamViewer", StringComparison.OrdinalIgnoreCase) < 0 && !file.EndsWith("msiexec.exe", StringComparison.OrdinalIgnoreCase))
                return false;
            if (silent) arguments = file.EndsWith("msiexec.exe", StringComparison.OrdinalIgnoreCase) ? arguments + " /qn" : arguments + " /S";
            using (var p = Process.Start(new ProcessStartInfo(file, arguments.Trim()) { UseShellExecute = true })!)
            {
                p.WaitForExit();
            }
            return true;
        }

        internal static void SplitCommand(string command, out string file, out string arguments)
        {
            command = command.Trim();
            if (command.StartsWith("\""))
            {
                int end = command.IndexOf('"', 1);
                file = end > 0 ? command.Substring(1, end - 1) : command.Trim('"');
                arguments = end > 0 ? command.Substring(end + 1).Trim() : "";
                return;
            }
            int exe = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exe > 0)
            {
                file = command.Substring(0, exe + 4);
                arguments = command.Substring(exe + 4).Trim();
                return;
            }
            file = command;
            arguments = "";
        }
    }
}
