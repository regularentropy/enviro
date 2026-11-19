using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using enviro.Models;

namespace enviro.Services;

/// <summary>
/// A <c>.reg</c> registry export: the same exchange document as the JSON variable file, read
/// from and written to the format regedit produces.
/// </summary>
internal interface IRegFileService : IVariableFileService;

/// <summary>
/// Reads and writes <c>.reg</c> registry exports. Snapshot, entry review and comparison are
/// format-independent and delegated to the JSON service; only the file format itself differs.
/// IO and format failures come back as a message instead of an exception.
/// </summary>
/// <param name="log">Where read failures and skipped lines are recorded.</param>
/// <param name="variableFiles">The JSON service the format-independent operations delegate to.</param>
internal sealed class RegFileService(IAppLog log, IVariableFileService variableFiles) : IRegFileService
{
    private const string Header = "Windows Registry Editor Version 5.00";
    internal const string UserSection = @"HKEY_CURRENT_USER\Environment";
    internal const string MachineSection = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment";

    /// <summary>
    /// Upper bound on the size of a file <see cref="Read"/> will parse, mirroring
    /// <see cref="VariableFileService"/>: the length check reads metadata, so it is free, and a
    /// crafted multi-gigabyte export is refused there instead of being decoded until the heap
    /// runs out.
    /// </summary>
    private const long MaxFileBytes = 64 * 1024 * 1024;

    /// <summary>
    /// One value assignment: a quoted, escaped name, an equals sign and the encoded value.
    /// </summary>
    private static readonly Regex ValueLine = new("""^"((?:[^"\\]|\\.)*)"\s*=\s*(.*)$""");

    /// <inheritdoc />
    public VariableFile Snapshot(IEnvService service) => variableFiles.Snapshot(service);

    /// <inheritdoc />
    public IReadOnlyList<VariableFileEntry> Entries(VariableFile file) => variableFiles.Entries(file);

    /// <inheritdoc />
    public IReadOnlyList<VariableFileEntry> Compare(
        VariableFile file, IEnvService service, bool canImportMachine) =>
        variableFiles.Compare(file, service, canImportMachine);

    /// <inheritdoc />
    public (VariableFile? File, string? Error) Read(string path)
    {
        string text;

        try
        {
            using var stream = File.OpenRead(path);

            // Bound the read before decoding: a hostile file must not be read until the process
            // runs out of memory, so an oversized one is refused with the usual message.
            if (stream.Length > MaxFileBytes)
                return Fail(path, $"The file is larger than {MaxFileBytes / (1024 * 1024)} MB and was not read.");

            var bytes = new byte[stream.Length];
            for (var offset = 0; offset < bytes.Length;)
            {
                var read = stream.Read(bytes, offset, bytes.Length - offset);
                if (read == 0)
                    break;

                offset += read;
            }

            text = DetectEncoding(bytes).GetString(bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            log.Error($"Could not read variables from {path}.", ex);
            return (null, DescribeFailure(path, ex.Message));
        }

        var lines = text.Replace("\r\n", "\n").Split('\n');
        var headerIndex = Array.FindIndex(lines, line => line.TrimStart(' ', '\uFEFF', '\t').Trim().Length > 0);

        if (headerIndex < 0)
            return Fail(path, "The file is empty.");

        var header = lines[headerIndex].Trim().TrimStart('\uFEFF');

        if (header is not (Header or "REGEDIT4"))
            return Fail(path, "The file is not a registry export (missing the 'Windows Registry Editor Version 5.00' or 'REGEDIT4' header).");

        return (ParseLogicalLines(path, lines[(headerIndex + 1)..]), null);
    }

    /// <inheritdoc />
    public string? Save(string path, VariableFile file)
    {
        try
        {
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, Render(file), Encoding.Unicode);
            File.Move(tempPath, path, overwrite: true);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            log.Error($"Could not write variables to {path}.", ex);
            return DescribeFailure(path, ex.Message);
        }
    }

    /// <summary>
    /// Builds the export text: the version header, then one section per non-empty scope with its
    /// values in name order.
    /// </summary>
    /// <param name="file">The document to write.</param>
    /// <returns>The text to write, with CRLF line endings.</returns>
    private static string Render(VariableFile file)
    {
        var builder = new StringBuilder();
        builder.Append(Header).Append("\r\n\r\n");
        AppendSection(builder, UserSection, file.User);
        AppendSection(builder, MachineSection, file.Machine);
        return builder.ToString();
    }

    /// <summary>
    /// Appends one section: the header line, one value line per entry in name order, and the
    /// blank line that separates sections.
    /// </summary>
    /// <param name="builder">The text under construction.</param>
    /// <param name="section">The registry key the scope maps to.</param>
    /// <param name="entries">The scope's values, or null when the section is absent.</param>
    private static void AppendSection(StringBuilder builder, string section, Dictionary<string, string>? entries)
    {
        if (entries is null || entries.Count == 0)
            return;

        builder.Append('[').Append(section).Append("]\r\n");

        foreach (var (name, value) in entries.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            builder.Append(ValueLineText(name, value)).Append("\r\n");

        builder.Append("\r\n");
    }

    /// <summary>
    /// Encodes one value line. Plain values become REG_SZ; a value holding '%' or a control
    /// character becomes UTF-16LE <c>hex(2)</c> — a '%' would be expanded away on import and
    /// control characters cannot round-trip in a REG_SZ export, and regedit uses hex(2) itself
    /// for expandable strings (percent variables must stay expandable, e.g. the machine PATH).
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The variable value.</param>
    /// <returns>The line as regedit would hold it.</returns>
    private static string ValueLineText(string name, string value)
    {
        var nameText = $"\"{Escape(name)}\"";

        if (!value.Contains('%') && !value.Any(IsControl))
            return $"{nameText}=\"{Escape(value)}\"";

        var bytes = Encoding.Unicode.GetBytes(value).Concat([(byte)0, (byte)0]).ToArray();
        var builder = new StringBuilder(nameText).Append("=hex(2):");

        // regedit wraps long hex lines at 24 bytes with a trailing backslash continuation.
        for (var offset = 0; offset < bytes.Length; offset += 24)
        {
            var pairs = bytes.Skip(offset).Take(24)
                .Select(b => b.ToString("x2", CultureInfo.InvariantCulture));

            if (offset > 0)
                builder.Append("    ");

            builder.Append(string.Join(",", pairs));

            if (offset + 24 < bytes.Length)
                builder.Append(",\\\r\n");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Parses the file body below the version header, never failing the whole file on one bad
    /// line: headers switch the current scope, and each value is decoded on its own, with an
    /// unsupported or malformed line skipped and logged.
    /// </summary>
    /// <param name="path">The file that was read, for the log.</param>
    /// <param name="physicalLines">The physical lines below the version header.</param>
    /// <returns>The document the file holds.</returns>
    private VariableFile ParseLogicalLines(string path, string[] physicalLines)
    {
        var user = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var machine = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        EnvironmentalVariableType? scope = null;

        foreach (var line in ContinuationsJoined(physicalLines))
        {
            if (line.Trim().Length == 0)
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var key = Section(line);
                scope = string.Equals(key, UserSection, StringComparison.OrdinalIgnoreCase) ? EnvironmentalVariableType.User
                    : string.Equals(key, MachineSection, StringComparison.OrdinalIgnoreCase) ? EnvironmentalVariableType.Machine
                    : null;
                continue;
            }

            var match = ValueLine.Match(line);
            if (!match.Success)
            {
                log.Error($"Could not read variables from {path}: skipped an unrecognised line ({line}).");
                continue;
            }

            var name = Unescape(match.Groups[1].Value);
            var body = match.Groups[2].Value.TrimEnd();
            var target = scope switch
            {
                EnvironmentalVariableType.User => user,
                EnvironmentalVariableType.Machine => machine,
                _ => null,
            };

            // shortcut: delete directives and non-string value types are skipped on .reg import, upgrade if delete-on-import is needed.
            if (body == "-")
            {
                log.Error($"Could not read variables from {path}: skipped the deletion directive for '{name}'.");
                continue;
            }

            if (body.Length >= 2 && body[0] == '"' && body[^1] == '"')
            {
                if (target is not null)
                    target[name] = Unescape(body[1..^1]);

                continue;
            }

            if (body.StartsWith("hex(2):", StringComparison.OrdinalIgnoreCase))
            {
                var value = DecodeHex2(body["hex(2):".Length..]);
                if (value is not null)
                {
                    if (target is not null)
                        target[name] = value;
                }
                else
                    log.Error($"Could not read variables from {path}: skipped a malformed hex(2) value for '{name}'.");

                continue;
            }

            if (body.StartsWith("dword:", StringComparison.OrdinalIgnoreCase)
                || body.StartsWith("hex:", StringComparison.OrdinalIgnoreCase)
                || body.StartsWith("hex(", StringComparison.OrdinalIgnoreCase))
            {
                log.Error($"Could not read variables from {path}: skipped the unsupported value type of '{name}'.");
                continue;
            }

            log.Error($"Could not read variables from {path}: skipped the malformed value of '{name}'.");
        }

        return new VariableFile
        {
            Version = VariableFile.CurrentVersion,
            User = user.Count > 0 ? user : null,
            Machine = machine.Count > 0 ? machine : null,
        };
    }

    /// <summary>
    /// Decodes one hex(2) line: the body after the marker, decoded as UTF-16LE with one trailing
    /// NUL character trimmed.
    /// </summary>
    /// <param name="body">The byte list as regedit holds it, e.g. "25,00,50,00".</param>
    /// <returns>The value, or null when a byte cannot be parsed.</returns>
    private static string? DecodeHex2(string body)
    {
        var bytes = new List<byte>();

        foreach (var token in body.Split(','))
        {
            var pair = token.Trim();

            if (pair.Length is 0 or > 4
                || !int.TryParse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
                || value is < 0 or > 255)
                return null;

            bytes.Add((byte)value);
        }

        var text = Encoding.Unicode.GetString([.. bytes]);
        return text.EndsWith('\0') ? text[..^1] : text;
    }

    /// <summary>
    /// Joins continuation lines: a line ending in a backslash is continued on the next physical
    /// line, which is how regedit wraps long hex lines.
    /// </summary>
    /// <param name="physicalLines">The physical lines below the version header.</param>
    /// <returns>The logical lines to parse.</returns>
    private static IEnumerable<string> ContinuationsJoined(string[] physicalLines)
    {
        var pending = new StringBuilder();

        foreach (var physical in physicalLines)
        {
            if (physical.EndsWith('\\'))
            {
                pending.Append(physical, 0, physical.Length - 1);
                continue;
            }

            pending.Append(physical);
            yield return pending.ToString();
            pending.Clear();
        }

        // A truncated file may end on a continuation; the partial line still gets a chance to parse.
        if (pending.Length > 0)
            yield return pending.ToString();
    }

    /// <summary>
    /// Removes the surrounding brackets from one section header.
    /// </summary>
    /// <param name="line">The header line.</param>
    /// <returns>The registry key the line names.</returns>
    private static string Section(string line) => line[1..^1].Trim();

    /// <summary>
    /// Decodes the file's character encoding from its first bytes: regedit writes UTF-16LE or
    /// UTF-16BE with a BOM, while REGEDIT4 exports are ANSI, which UTF-8 decoding reads plainly.
    /// </summary>
    /// <param name="bytes">The raw file bytes.</param>
    /// <returns>The encoding to decode with.</returns>
    private static Encoding DetectEncoding(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE ? Encoding.Unicode
        : bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF ? Encoding.BigEndianUnicode
        : Encoding.UTF8;

    /// <summary>
    /// Unescapes a name or REG_SZ value: a backslash escapes a backslash or a double quote.
    /// </summary>
    /// <param name="text">The text as the file holds it.</param>
    /// <returns>The plain text.</returns>
    private static string Unescape(string text)
    {
        var builder = new StringBuilder(text.Length);

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\\' && index + 1 < text.Length && (text[index + 1] == '\\' || text[index + 1] == '"'))
            {
                builder.Append(text[index + 1]);
                index++;
            }
            else
                builder.Append(text[index]);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Escapes a name or REG_SZ value for the file: a backslash and a double quote are doubled.
    /// </summary>
    /// <param name="text">The plain text.</param>
    /// <returns>The text as the file holds it.</returns>
    private static string Escape(string text) =>
        text.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>
    /// Whether one character cannot be carried by a REG_SZ export: anything below the printable
    /// ASCII range, plus DEL.
    /// </summary>
    /// <param name="character">The character to test.</param>
    /// <returns>True when the character must be written as hex(2) instead.</returns>
    private static bool IsControl(char character) => character is < (char)0x20 or (char)0x7F;

    /// <summary>
    /// Records a format failure that has no exception behind it.
    /// </summary>
    /// <param name="path">The file that was read.</param>
    /// <param name="reason">The user-facing reason.</param>
    /// <returns>No document, and the message to show.</returns>
    private (VariableFile? File, string? Error) Fail(string path, string reason)
    {
        log.Error($"Could not read variables from {path}: {reason}");
        return (null, DescribeFailure(path, reason));
    }

    /// <summary>
    /// Builds the one error message the shell shows, naming the file.
    /// </summary>
    /// <param name="path">The file involved.</param>
    /// <param name="reason">Why the operation failed.</param>
    /// <returns>The user-facing message.</returns>
    private static string DescribeFailure(string path, string reason) =>
        $"The variables could not be processed:\n{path}\n\n{reason}";
}
