namespace enviro.Services;

/// <summary>
/// Single funnel for failures the app survives and for unhandled exceptions.
/// </summary>
internal interface IAppLog
{
    /// <summary>
    /// The log file; named to the user when an unhandled exception is reported.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Appends one entry, with the exception's details when there is one.
    /// </summary>
    /// <param name="message">What failed, in one sentence.</param>
    /// <param name="exception">The failure to attach, or null when there is nothing to attach.</param>
    void Error(string message, Exception? exception = null);
}

/// <summary>
/// Append-only text log at <c>%APPDATA%\Enviro\enviro.log</c> with one rotated backup and a size cap.
/// </summary>
/// <remarks>
/// It is written from crash handlers, so a logging failure is swallowed: the log must never
/// produce a second failure on a path that is already reporting one.
/// </remarks>
internal sealed class AppLog : IAppLog
{
    /// <summary>
    /// Size at which the current file becomes <c>enviro.log.old</c> before the next append.
    /// </summary>
    public const long MaxBytes = 256 * 1024;

    private readonly object _gate = new();

    /// <inheritdoc />
    public string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Enviro", "enviro.log");

    /// <inheritdoc />
    public void Error(string message, Exception? exception = null)
    {
        var entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}";

        if (exception is not null)
            entry += exception + Environment.NewLine;

        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                Rotate();
                Append(entry);
            }
        }
        catch (Exception)
        {
            // Logging must never break the caller, least of all the crash path.
        }
    }

    /// <summary>
    /// Moves the live file to <c>enviro.log.old</c> once it reaches <see cref="MaxBytes"/>.
    /// </summary>
    /// <remarks>
    /// A failed rotation is dropped on purpose: the entry is still written, so a locked or
    /// read-only backup costs the older entries rather than this one and every later one.
    /// </remarks>
    private void Rotate()
    {
        try
        {
            if (File.Exists(FilePath) && new FileInfo(FilePath).Length >= MaxBytes)
                File.Move(FilePath, FilePath + ".old", overwrite: true);
        }
        catch (Exception)
        {
            // The live file keeps growing past the threshold instead of the log stalling at it.
        }
    }

    /// <summary>
    /// Appends the entry, falling back to rewriting the file when the append itself fails.
    /// </summary>
    private void Append(string entry)
    {
        try
        {
            File.AppendAllText(FilePath, entry);
        }
        catch (Exception)
        {
            try
            {
                File.WriteAllText(FilePath, entry);
            }
            catch (Exception)
            {
                // Logging must never break the caller, least of all the crash path.
            }
        }
    }
}
