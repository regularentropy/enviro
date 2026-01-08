using System.Text.Json;

namespace enviro.Services;

/// <summary>
/// Represents the response from a GitHub release check.
/// </summary>
/// <param name="Tag">The version tag of the release.</param>
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
    /// <returns>An UpdateResponse if a newer version is available, null if up to date.</returns>
    /// <exception cref="HttpRequestException">Thrown when the request fails or the repository is unavailable.</exception>
    Task<UpdateResponse?> CheckForUpdatesAsync();
}

/// <summary>
/// Service for checking for application updates via the GitHub API.
/// </summary>
/// <param name="_mr">The metadata repository containing version and API information.</param>
internal sealed class UpdateService (MetadataRepository _mr ) : IUpdateService
{
    /// <summary>
    /// Checks for available updates by querying the GitHub API for the latest release.
    /// Compares the latest version with the current application version.
    /// </summary>
    /// <returns>An UpdateResponse if a newer version is available, null if the application is up to date.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API request fails or returns an error status code.</exception>
    public async Task<UpdateResponse?> CheckForUpdatesAsync()
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd(_mr.Title);

        var response = await http.GetAsync(_mr.APILink).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Request failed: {response.ReasonPhrase}",
                null,
                response.StatusCode);
        }

        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        var tag = doc.RootElement.GetProperty("tag_name").GetString()!;
        if (new Version(tag) <= _mr.Version)
        {
            return null;
        }

        var name = doc.RootElement.GetProperty("name").GetString()!;
        var description = doc.RootElement.GetProperty("body").GetString()!;
        var url = doc.RootElement.GetProperty("html_url").GetString()!;

        return new UpdateResponse(tag, url, description);
    }
}
