using System.Reflection;

namespace PhotoArchiver.Architecture.Tests;

/// <summary>
/// The Onion layers of the solution (docs/architecture/05_building_block_view.md, §5.1).
/// </summary>
internal static class Layers
{
    public const string Domain = "PhotoArchiver.Domain";
    public const string Application = "PhotoArchiver.Application";
    public const string Web = "PhotoArchiver.Web";
    public const string InfrastructurePrefix = "PhotoArchiver.Infrastructure.";

    public static readonly string[] Infrastructure =
    [
        "PhotoArchiver.Infrastructure.ICloud",
        "PhotoArchiver.Infrastructure.Storage",
        "PhotoArchiver.Infrastructure.Media",
        "PhotoArchiver.Infrastructure.Security",
    ];

    public static Assembly AssemblyOf(string project) => project switch
    {
        Domain => typeof(PhotoArchiver.Domain.AssemblyMarker).Assembly,
        Application => typeof(PhotoArchiver.Application.AssemblyMarker).Assembly,
        "PhotoArchiver.Infrastructure.ICloud" => typeof(PhotoArchiver.Infrastructure.ICloud.AssemblyMarker).Assembly,
        "PhotoArchiver.Infrastructure.Storage" => typeof(PhotoArchiver.Infrastructure.Storage.AssemblyMarker).Assembly,
        "PhotoArchiver.Infrastructure.Media" => typeof(PhotoArchiver.Infrastructure.Media.AssemblyMarker).Assembly,
        "PhotoArchiver.Infrastructure.Security" => typeof(PhotoArchiver.Infrastructure.Security.AssemblyMarker).Assembly,
        Web => typeof(PhotoArchiver.Web.Components.App).Assembly,
        _ => throw new ArgumentOutOfRangeException(nameof(project), project, "Unknown project."),
    };
}
