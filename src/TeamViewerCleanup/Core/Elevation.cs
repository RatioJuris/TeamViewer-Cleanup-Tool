using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;

namespace TeamViewerCleanup.Core
{
    public static class Elevation
    {
        public static bool IsAdministrator
        {
            get
            {
                using (var identity = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        /// <summary>Restarts the app with administrator rights. Returns false if the user declined.</summary>
        public static bool RestartAsAdministrator()
        {
            var exe = Assembly.GetEntryAssembly()!.Location;
            try
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" });
                return true;
            }
            catch (Win32Exception)
            {
                return false; // The UAC prompt was cancelled.
            }
        }
    }

    public static class AppInfo
    {
        public static string Version
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return v == null ? "" : $"{v.Major}.{v.Minor}.{v.Build}";
            }
        }

        public const string Website = "https://github.com/RatioJuris/TeamViewer-Cleanup-Tool";
        public const string Support = "https://ratiojuris.github.io/legal/teamviewer-cleanup-tool/support/";
    }
}
