using System;
using System.Threading;
using System.Threading.Tasks;

namespace ResilientFileLock
{
    /// <inheritdoc />
    /// <summary>
    ///     From answer to StackOverflow question:
    ///     http://stackoverflow.com/questions/28305968/use-task-run-in-synchronous-method-to-avoid-deadlock-waiting-on-async-method/28307965#28307965
    ///     From pastebin: https://pastebin.com/feHtWPwX
    /// </summary>
    public class NoSynchronizationContextScope : IDisposable
    {
        private readonly SynchronizationContext _synchronizationContext;

        public NoSynchronizationContextScope()
        {
            _synchronizationContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
        }

        public void Dispose()
        {
            SynchronizationContext.SetSynchronizationContext(_synchronizationContext);
        }

        /// <summary>
        ///     Run async method with no SynchronizationContext
        /// </summary>
        /// <param name="asyncMethod"></param>
        public static void RunSynchronously(Func<Task> asyncMethod)
        {
            using (new NoSynchronizationContextScope())
            {
                asyncMethod().Wait();
            }
        }

        public static TResult RunSynchronously<TResult>(Func<Task<TResult>> asyncMethod)
        {
            using (new NoSynchronizationContextScope())
            {
                return asyncMethod().Result;
            }
        }
    }
}