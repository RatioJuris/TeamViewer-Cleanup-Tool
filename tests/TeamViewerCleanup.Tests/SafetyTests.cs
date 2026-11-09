using TeamViewerCleanup;
using TeamViewerCleanup.Core;
using Xunit;

namespace TeamViewerCleanup.Tests
{
    public class SafetyTests
    {
        [Theory]
        [InlineData(@"C:\Program Files\TeamViewer", true)]
        [InlineData(@"C:\Program Files (x86)\TeamViewer\", true)]
        [InlineData(@"C:\Users\A\AppData\Roaming\TeamViewer", true)]
        [InlineData(@"C:\Users\A\Desktop\TeamViewer 15.lnk", true)]
        [InlineData(@"C:\Program Files", false)]
        [InlineData(@"C:\Users\A\AppData\Roaming", false)]
        [InlineData(@"C:\", false)]
        [InlineData(@"C:\Users\A\Documents\Not TeamViewer", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Only_TeamViewer_paths_may_be_deleted(string? path, bool expected) =>
            Assert.Equal(expected, Safety.IsTeamViewerPath(path));

        [Theory]
        [InlineData(@"SOFTWARE\TeamViewer", true)]
        [InlineData(@"Software\TeamViewer\", true)]
        [InlineData(@"SOFTWARE", false)]
        [InlineData(@"SOFTWARE\TeamViewer\Something", false)]
        [InlineData(@"SOFTWARE\Microsoft", false)]
        public void Only_TeamViewer_registry_keys_may_be_deleted(string key, bool expected) =>
            Assert.Equal(expected, Safety.IsTeamViewerKey(key));

        [Theory]
        [InlineData("\"C:\\Program Files\\TeamViewer\\uninstall.exe\" /x", @"C:\Program Files\TeamViewer\uninstall.exe", "/x")]
        [InlineData(@"C:\Program Files (x86)\TeamViewer\uninstall.exe", @"C:\Program Files (x86)\TeamViewer\uninstall.exe", "")]
        [InlineData("MsiExec.exe /X{ABC}", "MsiExec.exe", "/X{ABC}")]
        public void Uninstall_commands_are_split_correctly(string command, string file, string args)
        {
            Cleaner.SplitCommand(command, out var f, out var a);
            Assert.Equal(file, f);
            Assert.Equal(args, a);
        }
    }

    public class CliTests
    {
        [Fact]
        public void Scan_with_log()
        {
            var o = Cli.Parse(new[] { "--scan", "--log", "report.txt" });
            Assert.True(o.Scan);
            Assert.Equal("report.txt", o.LogFile);
            Assert.Null(o.Error);
        }

        [Fact]
        public void Clean_requires_explicit_confirmation()
        {
            Assert.NotNull(Cli.Parse(new[] { "--clean" }).Error);
            Assert.Null(Cli.Parse(new[] { "--clean", "--yes" }).Error);
            Assert.Null(Cli.Parse(new[] { "/clean", "/y", "/uninstall" }).Error);
        }

        [Fact]
        public void Uninstall_only_with_clean() => Assert.NotNull(Cli.Parse(new[] { "--uninstall" }).Error);

        [Fact]
        public void Unknown_options_are_rejected() => Assert.NotNull(Cli.Parse(new[] { "--reset-id" }).Error);
    }
}
