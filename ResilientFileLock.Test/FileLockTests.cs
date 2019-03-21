using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ResilientFileLock.Test
{
    public class FileLockTests
    {
        private readonly TimeSpan _timeVariable = TimeSpan.FromSeconds(5);
        private const string Extension = "lock";

        [Fact]
        public async void AcquireSecondLock()
        {
            using (var testPath = new TestPath())
            using (var fileLock1 = new FileLock(testPath.TempFile))
            using (var fileLock2 = new FileLock(testPath.TempFile))
            {
                var fileLock1Acquired = await fileLock1.TryAcquire(TimeSpan.FromHours(1));
                var fileLock2Acquired = await fileLock2.TryAcquire(TimeSpan.FromHours(1));
                Assert.True(fileLock1Acquired);
                Assert.False(fileLock2Acquired);
            }
        }

        [Fact]
        public async void AcquireSecondLockAfterRelease()
        {
            using (var testPath = new TestPath())
            {
                using (var firstLock = new FileLock(testPath.TempFile))
                {
                    await firstLock.TryAcquire(TimeSpan.FromSeconds(1));
                }

                using (var secondLock = new FileLock(testPath.TempFile))
                {
                    var secondLockAcquired = await secondLock.TryAcquire(TimeSpan.FromSeconds(10));
                    Assert.True(secondLockAcquired);
                }
            }
        }

        [Fact]
        public async void BasicLock()
        {
            using (var testPath = new TestPath())
            using(var fileLock = new FileLock(testPath.TempFile))
            {
                var lockFilename = Path.ChangeExtension(testPath.TempFile.FullName, Extension);

                await fileLock.TryAcquire(TimeSpan.FromHours(1));
                Assert.True(File.Exists(lockFilename));

                var lockContentLines = File.ReadAllLines(lockFilename);
                Assert.True(Guid.TryParse(lockContentLines[0], out _));
                Assert.True(long.TryParse(lockContentLines[1], out var ticks));

                var fileDate = new DateTime(ticks);
                Assert.True(fileDate - DateTime.UtcNow - TimeSpan.FromHours(1) < _timeVariable);
            }
        }

        [Fact]
        public async void DisposeTest()
        {
            using (var testPath = new TestPath())
            {
                var file = testPath.TempFile;
                var filename = Path.ChangeExtension(file.FullName, Extension);
                ILock fileLock = new FileLock(file);
                if (await fileLock.TryAcquire(TimeSpan.FromHours(1)))
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
        }

        [Fact]
        public async void Many()
        {
            const int i = 100;
            var result = true;
            for(var j = 0; j < i; j++)
            {
                using (var testPath = new TestPath())
                using (var fileLock = new FileLock(testPath.TempFile))
                {
                    if (!await fileLock.TryAcquire(TimeSpan.FromHours(1)) &&
                        !File.Exists(Path.ChangeExtension(testPath.TempFile.FullName, Extension)))
                    {
                        result = false;
                        break;
                    }
                }
            }

            Assert.True(result);
        }

        [Fact]
        public async void GetTimeReturnsMinValueWithNoLock()
        {
            using (var testPath = new TestPath())
            using (var fileLock = new FileLock(testPath.TempFile))
            {
                var dateTime = await fileLock.GetReleaseDate();
                Assert.Equal(DateTime.MinValue, dateTime);
            }
        }

        [Fact]
        public async void GetTimeReturnsCurrentReleaseDate()
        {
            using (var testPath = new TestPath())
            using (var fileLock = new FileLock(testPath.TempFile))
            {
                await fileLock.TryAcquire(TimeSpan.FromHours(1));
                var dateTime = await fileLock.GetReleaseDate();
                Assert.NotEqual(DateTime.MaxValue, dateTime);
            }
        }
    }
}
