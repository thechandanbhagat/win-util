using System.Threading;

namespace WinUtil.Services;

internal sealed class SingleInstanceGate : IDisposable
{
    private readonly Mutex mutex;

    internal SingleInstanceGate(string mutexName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutexName);
        mutex = new Mutex(false, mutexName, out var createdNew);
        IsPrimaryInstance = createdNew;
    }

    internal bool IsPrimaryInstance { get; }

    public void Dispose() => mutex.Dispose();
}
