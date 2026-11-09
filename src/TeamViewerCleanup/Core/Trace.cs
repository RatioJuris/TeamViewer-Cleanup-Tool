using Microsoft.Win32;

namespace TeamViewerCleanup.Core
{
    public enum TraceKind
    {
        Installation,
        Process,
        Service,
        AutoStart,
        Folder,
        Shortcut,
        RegistryKey,
    }

    /// <summary>Something TeamViewer left on this computer.</summary>
    public sealed class Trace
    {
        public TraceKind Kind { get; set; }

        /// <summary>Short human-readable name, e.g. "TeamViewer service".</summary>
        public string Name { get; set; } = "";

        /// <summary>Where it is: a path, registry key, process ID or service name.</summary>
        public string Location { get; set; } = "";

        /// <summary>Whether removing it needs administrator rights.</summary>
        public bool NeedsAdmin { get; set; }

        /// <summary>Installations are removed with TeamViewer's own uninstaller, not by this tool.</summary>
        public bool Removable => Kind != TraceKind.Installation;

        // Details used to remove the trace.
        internal string? Path { get; set; }
        internal RegistryHive Hive { get; set; }
        internal RegistryView View { get; set; }
        internal string? SubKey { get; set; }
        internal string? ValueName { get; set; }
        internal int ProcessId { get; set; }
        internal string? ServiceName { get; set; }

        /// <summary>For installations: the command that runs TeamViewer's uninstaller.</summary>
        public string? UninstallCommand { get; set; }

        public string Version { get; set; } = "";

        public string KindLabel
        {
            get
            {
                switch (Kind)
                {
                    case TraceKind.Installation: return "Installed program";
                    case TraceKind.Process: return "Running program";
                    case TraceKind.Service: return "Windows service";
                    case TraceKind.AutoStart: return "Starts with Windows";
                    case TraceKind.Folder: return "Folder";
                    case TraceKind.Shortcut: return "Shortcut";
                    case TraceKind.RegistryKey: return "Registry key";
                    default: return Kind.ToString();
                }
            }
        }

        public override string ToString() => $"{KindLabel}: {Name} — {Location}";
    }
}
