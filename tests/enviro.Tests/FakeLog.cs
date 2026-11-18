using enviro.Services;

namespace enviro.Tests;

/// <summary>
/// An <see cref="IAppLog"/> that records nothing: tests must never write the real log.
/// </summary>
internal sealed class FakeLog : IAppLog
{
    public string FilePath => string.Empty;

    public List<string> Entries { get; } = [];

    public void Error(string message, Exception? exception = null) => Entries.Add(message);
}
