using dCompare.Session;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.IO;
using System.Threading;
using System.Windows.Controls;
using Xunit;

namespace dCompare.Tests.Session
{
    public class SessionManagerTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "dCompareTests", Guid.NewGuid().ToString("N"));

        public SessionManagerTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }

        // WPF element creation (TabControl) requires an STA thread; xunit runs on MTA.
        private static T RunSta<T>(Func<T> action)
        {
            T result = default!;
            Exception? error = null;
            var thread = new Thread(() =>
            {
                try { result = action(); } catch (Exception ex) { error = ex; }
            })
            { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (error != null) throw error;
            return result;
        }

        private static void RunSta(Action action)
        {
            Exception? error = null;
            var thread = new Thread(() =>
            {
                try { action(); } catch (Exception ex) { error = ex; }
            })
            { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (error != null) throw error;
        }

        [Fact]
        public void LoadSession_NonExistentFile_ThrowsFileNotFoundAndReportsNotFoundStatus()
        {
            string missingPath = Path.Combine(_dir, "missing.json");
            var status = new List<string>();

            RunSta(() =>
            {
                var sessionManager = new SessionManager(new ServiceCollection().BuildServiceProvider());
                sessionManager.Initialize(new TabControl(), new MenuItem());
                Assert.Throws<FileNotFoundException>(() =>
                    sessionManager.LoadSession(missingPath, false, status.Add, null));
            });

            // The not-found status is reported; nothing is reported as loaded.
            Assert.Contains(status, s => s.Contains(missingPath));
            Assert.DoesNotContain(status, s => s.Contains("Session loaded"));
        }

        [Fact]
        public void LoadSession_UncPath_RejectedAndNeverAutoCompares()
        {
            string sessionPath = Path.Combine(_dir, "evil.json");
            File.WriteAllText(sessionPath,
                "{\"tab1\": {\"TabName\": \"T1\", \"Left\": \"\\\\\\\\evil\\\\share\", \"Right\": \"C:\\\\ok\"}}");
            var interaction = new Mock<IUserInteractionService>();
            var status = new List<string>();
            int compares = 0;

            RunSta(() =>
            {
                var sessionManager = new SessionManager(new ServiceCollection().BuildServiceProvider(), interaction.Object);
                sessionManager.Initialize(new TabControl(), new MenuItem());
                sessionManager.LoadSession(sessionPath, true, status.Add, _ => compares++);
            });

            // The UNC entry is rejected before any compare is started or any service is resolved.
            Assert.Equal(0, compares);
            Assert.DoesNotContain(status, s => s.Contains("Session loaded"));
            Assert.Contains(status, s => s.Contains("evil"));
            interaction.Verify(i => i.ShowError(It.Is<string>(m => m.Contains("evil")), It.IsAny<string>()), Times.AtLeastOnce);
        }

        [Fact]
        public void LoadSession_AllEntriesRejected_ReturnsFalseWithoutSuccessStatus()
        {
            string sessionPath = Path.Combine(_dir, "allbad.json");
            File.WriteAllText(sessionPath,
                "{\"tab1\": {\"Left\": \"\\\\\\\\evil\\\\share\"}}");
            var status = new List<string>();

            bool loaded = RunSta(() =>
            {
                var sessionManager = new SessionManager(new ServiceCollection().BuildServiceProvider(), new Mock<IUserInteractionService>().Object);
                sessionManager.Initialize(new TabControl(), new MenuItem());
                return sessionManager.LoadSession(sessionPath, true, status.Add, null);
            });

            Assert.False(loaded);
            Assert.DoesNotContain(status, s => s.Contains("Session loaded"));
        }

        [Fact]
        public void LoadSession_EmptySessionFile_ReturnsFalseWithoutSuccessStatus()
        {
            string emptyPath = Path.Combine(_dir, "empty.json");
            File.WriteAllText(emptyPath, "null");
            var status = new List<string>();

            bool loaded = RunSta(() =>
            {
                var sessionManager = new SessionManager(new ServiceCollection().BuildServiceProvider());
                sessionManager.Initialize(new TabControl(), new MenuItem());
                return sessionManager.LoadSession(emptyPath, false, status.Add, null);
            });

            Assert.False(loaded);
            Assert.DoesNotContain(status, s => s.Contains("Session loaded"));
        }

        [Fact]
        public void TabIdMap_TabItemKeys_SameHeaderTabsStayDistinct()
        {
            var (map, tabA, tabB) = RunSta(() =>
            {
                var sessionManager = new SessionManager(new ServiceCollection().BuildServiceProvider());
                sessionManager.Initialize(new TabControl(), new MenuItem());
                var tabA = new TabItem { Header = "same" };
                var tabB = new TabItem { Header = "same" };
                sessionManager.TabIdMap[tabA] = "id-a";
                sessionManager.TabIdMap[tabB] = "id-b";
                return (sessionManager.TabIdMap, tabA, tabB);
            });

            // Reference-keyed: two tabs with an identical header are two entries, not one.
            Assert.Equal(2, map.Count);
            Assert.Equal("id-a", map[tabA]);
            Assert.Equal("id-b", map[tabB]);
        }
    }
}
