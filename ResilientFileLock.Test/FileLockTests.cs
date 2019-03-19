using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Path = ResilientFileLock.Test.FileLockTestPath;

namespace ResilientFileLock.Test
{
    [Collection(nameof(FileLockCollection))]
    public class FileLockTests
    {
        private readonly TimeSpan _timeVariable = TimeSpan.FromSeconds(5);
        private const string Extension = "lock";

        [Fact]
        public async void AcquireSecondLock()
        {
            var file = new FileInfo(Path.GetTempFileName());
            await new FileLock(file).TryAcquire(TimeSpan.FromHours(1));

            bool fileLock = await new FileLock(file).TryAcquire(TimeSpan.FromHours(1));
            Assert.False(fileLock);
        }

        [Fact]
        public async void AcquireSecondLockAfterRelease()
        {
            var file = new FileInfo(Path.GetTempFileName());
            ILock firstLock = new FileLock(file);
            using (firstLock)
            {
                await firstLock.TryAcquire(TimeSpan.FromSeconds(1));
            }
            ILock secondLock = new FileLock(file);
            using (secondLock)
            {
                var secondLockAcquired = await secondLock.TryAcquire(TimeSpan.FromSeconds(10));
                Assert.True(secondLockAcquired);
            }
        }

        [Fact]
        public async void DisposeTest()
        {
            var file = new FileInfo(Path.GetTempFileName());
            var filename = Path.ChangeExtension(file.FullName, Extension);
            ILock fileLock = new FileLock(file);
            if(await fileLock.TryAcquire(TimeSpan.FromHours(1)))
            {
                using (fileLock)
                {
                    Assert.True(File.Exists(filename));
                    fileLock.Dispose();
                }
            }

            var timeoutSpan = TimeSpan.FromSeconds(5);
            var cancellationTokenSource = new CancellationTokenSource(timeoutSpan);
            var cancellationToken = cancellationTokenSource.Token;
            var fileExists = File.Exists(filename);
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                if (!fileExists)
                {
                    break;
                }

                await Task.Delay(1, cancellationToken);
                fileExists = File.Exists(filename);
            }

            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            Assert.False(fileExists);
        }

        [Fact]
        public async void Many()
        {
            int i = 100;
            var result = true;
            for(int j = 0; j < i; j++)
            {
                var file = new FileInfo(Path.GetTempFileName());
                if(!await new FileLock(file).TryAcquire(TimeSpan.FromHours(1)) &&
                   !File.Exists(Path.ChangeExtension(file.FullName, Extension)))
                {
                    result = false;
                    break;
                }
            }

            Assert.True(result);
        }

        [Fact]
        public async void GetTimeReturnsMinValueWithNoLock()
        {
            var file = new FileInfo(Path.GetTempFileName());
            var fileLock = new FileLock(file);
            DateTime dateTime = await fileLock.GetReleaseDate();
            Assert.Equal(DateTime.MinValue, dateTime);
        }

        [Fact]
        public async void GetTimeReturnsCurrentReleaseDate()
        {
            var file = new FileInfo(Path.GetTempFileName());
            var fileLock = new FileLock(file);
            await fileLock.TryAcquire(TimeSpan.FromHours(1));
            DateTime dateTime = await fileLock.GetReleaseDate();
            Assert.NotEqual(DateTime.MaxValue, dateTime);
        }
    }
}
