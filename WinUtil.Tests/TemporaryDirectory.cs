namespace WinUtil.Tests;

internal sealed class TemporaryDirectory : IDisposable
{
    private readonly string rootPath = Path.Combine(Path.GetTempPath(), "WinUtil.Tests", Guid.NewGuid().ToString("N"));

    internal TemporaryDirectory()
    {
        Directory.CreateDirectory(rootPath);
    }

    internal string PathFor(params string[] segments) => Path.Combine([rootPath, .. segments]);

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }
}
