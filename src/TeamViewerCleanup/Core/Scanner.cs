using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using Microsoft.Win32;

namespace TeamViewerCleanup.Core
{
    public sealed class ScanResult
    {
        public List<Trace> Traces { get; } = new List<Trace>();

        public IEnumerable<Trace> Installations => Traces.Where(t => t.Kind == TraceKind.Installation);

        public bool IsInstalled => Installations.Any();

        public bool IsRunning => Traces.Any(t => t.Kind == TraceKind.Process || t.Kind == TraceKind.Service);

        public bool StartsWithWindows => Traces.Any(t => t.Kind == TraceKind.AutoStart || t.Kind == TraceKind.Service);

        public bool IsClean => Traces.Count == 0;
    }

    /// <summary>Finds TeamViewer on this computer. Scanning never changes anything.</summary>
    public sealed class Scanner
    {
        private readonly ScanTargets _targets;

        public Scanner(ScanTargets targets) => _targets = targets;

        public ScanResult Scan()
        {
            var result = new ScanResult();
            FindInstallations(result);
            FindProcesses(result);
            FindServices(result);
            FindAutoStart(result);
            FindFolders(result);
            FindShortcuts(result);
            FindRegistryKeys(result);
            return result;
        }

        private void FindInstallations(ScanResult result)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var target in _targets.UninstallKeys)
            {
                using (var key = Open(target))
                {
                    if (key == null) continue;
                    foreach (var name in key.GetSubKeyNames())
                    {
                        using (var entry = key.OpenSubKey(name))
                        {
                            var displayName = entry?.GetValue("DisplayName") as string;
                            if (displayName == null || displayName.IndexOf("TeamViewer", StringComparison.OrdinalIgnoreCase) < 0) continue;
                            var uninstall = entry!.GetValue("UninstallString") as string;
                            if (!seen.Add(displayName + "|" + uninstall)) continue;
                            result.Traces.Add(new Trace
                            {
                                Kind = TraceKind.Installation,
                                Name = displayName,
                                Version = entry.GetValue("DisplayVersion") as string ?? "",
                                Location = entry.GetValue("InstallLocation") as string ?? target.Display + "\\" + name,
                                UninstallCommand = uninstall,
                                NeedsAdmin = target.Hive == RegistryHive.LocalMachine,
                            });
                        }
                    }
                }
            }
        }

        private void FindProcesses(ScanResult result)
        {
            foreach (var name in _targets.ProcessNames)
            {
                Process[] processes;
                try { processes = Process.GetProcessesByName(name); }
                catch (Exception) { continue; }
                foreach (var p in processes)
                {
                    using (p)
                    {
                        result.Traces.Add(new Trace
                        {
                            Kind = TraceKind.Process,
                            Name = p.ProcessName + ".exe",
                            Location = "Process ID " + p.Id,
                            ProcessId = p.Id,
                            NeedsAdmin = true,
                        });
                    }
                }
            }
        }

        private void FindServices(ScanResult result)
        {
            if (!_targets.IncludeServices) return;
            ServiceController[] services;
            try { services = ServiceController.GetServices(); }
            catch (Exception) { return; }
            foreach (var s in services)
            {
                using (s)
                {
                    if (!s.ServiceName.StartsWith("TeamViewer", StringComparison.OrdinalIgnoreCase)
                        && s.DisplayName.IndexOf("TeamViewer", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    result.Traces.Add(new Trace
                    {
                        Kind = TraceKind.Service,
                        Name = s.DisplayName,
                        Location = $"Service “{s.ServiceName}” ({s.Status})",
                        ServiceName = s.ServiceName,
                        NeedsAdmin = true,
                    });
                }
            }
        }

        private void FindAutoStart(ScanResult result)
        {
            foreach (var target in _targets.RunKeys)
            {
                using (var key = Open(target))
                {
                    if (key == null) continue;
                    foreach (var valueName in key.GetValueNames())
                    {
                        var command = key.GetValue(valueName) as string;
                        if (!Safety.IsTeamViewerCommand(command) && !Safety.IsTeamViewerCommand(valueName)) continue;
                        result.Traces.Add(new Trace
                        {
                            Kind = TraceKind.AutoStart,
                            Name = valueName,
                            Location = target.Display + " -> " + command,
                            Hive = target.Hive,
                            View = target.View,
                            SubKey = target.SubKey,
                            ValueName = valueName,
                            NeedsAdmin = target.Hive == RegistryHive.LocalMachine,
                        });
                    }
                }
            }
        }

        private void FindFolders(ScanResult result)
        {
            foreach (var folder in _targets.Folders)
            {
                if (!Directory.Exists(folder)) continue;
                result.Traces.Add(new Trace
                {
                    Kind = TraceKind.Folder,
                    Name = DescribeFolder(folder),
                    Location = folder,
                    Path = folder,
                    NeedsAdmin = !IsUserFolder(folder),
                });
            }
        }

        private void FindShortcuts(ScanResult result)
        {
            foreach (var root in _targets.ShortcutFolders)
            {
                if (!Directory.Exists(root)) continue;
                IEnumerable<string> files, dirs;
                try
                {
                    files = Directory.EnumerateFiles(root, "TeamViewer*.lnk").ToList();
                    dirs = Directory.EnumerateDirectories(root, "TeamViewer*").ToList();
                }
                catch (Exception) { continue; }

                foreach (var path in files.Concat(dirs))
                {
                    result.Traces.Add(new Trace
                    {
                        Kind = TraceKind.Shortcut,
                        Name = Path.GetFileName(path),
                        Location = path,
                        Path = path,
                        NeedsAdmin = !IsUserFolder(path),
                    });
                }
            }
        }

        private void FindRegistryKeys(ScanResult result)
        {
            foreach (var target in _targets.RegistryKeys)
            {
                using (var key = Open(target))
                {
                    if (key == null) continue;
                }
                result.Traces.Add(new Trace
                {
                    Kind = TraceKind.RegistryKey,
                    Name = target.Hive == RegistryHive.CurrentUser ? "Your TeamViewer settings" : "TeamViewer settings for this computer",
                    Location = target.Display,
                    Hive = target.Hive,
                    View = target.View,
                    SubKey = target.SubKey,
                    NeedsAdmin = target.Hive == RegistryHive.LocalMachine,
                });
            }
        }

        internal static RegistryKey? Open(RegistryTarget target, bool writable = false)
        {
            try
            {
                using (var root = RegistryKey.OpenBaseKey(target.Hive, target.View))
                    return root.OpenSubKey(target.SubKey, writable);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string DescribeFolder(string folder)
        {
            string parent = Path.GetDirectoryName(folder) ?? "";
            string Is(Environment.SpecialFolder f) => Environment.GetFolderPath(f);
            if (parent.Equals(Is(Environment.SpecialFolder.ApplicationData), StringComparison.OrdinalIgnoreCase)) return "Your TeamViewer data and logs";
            if (parent.Equals(Is(Environment.SpecialFolder.LocalApplicationData), StringComparison.OrdinalIgnoreCase)) return "Your TeamViewer local data";
            if (parent.Equals(Is(Environment.SpecialFolder.CommonApplicationData), StringComparison.OrdinalIgnoreCase)) return "TeamViewer data for all users";
            if (parent.TrimEnd('\\').Equals(Path.GetTempPath().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return "TeamViewer temporary files";
            return "TeamViewer program files";
        }

        private static bool IsUserFolder(string path)
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return !string.IsNullOrEmpty(profile) && path.StartsWith(profile + "\\", StringComparison.OrdinalIgnoreCase);
        }
    }
}
