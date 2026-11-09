using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using TeamViewerCleanup.Core;
using Xunit;

namespace TeamViewerCleanup.Tests
{
    /// <summary>
    /// Builds a fake TeamViewer installation in a temporary folder and a private HKCU test key,
    /// next to unrelated items, and checks that exactly the TeamViewer items are found and removed.
    /// </summary>
    public sealed class ScanAndCleanTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "tvct-test-" + Guid.NewGuid().ToString("N"));
        private readonly string _testKey = @"Software\RatioJurisTvctTest-" + Guid.NewGuid().ToString("N");
        private readonly ScanTargets _targets = new ScanTargets();

        public ScanAndCleanTests()
        {
            // Folders: a TeamViewer one and an unrelated one.
            Directory.CreateDirectory(Path.Combine(_root, "AppData", "TeamViewer", "Logs"));
            File.WriteAllText(Path.Combine(_root, "AppData", "TeamViewer", "Logs", "log.txt"), "x");
            File.SetAttributes(Path.Combine(_root, "AppData", "TeamViewer", "Logs", "log.txt"), FileAttributes.ReadOnly);
            Directory.CreateDirectory(Path.Combine(_root, "AppData", "OtherApp"));
            _targets.Folders.Add(Path.Combine(_root, "AppData", "TeamViewer"));
            _targets.Folders.Add(Path.Combine(_root, "AppData", "Missing", "TeamViewer"));

            // Shortcuts: TeamViewer ones and an unrelated one.
            var desktop = Path.Combine(_root, "Desktop");
            Directory.CreateDirectory(Path.Combine(desktop, "TeamViewer"));
            File.WriteAllText(Path.Combine(desktop, "TeamViewer 15.lnk"), "");
            File.WriteAllText(Path.Combine(desktop, "Notepad.lnk"), "");
            _targets.ShortcutFolders.Add(desktop);

            // Registry: settings key, Run values, uninstall entries.
            using (var key = Registry.CurrentUser.CreateSubKey(_testKey + @"\TeamViewer")) key.SetValue("ClientID", 123, RegistryValueKind.DWord);
            using (var key = Registry.CurrentUser.CreateSubKey(_testKey + @"\Other")) key.SetValue("Keep", "yes");
            using (var run = Registry.CurrentUser.CreateSubKey(_testKey + @"\Run"))
            {
                run.SetValue("TeamViewer", @"""C:\Program Files\TeamViewer\TeamViewer.exe"" --minimize");
                run.SetValue("OneDrive", @"""C:\Program Files\Microsoft OneDrive\OneDrive.exe""");
            }
            using (var u = Registry.CurrentUser.CreateSubKey(_testKey + @"\Uninstall\TeamViewer"))
            {
                u.SetValue("DisplayName", "TeamViewer");
                u.SetValue("DisplayVersion", "15.60.3");
                u.SetValue("UninstallString", @"""C:\Program Files\TeamViewer\uninstall.exe""");
            }
            using (var u = Registry.CurrentUser.CreateSubKey(_testKey + @"\Uninstall\7-Zip")) u.SetValue("DisplayName", "7-Zip");

            _targets.RegistryKeys.Add(new RegistryTarget(RegistryHive.CurrentUser, RegistryView.Default, _testKey + @"\TeamViewer"));
            _targets.RunKeys.Add(new RegistryTarget(RegistryHive.CurrentUser, RegistryView.Default, _testKey + @"\Run"));
            _targets.UninstallKeys.Add(new RegistryTarget(RegistryHive.CurrentUser, RegistryView.Default, _testKey + @"\Uninstall"));
        }

        [Fact]
        public void Finds_exactly_the_TeamViewer_items()
        {
            var scan = new Scanner(_targets).Scan();

            Assert.Single(scan.Traces, t => t.Kind == TraceKind.Installation && t.Version == "15.60.3");
            Assert.Single(scan.Traces, t => t.Kind == TraceKind.Folder);
            Assert.Equal(2, scan.Traces.Count(t => t.Kind == TraceKind.Shortcut));
            Assert.Single(scan.Traces, t => t.Kind == TraceKind.RegistryKey);
            Assert.Single(scan.Traces, t => t.Kind == TraceKind.AutoStart && t.Name == "TeamViewer");
            Assert.True(scan.IsInstalled);
            Assert.True(scan.StartsWithWindows);
            Assert.DoesNotContain(scan.Traces, t => t.Location.Contains("OtherApp") || t.Location.Contains("Notepad") || t.Name == "OneDrive" || t.Name == "7-Zip");
        }

        [Fact]
        public void Removes_TeamViewer_items_and_leaves_everything_else()
        {
            var scanner = new Scanner(_targets);
            var results = new Cleaner().Remove(scanner.Scan().Traces.Where(t => t.Removable));

            Assert.All(results, r => Assert.True(r.Success, r.Trace.Location + ": " + r.Message));

            var after = scanner.Scan();
            Assert.All(after.Traces, t => Assert.Equal(TraceKind.Installation, t.Kind)); // only the uninstall entry remains

            Assert.False(Directory.Exists(Path.Combine(_root, "AppData", "TeamViewer")));
            Assert.True(Directory.Exists(Path.Combine(_root, "AppData", "OtherApp")));
            Assert.True(File.Exists(Path.Combine(_root, "Desktop", "Notepad.lnk")));
            using (var other = Registry.CurrentUser.OpenSubKey(_testKey + @"\Other")) Assert.NotNull(other);
            using (var run = Registry.CurrentUser.OpenSubKey(_testKey + @"\Run"))
            {
                Assert.Null(run!.GetValue("TeamViewer"));
                Assert.NotNull(run.GetValue("OneDrive"));
            }
        }

        [Fact]
        public void Safety_check_refuses_non_TeamViewer_folders()
        {
            var important = Path.Combine(_root, "Important");
            Directory.CreateDirectory(important);
            var forged = new Trace { Kind = TraceKind.Folder, Name = "forged", Location = important, Path = important };

            var result = new Cleaner().RemoveOne(forged);

            Assert.False(result.Success);
            Assert.True(Directory.Exists(important));
        }

        [Fact]
        public void Safety_check_refuses_non_TeamViewer_registry_keys()
        {
            var forged = new Trace { Kind = TraceKind.RegistryKey, Hive = RegistryHive.CurrentUser, View = RegistryView.Default, SubKey = _testKey + @"\Other" };

            var result = new Cleaner().RemoveOne(forged);

            Assert.False(result.Success);
            using (var other = Registry.CurrentUser.OpenSubKey(_testKey + @"\Other")) Assert.NotNull(other);
        }

        [Fact]
        public void Summary_describes_the_situation_in_plain_language()
        {
            var scan = new Scanner(_targets).Scan();
            Assert.Equal("TeamViewer is installed, set to start with Windows.", Report.Summary(scan));
            Assert.Equal("No trace of TeamViewer was found on this computer.", Report.Summary(new ScanResult()));
        }

        public void Dispose()
        {
            try
            {
                foreach (var f in Directory.Exists(_root) ? Directory.GetFiles(_root, "*", SearchOption.AllDirectories) : new string[0])
                    File.SetAttributes(f, FileAttributes.Normal);
                if (Directory.Exists(_root)) Directory.Delete(_root, true);
            }
            catch (IOException) { }
            Registry.CurrentUser.DeleteSubKeyTree(_testKey, throwOnMissingSubKey: false);
        }
    }
}
