using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ResilientFileLock.Test
{
    [Collection(nameof(FileLockCollection))]
    public class AcquireBeforeReleased
    {
        [Theory]
        [InlineData(30)]
        [InlineData(50)]
        [InlineData(70)]
        [InlineData(100)]
        [InlineData(150)]
        public async void TryToAcquireLockBeforeItIsReleased(int lockMilliseconds)
        {
            var file = new FileInfo(FileLockTestPath.GetTempFileName());
            var lockSpan = TimeSpan.FromMilliseconds(lockMilliseconds);
            using (var firstLock = new FileLock(file))
            using (var secondLock = new FileLock(file))
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

    [Collection(nameof(FileLockCollection))]
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
            var file = new FileInfo(FileLockTestPath.GetTempFileName());
            var firstAcquireTask = await Helpers.AcquireLockAndReleaseAfterDelay(file, Helpers.OneMillisecond);
            using (var secondLock = new FileLock(file))
            {
                var secondFileLock = await secondLock.WithTimeout(timeout, Helpers.MinimumRetry).TryAcquire(lockSpan);
                Assert.True(secondFileLock);
            }

            Assert.True(firstAcquireTask);
        }
    }

    [Collection(nameof(FileLockCollection))]
    public class AcquireLockBeforeOfficialRelease
    {
        //Minimum time is 15ms. So the lockMilliseconds (x) should be x > 60ms, because if x/4 >= 15ms there is time
        //to try at least a second time without timing out, because of the timeout in this test set to x - 15ms
        //Besides x % 4 == 0, to make it divisible between 4.
        [Theory]
        [InlineData(64)]
        [InlineData(68)]
        [InlineData(72)]
        [InlineData(76)]
        [InlineData(80)]
        public async void TryToAcquireLockBeforeOfficialRelease(int delayMilliseconds)
        {
            var spanToRelease = TimeSpan.FromMilliseconds(delayMilliseconds);
            var lockSpan = TimeSpan.FromMilliseconds(delayMilliseconds * 2);
            var timeoutSpan = TimeSpan.FromMilliseconds(delayMilliseconds * 4);
            var file = new FileInfo(FileLockTestPath.GetTempFileName());

            var firstAcquireTask = Helpers.AcquireLockAndReleaseAfterDelay(file, lockSpan, spanToRelease);
            using (var secondLock = new FileLock(file))
            {
                var secondAcquireTask = secondLock
                    .WithTimeout(timeoutSpan, Helpers.MinimumRetry)
                    .TryAcquire(lockSpan);
                Assert.True(await secondAcquireTask);
            }

            Assert.True(await firstAcquireTask);
        }
    }

    [Collection(nameof(FileLockCollection))]
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
            var file = new FileInfo(FileLockTestPath.GetTempFileName());
            var fileLock = new FileLock(file);
            using (fileLock)
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
            ILock fileLock = new FileLock(file);
            if (!await fileLock.TryAcquire(lockSpan))
            {
                return false;
            }

            using (fileLock)
            {
                await Task.Delay(delaySpan);
            }

            return true;
        }
    }
}