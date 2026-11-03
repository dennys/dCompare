using System.ComponentModel;
using dCompare.FileOperation.Strategies;

namespace dCompare.Tests.FileOperation.Strategies
{
    public class FileOperationRetryTests
    {
        [Fact]
        public void Run_SucceedsAfterTemporarySharingViolation_Retries()
        {
            var calls = 0;
            FileOperationRetry.Run("test", () =>
            {
                calls++;
                if (calls < 3) throw new Win32Exception(32); // ERROR_SHARING_VIOLATION
            });

            Assert.Equal(3, calls);
        }

        [Fact]
        public void Run_SucceedsAfterLockViolation_Retries()
        {
            var calls = 0;
            FileOperationRetry.Run("test", () =>
            {
                calls++;
                if (calls < 2) throw new Win32Exception(33); // ERROR_LOCK_VIOLATION
            });

            Assert.Equal(2, calls);
        }

        [Fact]
        public void Run_NonTemporaryError_DoesNotRetry()
        {
            var calls = 0;
            var ex = Record.Exception(() => FileOperationRetry.Run("test", () =>
            {
                calls++;
                throw new Win32Exception(2); // ERROR_FILE_NOT_FOUND
            }));

            Assert.NotNull(ex);
            Assert.IsType<Win32Exception>(ex);
            Assert.Equal(1, calls);
        }

        [Fact]
        public void Run_GenericException_DoesNotRetry()
        {
            var calls = 0;
            var ex = Record.Exception(() => FileOperationRetry.Run("test", () =>
            {
                calls++;
                throw new InvalidOperationException("boom");
            }));

            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex);
            Assert.Equal(1, calls);
        }

        [Fact]
        public void Run_ExhaustsRetries_ThenThrows()
        {
            var calls = 0;
            var ex = Record.Exception(() => FileOperationRetry.Run("test", () =>
            {
                calls++;
                throw new Win32Exception(32);
            }));

            Assert.NotNull(ex);
            Assert.IsType<Win32Exception>(ex);
            Assert.Equal(3, calls); // 1 + 2 retries
        }
    }
}
