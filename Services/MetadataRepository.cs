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
    /// Copyright notice of the program, falling back to the author when the assembly has none
    /// </summary>
    public string Copyright { get; init; }

    /// <summary>
    /// Version of the program in string
    /// </summary>
    public Version Version { get; init; }

    /// <summary>
    /// Link to the current repository on a hosting serices
    /// </summary>
    public string RepositoryLink { get; init; }

    /// <summary>
    /// Link to the repository's issue tracker
    /// </summary>
    public string IssuesLink { get; init; }

    /// <summary>
    /// Link to the license the program is distributed under
    /// </summary>
    public string LicenseLink { get; init; }

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
        Copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright is { Length: > 0 } copyright
            ? copyright
            : Author;
        Version = new Version(GetVersion(assembly));

        RepositoryLink = $"https://github.com/{Author}/{Title}";
        IssuesLink = $"{RepositoryLink}/issues";
        LicenseLink = "https://www.gnu.org/licenses/gpl-3.0";
        APILink = $"https://api.github.com/repos/{Author}/{Title}/releases/latest";
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
