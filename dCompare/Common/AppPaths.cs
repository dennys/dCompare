using Serilog;
using System.IO;

namespace dCompare.Common
{
    /// <summary>
    /// The single portable home for everything the app persists (dCompare.json, recentSessions.json,
    /// logs): the executable folder, so behaviour never depends on how the exe was launched.
    /// </summary>
    public static class AppPaths
    {
        /// <summary>Base directory every persisted file hangs off. AppContext (not AppDomain) so it is
        /// also correct when the app is started from another process's working directory.</summary>
        public static string BaseDirectory { get; } = AppContext.BaseDirectory;

        /// <summary>Absolute log path under <see cref="BaseDirectory"/>; overrides the relative path in Serilog.json.</summary>
        public static string LogFile => Path.GetFullPath(Path.Combine(BaseDirectory, Constant.DefaultLogFile));

        /// <summary>Write target when the exe folder is read-only (e.g. installed under Program Files).</summary>
        public static string FallbackDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dCompare");

        /// <summary>Full path of a persisted file in the portable home (whether or not it exists yet).</summary>
        public static string Resolve(string fileName) => Path.Combine(BaseDirectory, fileName);

        /// <summary>
        /// Existing copy of a persisted file: the portable home first, then the
        /// <see cref="FallbackDirectory"/> copy written when the exe folder was read-only.
        /// </summary>
        public static string ResolveExisting(string fileName)
        {
            string portable = Resolve(fileName);
            if (File.Exists(portable)) return portable;
            string fallback = GetFallbackPath(portable);
            return File.Exists(fallback) ? fallback : portable;
        }

        /// <summary>Same file name inside <see cref="FallbackDirectory"/> (does not create the directory).</summary>
        public static string GetFallbackPath(string path) => Path.Combine(FallbackDirectory, Path.GetFileName(path));

        /// <summary>
        /// Copy files that older builds (which used the working directory) left behind into the
        /// portable home. The old file is only ever copied, never deleted, and an existing new file wins.
        /// </summary>
        public static void MigrateFromWorkingDirectory(params string[] fileNames)
        {
            foreach (string fileName in fileNames)
            {
                string target = Resolve(fileName);
                string legacy = Path.GetFullPath(fileName, Directory.GetCurrentDirectory());
                if (File.Exists(target) || !File.Exists(legacy)
                    || string.Equals(target, legacy, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                try
                {
                    File.Copy(legacy, target, overwrite: false);
                    Log.Information("Migrated {FileName} from '{Legacy}' to '{Target}'.", fileName, legacy, target);
                }
                catch (Exception ex)
                {
                    // Read-only exe folder: the file stays in the working directory and writes fall back to FallbackDirectory.
                    Log.Warning(ex, "Could not migrate {FileName} from '{Legacy}' to '{Target}'.", fileName, legacy, target);
                }
            }
        }
    }
}
