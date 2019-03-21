using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ResilientFileLock
{
    /// <inheritdoc />
    /// <summary>
    ///     Providing file locks
    /// </summary>
    public class FileLock : ILock
    {
        private const string Extension = "lock";
        private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(10);
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly LockModel _content;
        private readonly string _path;
        private bool _disposed;
        private TimeSpan _retrySpan = TimeSpan.MinValue;
        private TimeSpan _timeout = TimeSpan.MinValue;

        /// <inheritdoc />
        /// <summary>
        ///     Creates reference to file lock on target file
        /// </summary>
        /// <param name="fileToLock">File we want lock</param>
        public FileLock(FileSystemInfo fileToLock) : this(fileToLock.FullName)
        {
        }

        /// <summary>
        ///     Creates reference to file lock on target file
        /// </summary>
        /// <param name="path">Path to file we want lock</param>
        public FileLock(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(path));
            }

            _path = GetLockFileName(path);
            _content = new LockModel(_path);
        }

        /// <inheritdoc />
        /// <summary>
        ///     Stop refreshing lock and delete lock when it makes sense. IOException is ignored for cases when file in use
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _disposed = true;
            NoSynchronizationContextScope.RunSynchronously(ReleaseLock);
        }

        /// <inheritdoc />
        public async Task AddTime(TimeSpan lockTime)
        {
            await _content.TrySetReleaseDate(await _content.GetReleaseDate() + lockTime);
        }

        public FileLock WithTimeout(TimeSpan timeoutSpan, TimeSpan retrySpan)
        {
            if (retrySpan >= timeoutSpan)
            {
                throw new ArgumentException("Retry span cannot be higher or equal than timeout span",
                    nameof(retrySpan));
            }

            _timeout = timeoutSpan;
            _retrySpan = retrySpan;
            return this;
        }

        /// <inheritdoc />
        public async Task<DateTime> GetReleaseDate()
        {
            return await _content.GetReleaseDate();
        }

        /// <inheritdoc />
        public async Task<bool> TryAcquire(TimeSpan lockTime,
            bool refreshContinuously = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                return await TryAcquireLock(lockTime, refreshContinuously, cancellationToken);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> TryAcquireLock(TimeSpan lockTime,
            bool refreshContinuously = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(FileLock));
            }

            if (_timeout == TimeSpan.MinValue)
            {
                return await TryAcquireWithoutTimeout(lockTime, refreshContinuously, cancellationToken);
            }

            RegisterFileLockTimeout(_timeout);
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                var isLockAcquired = await TryAcquireWithoutTimeout(lockTime, refreshContinuously, cancellationToken);
                if (isLockAcquired)
                {
                    return true;
                }

                await Task.Delay(_retrySpan, _cancellationTokenSource.Token);
            }

            return false;
        }

        private async Task<bool> TryAcquireWithoutTimeout(TimeSpan lockTime,
            bool refreshContinuously = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            RegisterCancellationToken(cancellationToken);
            if (lockTime <= TimeSpan.Zero || _cancellationTokenSource.IsCancellationRequested)
            {
                return false;
            }

            if (File.Exists(_path) && !await _content.CanModify())
            {
                return false;
            }

            var utcReleaseDate = DateTime.UtcNow + lockTime;
            if (!await _content.TrySetReleaseDate(utcReleaseDate))
            {
                return false;
            }

            if (refreshContinuously)
            {
                ContinuousRefreshTask(lockTime);
            }

            return true;
        }

        private void RegisterFileLockTimeout(TimeSpan timeoutSpan)
        {
            var cancellationTokenSource = new CancellationTokenSource(timeoutSpan);
            var timeoutToken = cancellationTokenSource.Token;
            RegisterCancellationToken(timeoutToken, () => cancellationTokenSource.Dispose());
        }

        private void RegisterCancellationToken(CancellationToken cancellationToken, Action action = null)
        {
            cancellationToken.Register(() =>
            {
                if (!_disposed)
                {
                    _cancellationTokenSource?.Cancel();
                }
                action?.Invoke();
            });
        }

        private void ContinuousRefreshTask(TimeSpan lockTime)
        {
            var cancellationToken = _cancellationTokenSource.Token;
            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await AddTime(lockTime);
                    await Task.Delay(lockTime, cancellationToken);
                }
            }, cancellationToken);
        }

        /// <summary>
        ///     IOException is ignored as there is nothing to do if we cannot delete the lock file
        /// </summary>
        private async Task ReleaseLock()
        {
            try
            {
                if (await LockNotOwnedOrTimedOut())
                {
                    return;
                }

                File.Delete(_path);
            }
            catch (IOException)
            {
            }
        }

        private async Task<bool> LockNotOwnedOrTimedOut()
        {
            try
            {
                return !await IsLockInstanceOwned().TimeoutAfter(DisposeTimeout);
            }
            catch (TimeoutException)
            {
                return true;
            }
        }

        private async Task<bool> IsLockInstanceOwned()
        {
            return File.Exists(_path) && await _content.IsInstanceOwned();
        }

        private static string GetLockFileName(string path)
        {
            return Path.ChangeExtension(path, Extension);
        }
    }
}