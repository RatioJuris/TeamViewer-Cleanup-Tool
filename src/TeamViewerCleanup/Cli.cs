using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TeamViewerCleanup.Core;

namespace TeamViewerCleanup
{
    /// <summary>Command-line mode for scripts and IT administrators.</summary>
    public static class Cli
    {
        public const int ExitClean = 0;
        public const int ExitTracesFound = 1;
        public const int ExitErrors = 2;
        public const int ExitNeedsAdmin = 3;
        public const int ExitStillInstalled = 4;
        public const int ExitUsage = 64;

        public sealed class Options
        {
            public bool Scan { get; set; }
            public bool Clean { get; set; }
            public bool Yes { get; set; }
            public bool Uninstall { get; set; }
            public bool Help { get; set; }
            public string? LogFile { get; set; }
            public string? Screenshot { get; set; }
            public string? Error { get; set; }
        }

        public static Options Parse(string[] args)
        {
            var o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant().TrimStart('-', '/'))
                {
                    case "scan": o.Scan = true; break;
                    case "clean": o.Clean = true; break;
                    case "yes": case "y": o.Yes = true; break;
                    case "uninstall": o.Uninstall = true; break;
                    case "help": case "?": case "h": o.Help = true; break;
                    case "log":
                        if (i + 1 >= args.Length) { o.Error = "--log needs a file path."; break; }
                        o.LogFile = args[++i];
                        break;
                    case "screenshot":
                        if (i + 1 >= args.Length) { o.Error = "--screenshot needs a file path."; break; }
                        o.Screenshot = args[++i];
                        break;
                    default:
                        o.Error = $"Unknown option: {args[i]}";
                        break;
                }
            }
            if (o.Error == null && o.Clean && !o.Yes) o.Error = "--clean removes items without asking. Add --yes to confirm.";
            if (o.Error == null && o.Uninstall && !o.Clean) o.Error = "--uninstall is used together with --clean --yes.";
            return o;
        }

        public const string HelpText =
@"TeamViewer Cleanup Tool - find and remove everything TeamViewer leaves behind.

Usage:
  TeamViewerCleanupTool.exe                         Open the window
  TeamViewerCleanupTool.exe --scan [--log FILE]     List what is found; change nothing
  TeamViewerCleanupTool.exe --clean --yes [--uninstall] [--log FILE]
                                                    Remove everything found
Options:
  --uninstall   Run TeamViewer's own uninstaller silently first, if it is installed
  --log FILE    Also write the report to FILE

Exit codes:
  0  nothing found / everything removed     3  administrator rights needed
  1  traces found (--scan)                  4  TeamViewer is still installed
  2  some items could not be removed       64  invalid options

This tool does not reset TeamViewer IDs or change licence status.";

        public static int Run(Options o)
        {
            AttachConsole();
            if (o.Error != null)
            {
                Console.Error.WriteLine(o.Error);
                Console.Error.WriteLine("Run with --help for usage.");
                return ExitUsage;
            }
            if (o.Help)
            {
                Console.WriteLine(HelpText);
                return ExitClean;
            }

            var scanner = new Scanner(ScanTargets.ForThisComputer());
            var scan = scanner.Scan();

            if (!o.Clean)
            {
                Write(Report.Build(scan), o.LogFile);
                return scan.IsClean ? ExitClean : ExitTracesFound;
            }

            if (!Elevation.IsAdministrator && (o.Uninstall && scan.IsInstalled || scan.Traces.Any(t => t.Removable && t.NeedsAdmin)))
            {
                Console.Error.WriteLine("Some of what was found can only be removed with administrator rights. Run this from an elevated Command Prompt or PowerShell.");
                Write(Report.Build(scan), o.LogFile);
                return ExitNeedsAdmin;
            }

            if (o.Uninstall && scan.IsInstalled)
            {
                foreach (var installation in scan.Installations.ToList())
                {
                    Console.WriteLine($"Running the uninstaller for {installation.Name}...");
                    Cleaner.RunUninstaller(installation, silent: true);
                }
                scan = scanner.Scan();
            }

            var results = new Cleaner().Remove(scan.Traces.Where(t => t.Removable));
            var after = scanner.Scan();
            Write(Report.Build(scan, results) + Environment.NewLine + "After cleaning: " + Report.Summary(after), o.LogFile);

            if (results.Any(r => !r.Success)) return ExitErrors;
            if (after.IsInstalled) return ExitStillInstalled;
            return ExitClean;
        }

        private static void Write(string text, string? logFile)
        {
            Console.WriteLine(text);
            if (!string.IsNullOrEmpty(logFile)) File.WriteAllText(logFile!, text, System.Text.Encoding.UTF8);
        }

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);

        private static void AttachConsole()
        {
            // This is a Windows app; attach to the console that started it so output is visible.
            if (AttachConsole(-1))
            {
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
                Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
                Console.WriteLine();
            }
        }
    }
}
