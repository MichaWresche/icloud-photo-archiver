using System.Xml.Linq;

namespace PhotoArchiver.Architecture.Tests;

/// <summary>
/// Enforces the Onion dependency rule on the project files. Unlike the type checks, this also
/// catches a forbidden reference before any type uses it.
/// </summary>
public sealed class ProjectReferenceTests
{
    private static readonly Dictionary<string, string[]> AllowedReferences = new()
    {
        [Layers.Domain] = [],
        [Layers.Application] = [Layers.Domain],
        ["PhotoArchiver.Infrastructure.ICloud"] = [Layers.Application],
        ["PhotoArchiver.Infrastructure.Storage"] = [Layers.Application],
        ["PhotoArchiver.Infrastructure.Media"] = [Layers.Application],
        ["PhotoArchiver.Infrastructure.Security"] = [Layers.Application],
        [Layers.Web] = [Layers.Application, .. Layers.Infrastructure],
    };

    public static TheoryData<string> SourceProjects => new(SourceProjectFiles().Select(Path.GetFileNameWithoutExtension)!);

    [Fact]
    public void Every_source_project_has_a_defined_layer()
    {
        var unknown = SourceProjectFiles()
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !AllowedReferences.ContainsKey(name!))
            .ToList();

        Assert.True(unknown.Count == 0, $"Add these projects to the layer rules: {string.Join(", ", unknown)}");
    }

    [Theory]
    [MemberData(nameof(SourceProjects))]
    public void Source_project_references_only_allowed_projects(string project)
    {
        var path = SourceProjectFiles().Single(file => Path.GetFileNameWithoutExtension(file) == project);
        var references = XDocument.Load(path)
            .Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(element.Attribute("Include")!.Value.Replace('\\', '/')))
            .ToList();

        var forbidden = references.Except(AllowedReferences[project]).ToList();

        Assert.True(forbidden.Count == 0, $"{project} must not reference {string.Join(", ", forbidden)}.");
    }

    private static string[] SourceProjectFiles() =>
        Directory.GetFiles(Path.Combine(RepositoryRoot(), "src"), "*.csproj", SearchOption.AllDirectories);

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PhotoArchiver.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("PhotoArchiver.slnx not found above " + AppContext.BaseDirectory);
    }
}
