using System;
using System.Threading;
using System.Threading.Tasks;

namespace ResilientFileLock
{
    internal static class Extensions
    {
        //From: https://stackoverflow.com/questions/4238345/asynchronously-wait-for-taskt-to-complete-with-timeout
        //From: https://stackoverflow.com/a/22078975
        public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan timeout)
        {
            using (var timeoutCts = new CancellationTokenSource())
            {
                var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCts.Token));
                if (completedTask != task)
                {
                    throw new TimeoutException("The operation has timed out.");
                }

                timeoutCts.Cancel();
                return await task; // Very important in order to propagate exceptions
            }
        }
    }
}