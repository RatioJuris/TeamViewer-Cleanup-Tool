using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace TeamViewerCleanup.Core
{
    public sealed class RegistryTarget
    {
        public RegistryTarget(RegistryHive hive, RegistryView view, string subKey)
        {
            Hive = hive;
            View = view;
            SubKey = subKey;
        }

        public RegistryHive Hive { get; }
        public RegistryView View { get; }
        public string SubKey { get; }

        public string Display
        {
            get
            {
                string hive = Hive == RegistryHive.LocalMachine ? "HKEY_LOCAL_MACHINE" : "HKEY_CURRENT_USER";
                string view = View == RegistryView.Registry32 ? " (32-bit view)" : "";
                return $@"{hive}\{SubKey}{view}";
            }
        }
    }

    /// <summary>Where to look for TeamViewer. <see cref="ForThisComputer"/> is used by the app; tests supply their own.</summary>
    public sealed class ScanTargets
    {
        public List<string> Folders { get; } = new List<string>();

        /// <summary>Folders whose TeamViewer*.lnk files and TeamViewer* subfolders are shortcuts.</summary>
        public List<string> ShortcutFolders { get; } = new List<string>();

        public List<RegistryTarget> RegistryKeys { get; } = new List<RegistryTarget>();

        /// <summary>Run keys whose values pointing at TeamViewer start it with Windows.</summary>
        public List<RegistryTarget> RunKeys { get; } = new List<RegistryTarget>();

        /// <summary>Uninstall keys listing installed programs.</summary>
        public List<RegistryTarget> UninstallKeys { get; } = new List<RegistryTarget>();

        public List<string> ProcessNames { get; } = new List<string>();

        public bool IncludeServices { get; set; }

        public static ScanTargets ForThisComputer()
        {
            var t = new ScanTargets { IncludeServices = true };

            string Env(string name) => Environment.GetEnvironmentVariable(name) ?? "";
            string Known(Environment.SpecialFolder f) => Environment.GetFolderPath(f);

            // ProgramW6432 is the 64-bit Program Files folder, even from a 32-bit process.
            var programFiles = new[] { Env("ProgramW6432"), Env("ProgramFiles"), Env("ProgramFiles(x86)") };
            foreach (var root in programFiles) AddFolder(t.Folders, root, "TeamViewer");

            AddFolder(t.Folders, Known(Environment.SpecialFolder.CommonApplicationData), "TeamViewer");
            AddFolder(t.Folders, Known(Environment.SpecialFolder.ApplicationData), "TeamViewer");
            AddFolder(t.Folders, Known(Environment.SpecialFolder.LocalApplicationData), "TeamViewer");
            AddFolder(t.Folders, Path.GetTempPath(), "TeamViewer");

            foreach (var f in new[]
            {
                Environment.SpecialFolder.CommonPrograms, Environment.SpecialFolder.Programs,
                Environment.SpecialFolder.CommonDesktopDirectory, Environment.SpecialFolder.DesktopDirectory,
            })
            {
                var path = Known(f);
                if (!string.IsNullOrEmpty(path) && !t.ShortcutFolders.Contains(path, StringComparer.OrdinalIgnoreCase))
                    t.ShortcutFolders.Add(path);
            }

            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                t.RegistryKeys.Add(new RegistryTarget(RegistryHive.LocalMachine, view, @"SOFTWARE\TeamViewer"));
                t.RunKeys.Add(new RegistryTarget(RegistryHive.LocalMachine, view, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"));
                t.UninstallKeys.Add(new RegistryTarget(RegistryHive.LocalMachine, view, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"));
            }
            t.RegistryKeys.Add(new RegistryTarget(RegistryHive.CurrentUser, RegistryView.Default, @"Software\TeamViewer"));
            t.RunKeys.Add(new RegistryTarget(RegistryHive.CurrentUser, RegistryView.Default, @"Software\Microsoft\Windows\CurrentVersion\Run"));
            t.UninstallKeys.Add(new RegistryTarget(RegistryHive.CurrentUser, RegistryView.Default, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"));

            t.ProcessNames.AddRange(new[]
            {
                "TeamViewer", "TeamViewer_Service", "TeamViewer_Desktop", "TeamViewer_Note",
                "tv_w32", "tv_x64", "TeamViewerQS", "TeamViewerQJ", "TeamViewerHost",
            });

            return t;
        }

        private static void AddFolder(List<string> list, string root, string name)
        {
            if (string.IsNullOrEmpty(root)) return;
            var path = Path.Combine(root, name);
            if (!list.Contains(path, StringComparer.OrdinalIgnoreCase)) list.Add(path);
        }
    }

    internal static class ListExtensions
    {
        public static bool Contains(this List<string> list, string value, StringComparer comparer)
        {
            foreach (var item in list) if (comparer.Equals(item, value)) return true;
            return false;
        }
    }
}
