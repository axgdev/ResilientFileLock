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
        public async Task AcquireSecondLock()
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
        public async Task AcquireSecondLockAfterRelease()
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

        private static async Task AfterLockModelArrange(Action<FileLock, string, TimeSpan> actAssert)
        {
            await AfterLockModelArrangeAsync((fileLock, lockFilename, lockTime) =>
            {
                actAssert(fileLock, lockFilename, lockTime);
                return Task.CompletedTask;
            });
        }

        private static async Task AfterLockModelArrangeAsync(Func<FileLock, string, TimeSpan, Task> actAssert)
        {
            using (var testPath = new TestPath())
            using (var fileLock = new FileLock(testPath.TempFile))
            {
                var lockFileName = Path.ChangeExtension(testPath.TempFile.FullName, Extension);
                var lockTime = TimeSpan.FromHours(1);
                await fileLock.TryAcquire(lockTime);
                await actAssert(fileLock, lockFileName, lockTime);
            }
        }

        [Fact]
        public async Task LockModelIsCreated()
        {
            await AfterLockModelArrange((fileLock, lockFilename, lockTime) =>
            {
                Assert.True(File.Exists(lockFilename));
            });
        }

        [Fact]
        public async Task LockModelIsCorrect()
        {
            await AfterLockModelArrange((fileLock, lockFilename, lockTime) =>
            {
                var lockContentLines = File.ReadAllLines(lockFilename);
                Assert.True(Guid.TryParse(lockContentLines[0], out _));
                Assert.True(long.TryParse(lockContentLines[1], out var ticks));
            });
        }

        [Fact]
        public async Task BasicLock()
        {
            await AfterLockModelArrange((fileLock, lockFilename, lockTime) =>
            {
                var lockContentLines = File.ReadAllLines(lockFilename);
                var ticks = long.Parse(lockContentLines[1]);
                var fileDate = new DateTime(ticks);
                Assert.True(fileDate - DateTime.UtcNow - lockTime < _timeVariable);
            });
        }


        [Fact]
        public async Task BasicLockAddTime()
        {
            await AfterLockModelArrangeAsync(async (fileLock, lockFilename, lockTime) =>
            {
                await fileLock.AddTime(lockTime);
                var lockContentLines = File.ReadAllLines(lockFilename);
                var ticks = long.Parse(lockContentLines[1]);
                var fileDate = new DateTime(ticks);
                Assert.True(fileDate - DateTime.UtcNow - lockTime - lockTime < _timeVariable);
            });
        }

        [Fact]
        public async Task DisposeTest()
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
        public async Task Many()
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
        public async Task GetTimeReturnsMinValueWithNoLock()
        {
            using (var testPath = new TestPath())
            using (var fileLock = new FileLock(testPath.TempFile))
            {
                var dateTime = await fileLock.GetReleaseDate();
                Assert.Equal(DateTime.MinValue, dateTime);
            }
        }

        [Fact]
        public async Task GetTimeReturnsCurrentReleaseDate()
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
