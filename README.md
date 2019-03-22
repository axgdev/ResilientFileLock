# ResilientFileLock

.NET Standard library providing exclusive lock on file. Additional functionality to acquire this lock with a timeout. Based on: [Xabe.Filelock](https://github.com/tomaszzmuda/Xabe.FileLock). Highly recommended to check out that library first, this library adds timeout possibilities as well as some additional mechanism to ensure it works for different processes in different computers.

## Using

Install the [ResilientFileLock NuGet package](https://www.nuget.org/packages/ResilientFileLock) via nuget:

	PM> Install-Package ResilientFileLock
	
Creating file lock:

```csharp
ILock fileLock = new FileLock(file);
fileLock.TryAcquire(TimeSpan.FromSeconds(15));
```
This will create lock file with extension ".lock" in the same directory. Example: "/tmp/data.txt" -> "/tmp/data.lock".

Last two parameters are optional, the second last defines if lock should be automatically refreshing before expired. The last one is to provide cancellation.

If file already has lock file, and it time has not expired, method returns false.

## Recommended using

```csharp
using (fileLock = new FileLock(file))
{
    if (await fileLock.TryAcquire(TimeSpan.FromSeconds(15)))
    {
        // file operations here
    }
    else 
    {
        // if lock not acquired
    }
}
```
## Timeout functionality

Similarly to the code above we can await the FileLock until timeout. Note that refreshing the lock could complicate things:

```csharp
using (fileLock = new FileLock(file))
{
    if (await fileLock
            .WithTimeout(timeoutSpan: TimeSpan.FromSeconds(30), retrySpan: TimeSpan.FromSeconds(1))
            .TryAcquire(TimeSpan.FromSeconds(15)))
    {
        // file operations here
    }
    else 
    {
        // things to do if timeout happens
    }
}
```
## License

ResilientFileLock is licensed under MIT - see [License](LICENSE.md) for details.