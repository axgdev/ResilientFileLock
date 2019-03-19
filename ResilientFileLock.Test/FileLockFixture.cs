using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ResilientFileLock.Test
{
    internal class FileLockFixture : IDisposable
    {
        private const int TimeoutMilliseconds = 1_000;
        private const int PollMilliseconds = 100;

        public void Dispose()
        {
            Assert.True(TryToDeleteTestFolder().Result);
        }

        public async Task<bool> TryToDeleteTestFolder()
        {
            if (!Directory.Exists(FileLockTestPath.TempFolderPath))
            {
                return true;
            }

            using (var cancellationTokenSource = new CancellationTokenSource(TimeoutMilliseconds))
            {
                while (Directory.Exists(FileLockTestPath.TempFolderPath))
                {
                    try
                    {
                        Directory.Delete(FileLockTestPath.TempFolderPath, true);
                        return true;
                    }
                    catch
                    {
                        await Task.Delay(PollMilliseconds, cancellationTokenSource.Token);
                    }
                }
            }

            return false;
        }
    }

    [CollectionDefinition(nameof(FileLockCollection))]
    public class FileLockCollection : ICollectionFixture<FileLockFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}