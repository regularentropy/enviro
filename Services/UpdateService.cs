using System.Text.Json;

namespace enviro.Services;

/// <summary>
/// Represents the response from a GitHub release check.
/// </summary>
/// <param name="Tag">The version tag of the release, without a leading "v".</param>
/// <param name="LastReleaseURL">The URL to the release page.</param>
/// <param name="ReleaseNotes">The release notes/description.</param>
internal record UpdateResponse(string Tag, string LastReleaseURL, string ReleaseNotes);

/// <summary>
/// Service interface for checking for application updates.
/// </summary>
internal interface IUpdateService
{
    /// <summary>
    /// Checks for available updates from the GitHub repository.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>An UpdateResponse if a newer version is available, null if up to date or the release data is unusable.</returns>
    /// <exception cref="HttpRequestException">Thrown when the request fails or the repository is unavailable.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the request is cancelled or times out.</exception>
    Task<UpdateResponse?> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for checking for application updates via the GitHub API.
/// </summary>
/// <param name="_mr">The metadata repository containing version and API information.</param>
/// <param name="_log">Where an unusable release response is recorded.</param>
internal sealed class UpdateService(MetadataRepository _mr, IAppLog _log) : IUpdateService
{
    /// <summary>Upper bound for the whole request, so an unresponsive host cannot hang the check.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Checks for available updates by querying the GitHub API for the latest release.
    /// Compares the latest version with the current application version.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>An UpdateResponse if a newer version is available, null if the application is up to date.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API request fails or returns an error status code.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the request is cancelled or times out.</exception>
    public async Task<UpdateResponse?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient { Timeout = RequestTimeout };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(_mr.Title);

        var response = await http.GetAsync(_mr.APILink, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Request failed: {response.ReasonPhrase}",
                null,
                response.StatusCode);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        // A malformed body means "cannot tell", not a crash: treat it as no update.
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            _log.Error("Update check returned a response that is not valid JSON.", ex);
            return null;
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("tag_name", out var tagElement))
            {
                return null;
            }

            var tag = NormalizeTag(tagElement.GetString());
            if (!Version.TryParse(tag, out var version) || version <= _mr.Version)
            {
                return null;
            }

            return new UpdateResponse(
                tag,
                ReadString(doc.RootElement, "html_url"),
                ReadString(doc.RootElement, "body"));
        }
    }

    /// <summary>
    /// Trims a release tag and strips one leading "v"/"V", so "v0.9.0" parses as a version.
    /// </summary>
    private static string NormalizeTag(string? rawTag)
    {
        var tag = (rawTag ?? string.Empty).Trim();
        return tag.Length > 0 && tag[0] is 'v' or 'V' ? tag[1..] : tag;
    }

    /// <summary>
    /// Reads a string property, treating a missing or null property as an empty string.
    /// </summary>
    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;
}
