using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ResilientFileLock.Test
{
    public class AcquireBeforeReleased
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public async void TryToAcquireLockBeforeItIsReleased(int lockSeconds)
        {
            var lockSpan = TimeSpan.FromSeconds(lockSeconds);
            using (var testPath = new TestPath())
            using (var firstLock = new FileLock(testPath.TempFile))
            using (var secondLock = new FileLock(testPath.TempFile))
            {
                var firstAcquireTask = firstLock.TryAcquire(lockSpan);
                var secondAcquireTask = await secondLock
                    .WithTimeout(Helpers.MinimumTimeout, Helpers.MinimumRetry)
                    .TryAcquire(lockSpan);
                Assert.False(secondAcquireTask);
                Assert.True(await firstAcquireTask);
            }
        }
    }

    public class AcquireAfterReleased
    {
        [Theory]
        [InlineData(30)]
        [InlineData(50)]
        [InlineData(70)]
        [InlineData(100)]
        [InlineData(150)]
        public async void TryToAcquireLockAfterItIsReleased(int lockMilliseconds)
        {
            var lockSpan = TimeSpan.FromMilliseconds(lockMilliseconds);
            var timeout = TimeSpan.FromMilliseconds(lockMilliseconds * 10);
            
            using (var testPath = new TestPath())
            using (var secondLock = new FileLock(testPath.TempFile))
            {
                var firstAcquireTask = await Helpers.AcquireLockAndReleaseAfterDelay(testPath.TempFile, Helpers.OneMillisecond);
                var secondFileLock = await secondLock.WithTimeout(timeout, Helpers.MinimumRetry).TryAcquire(lockSpan);
                Assert.True(secondFileLock);
                Assert.True(firstAcquireTask);
            }
        }
    }

    public class AcquireLockBeforeOfficialRelease
    {
        [Theory]
        [InlineData(10)]
        [InlineData(20)]
        [InlineData(30)]
        [InlineData(40)]
        [InlineData(50)]
        public async void TryToAcquireLockBeforeOfficialRelease(int delayMilliseconds)
        {
            var spanToRelease = TimeSpan.FromMilliseconds(delayMilliseconds);
            var lockSpan = TimeSpan.FromMilliseconds(delayMilliseconds * 2);
            var timeoutSpan = TimeSpan.FromMilliseconds(delayMilliseconds * 4);

            using (var testPath = new TestPath())
            using (var secondLock = new FileLock(testPath.TempFile))
            {
                var firstAcquireTask = Helpers.AcquireLockAndReleaseAfterDelay(testPath.TempFile, lockSpan, spanToRelease);
                var secondAcquireTask = secondLock
                    .WithTimeout(timeoutSpan, Helpers.MinimumRetry)
                    .TryAcquire(lockSpan);
                Assert.True(await secondAcquireTask);
                Assert.True(await firstAcquireTask);
            }
        }
    }

    public class ShouldGetOutBeforeLockTime
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public async void LockShouldNotWaitTillTimeoutToBeAcquiredIfNotLocked(int lockSeconds)
        {
            var lockSpan = TimeSpan.FromSeconds(lockSeconds);
            var retrySpan = TimeSpan.FromSeconds(lockSeconds / 2.0);
            using (var testPath = new TestPath())
            using (var fileLock = new FileLock(testPath.TempFile))
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                await fileLock.WithTimeout(lockSpan, retrySpan).TryAcquire(lockSpan);
                stopwatch.Stop();
                Assert.True(stopwatch.ElapsedMilliseconds < lockSeconds * 1000 / 2.0);
            }
        }
    }

    public class Helpers
    {
        public static readonly TimeSpan MinimumTimeout = TimeSpan.FromMilliseconds(15);
        public static readonly TimeSpan MinimumRetry = TimeSpan.FromMilliseconds(1);
        public static readonly TimeSpan OneMillisecond = TimeSpan.FromMilliseconds(1);

        public static Task<bool> AcquireLockAndReleaseAfterDelay(FileInfo file, TimeSpan lockSpan)
        {
            return AcquireLockAndReleaseAfterDelay(file, lockSpan, lockSpan);
        }


        public static async Task<bool> AcquireLockAndReleaseAfterDelay(FileInfo file, TimeSpan lockSpan, TimeSpan delaySpan)
        {
            using (var fileLock = new FileLock(file))
            {
                if (!await fileLock.TryAcquire(lockSpan))
                {
                    return false;
                }

                await Task.Delay(delaySpan);
                return true;
            }
        }
    }
}