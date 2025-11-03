using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;

namespace enviro.Services;

internal interface ISoftwareMetadataService
{
    string APIHost { get; init; }
    string APILink { get; init; }
    string Author { get; init; }
    string RepositoryHost { get; init; }
    string RepositoryLink { get; init; }
    string Title { get; init; }
    Version Version { get; init; }
}

internal class SoftwareMetadataService : ISoftwareMetadataService
{
    /// <summary>
    /// Name of the program
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// Author of the program
    /// </summary>
    public string Author { get; init; }

    /// <summary>
    /// Version of the program in string
    /// </summary>
    public Version Version { get; init; }

    /// <summary>
    /// Host of the repository (e.g github.com)
    /// </summary>
    public string RepositoryHost { get; init; }

    /// <summary>
    /// Link to the current repository on a hosting serices
    /// </summary>
    public string RepositoryLink { get; init; }

    /// <summary>
    /// Link to the latest release from a Github Repository
    /// </summary>
    public string RepositoryLastRelease { get; init; }

    /// <summary>
    /// Name of the host (e.g api.github.com)
    /// </summary>
    public string APIHost { get; init; }

    /// <summary>
    /// Link to an API link
    /// </summary>
    public string APILink { get; init; }


    public SoftwareMetadataService()
    {
        var assembly = Assembly.GetExecutingAssembly();

        Title = assembly.GetName().Name;
        Author = assembly.GetCustomAttribute<AssemblyCompanyAttribute>().Company;
        Version = new Version(GetVersion(assembly));

        RepositoryHost = "github.com";
        RepositoryLink = $"https://{RepositoryHost}/{Author}/{Title}";
        RepositoryLastRelease = $"https://{RepositoryHost}/{Author}/{Title}/release/latest";

        APIHost = $"https://api.{RepositoryHost}";
        APILink = $"{APIHost}/repos/{Author}/{Title}/releases/latest";
    }

    private string GetVersion(Assembly assembly)
    {
        var ver = assembly.GetName().Version;
        return $"{ver.Major}.{ver.Minor}.{ver.Build}";
    }
}
