using System.IO;

namespace dCompare.Common
{
    /// <summary>
    /// Path restrictions applied when loading sessions. UNC paths are rejected because an
    /// auto-compare after session load would perform SMB authentication against them (relayable
    /// to Net-NTLM2) and hash files at an attacker-chosen location.
    /// </summary>
    public static class SessionPathGuard
    {
        /// <summary>
        /// A rooted local path on a drive letter, such as <c>D:\data\src</c>. <see cref="Path.IsPathRooted"/>
        /// is not enough: it also calls <c>C:evil</c> rooted, while Windows resolves that against the current
        /// directory of the C: drive, which is how a session file could aim the scan at a folder the user never
        /// picked and reintroduce the working-directory behaviour this guard exists to keep out.
        /// </summary>
        private static bool IsRootedLocalPath(string path) =>
            path.Length > 2 && path[1] == ':' && (path[2] == '\\' || path[2] == '/');

        /// <summary>
        /// A compare path is allowed when it is empty (an unfilled side) or a rooted local path.
        /// </summary>
        public static bool IsAllowedComparePath(string? path) =>
            string.IsNullOrEmpty(path) || IsRootedLocalPath(path);

        /// <summary>
        /// The default session file must be a rooted local path under the user profile.
        /// </summary>
        public static bool IsAllowedDefaultSessionFile(string? path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                if (!IsRootedLocalPath(path)) return false;
                string full = Path.GetFullPath(path);
                string profile = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                return full.Equals(profile, StringComparison.OrdinalIgnoreCase)
                    || full.StartsWith(profile + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
