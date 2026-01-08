using System.Reflection;

namespace enviro.Services;

/// <summary>
/// Repository containing metadata information about the application and its repository.
/// Provides information for update checking and about dialog.
/// </summary>
internal class MetadataRepository
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

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataRepository"/> class.
    /// Extracts metadata from the assembly attributes and constructs repository URLs.
    /// </summary>
    public MetadataRepository()
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

    /// <summary>
    /// Extracts the version string from the assembly in format "Major.Minor.Build".
    /// </summary>
    /// <param name="assembly">The assembly to extract the version from.</param>
    /// <returns>The version string in Major.Minor.Build format.</returns>
    private string GetVersion(Assembly assembly)
    {
        var ver = assembly.GetName().Version;
        return $"{ver.Major}.{ver.Minor}.{ver.Build}";
    }
}
