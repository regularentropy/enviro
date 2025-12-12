using System.Text.Json;

namespace enviro.Services;

internal record UpdateResponse(string Tag, string LastReleaseURL, string ReleaseNotes);

internal interface IUpdateService
{
    Task<UpdateResponse?> CheckForUpdatesAsync();
}

internal sealed class UpdateService : IUpdateService
{
    private readonly MetadataRepository _mr;

    public UpdateService(MetadataRepository mr)
    {
        _mr = mr;
    }

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
