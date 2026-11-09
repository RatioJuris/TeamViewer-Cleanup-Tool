using System;
using System.IO;

namespace TeamViewerCleanup.Core
{
    /// <summary>
    /// Last line of defence: nothing is deleted unless it is plainly TeamViewer's.
    /// </summary>
    public static class Safety
    {
        private const string Marker = "TeamViewer";

        /// <summary>A folder or file may be deleted only if its own name starts with "TeamViewer".</summary>
        public static bool IsTeamViewerPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string full;
            try
            {
                full = Path.GetFullPath(path!).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception)
            {
                return false;
            }
            string name = Path.GetFileName(full);
            return name.StartsWith(Marker, StringComparison.OrdinalIgnoreCase)
                && Path.GetPathRoot(full)?.TrimEnd('\\') != full;
        }

        /// <summary>A registry key may be deleted only if its last segment is "TeamViewer".</summary>
        public static bool IsTeamViewerKey(string? subKey)
        {
            if (string.IsNullOrWhiteSpace(subKey)) return false;
            string last = subKey!.TrimEnd('\\');
            int slash = last.LastIndexOf('\\');
            if (slash >= 0) last = last.Substring(slash + 1);
            return last.Equals(Marker, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>A Run-key value counts as TeamViewer's only if its command refers to TeamViewer.</summary>
        public static bool IsTeamViewerCommand(string? command) =>
            command != null && command.IndexOf(Marker, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
