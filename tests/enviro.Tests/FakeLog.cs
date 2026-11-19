using enviro.Services;

namespace enviro.Tests;

/// <summary>
/// An <see cref="IAppLog"/> that records everything in memory: tests must never write the real
/// log. <see cref="Infos"/> holds just the audit entries, so a test can assert on them alone.
/// </summary>
internal sealed class FakeLog : IAppLog
{
    public string FilePath => string.Empty;

    public List<string> Entries { get; } = [];

    public List<string> Infos { get; } = [];

    public void Error(string message, Exception? exception = null) => Entries.Add(message);

    public void Info(string message)
    {
        Entries.Add(message);
        Infos.Add(message);
    }
}
