using System;
using System.Reflection;
using System.Text.Json;

namespace enviro.Services;

public interface IUpdateService
{
    Task<bool> CheckForUpdatesAsync();
}

internal sealed class UpdateService : IUpdateService
{
    private readonly ISoftwareMetadataService _sms;

    public UpdateService(ISoftwareMetadataService sms)
    {
        _sms = sms;
    }

    public async Task<bool> CheckForUpdatesAsync()
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd(_sms.Title);

        var response = await http.GetAsync(_sms.APILink).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Request failed: {response.ReasonPhrase}",
                null,
                response.StatusCode);
        }

        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        var tag = doc.RootElement.GetProperty("tag_name").GetString();
        var remoteVersion = new Version(tag);

        return remoteVersion > _sms.Version;
    }
}
