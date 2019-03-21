using System;
using System.Threading;
using System.Threading.Tasks;

namespace ResilientFileLock
{
    /// <inheritdoc />
    /// <summary>
    ///     Interface of FileLock
    /// </summary>
    public interface ILock : IDisposable
    {
        /// <summary>
        ///     Specify the timeoutSpan for this lock to be acquired
        /// </summary>
        /// <param name="timeoutSpan">Amount of time that </param>
        /// <param name="retrySpan">Amount of time to wait between retries</param>
        /// <returns></returns>
        FileLock WithTimeout(TimeSpan timeoutSpan, TimeSpan retrySpan);

        FileLock WithDisposalTimeout(TimeSpan disposeTimeout);

        /// <summary>
        ///     Extend lock by certain amount of time
        /// </summary>
        /// <param name="lockTime">How much time add to lock</param>
        Task AddTime(TimeSpan lockTime);

        /// <summary>
        ///     Get current lock release date
        /// </summary>
        /// <returns>Estimated date when lock gets released. DateTime.MaxValue if no lock exists.</returns>
        Task<DateTime> GetReleaseDate();

        /// <summary>
        ///     Acquire lock.
        /// </summary>
        /// <param name="lockTime">Amount of time after that lock is released</param>
        /// <param name="refreshContinuously">Optional specification if FileLock should automatically refresh lock.</param>
        /// <param name="cancellationToken">Optional mechanism to cancel getting the lock</param>
        /// <returns>File lock. False if lock already exists.</returns>
        Task<bool> TryAcquire(TimeSpan lockTime,
            bool refreshContinuously = false,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}