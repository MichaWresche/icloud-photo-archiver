using NetArchTest.Rules;

namespace PhotoArchiver.Architecture.Tests;

/// <summary>
/// Enforces the Onion dependency rule on compiled types: dependencies point inwards only.
/// </summary>
public sealed class TypeDependencyTests
{
    public static TheoryData<string> InfrastructureProjects => new(Layers.Infrastructure);

    [Fact]
    public void Domain_CompiledTypes_DependOnNoOtherProject()
    {
        AssertNoDependency(Layers.Domain, [Layers.Application, Layers.Web, .. Layers.Infrastructure]);
    }

    [Fact]
    public void Domain_CompiledTypes_DependOnNoFrameworkBeyondBaseLibrary()
    {
        AssertNoDependency(Layers.Domain, ["Microsoft.AspNetCore", "Microsoft.Extensions", "System.Net.Http"]);
    }

    [Fact]
    public void Application_CompiledTypes_DependNeitherOnInfrastructureNorOnWeb()
    {
        AssertNoDependency(Layers.Application, [Layers.Web, .. Layers.Infrastructure]);
    }

    [Theory]
    [MemberData(nameof(InfrastructureProjects))]
    public void Infrastructure_CompiledTypes_DependNeitherOnOtherInfrastructureNorOnWeb(string project)
    {
        string[] forbidden = [Layers.Web, .. Layers.Infrastructure.Where(other => other != project)];

        AssertNoDependency(project, forbidden);
    }

    private static void AssertNoDependency(string project, string[] forbidden)
    {
        var result = Types.InAssembly(Layers.AssemblyOf(project))
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"{project} must not depend on {string.Join(", ", forbidden)}. Violating types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
