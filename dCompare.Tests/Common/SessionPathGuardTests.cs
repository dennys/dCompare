using dCompare.Common;
using Xunit;

namespace dCompare.Tests.Common
{
    public class SessionPathGuardTests
    {
        [Theory]
        [InlineData(@"C:\Users\dennys\src", true)]
        [InlineData(@"C:\", true)]
        [InlineData(@"D:/x", true)]
        [InlineData(@"\\evil\share", false)]          // UNC: relayable SMB auth
        [InlineData(@"\\evil\share\sub", false)]
        [InlineData(@"relative\path", false)]
        [InlineData(@"sub\file.txt", false)]
        [InlineData(@"C:evil", false)]                // drive-relative: Path.IsPathRooted calls this rooted
        [InlineData(@"C:evil\file.txt", false)]       // and Windows resolves it off the drive's cwd
        [InlineData(@"C:..\..\..\Windows", false)]    // traversal from there to anywhere on the drive
        [InlineData(@"C:", false)]
        [InlineData(@"\\?\C:\Users\dennys\src", false)]
        public void IsAllowedComparePath(string path, bool expected)
        {
            Assert.Equal(expected, SessionPathGuard.IsAllowedComparePath(path));
        }

        [Fact]
        public void IsAllowedComparePath_EmptySidesAllowed()
        {
            Assert.True(SessionPathGuard.IsAllowedComparePath(string.Empty));
            Assert.True(SessionPathGuard.IsAllowedComparePath(null));
        }

        [Theory]
        [InlineData(@"\\evil\share")]
        [InlineData(@"relative.json")]
        [InlineData(@"C:session.json")]               // drive-relative: resolves off the drive's cwd
        [InlineData(@"C:..\..\session.json")]
        [InlineData("")]
        public void IsAllowedDefaultSessionFile_NonLocalRejected(string path)
        {
            Assert.False(SessionPathGuard.IsAllowedDefaultSessionFile(path));
        }

        [Fact]
        public void IsAllowedDefaultSessionFile_UnderUserProfileAccepted()
        {
            string profileFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "dCompare", "s.json");
            Assert.True(SessionPathGuard.IsAllowedDefaultSessionFile(profileFile));
        }
    }
}
