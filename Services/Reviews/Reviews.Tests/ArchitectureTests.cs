using System.Reflection;
using System.Xml.Linq;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Tests;

/// <summary>
/// Правило залежностей Clean Architecture: внутрішні шари не знають про зовнішні.
/// Перевіряються і посилання між проєктами (*.csproj), і реальні залежності збірок.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Domain.Entities.Review).Assembly;
    private static readonly Assembly Application = typeof(Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly WebApi = typeof(WebApi.DependencyInjection).Assembly;

    public static TheoryData<string, string[]> ExpectedProjectReferences => new()
    {
        { "Reviews.Domain", [] },
        { "Reviews.Application", ["Reviews.Domain"] },
        { "Reviews.Infrastructure", ["Reviews.Application", "Reviews.Domain"] },
        { "Reviews.WebApi", ["Reviews.Application", "Reviews.Infrastructure"] }
    };

    [Theory]
    [MemberData(nameof(ExpectedProjectReferences))]
    public void Project_references_follow_the_dependency_rule(string project, string[] expected)
    {
        var references = XDocument.Load(FindProjectFile(project))
            .Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value))
            .Order()
            .ToArray();

        Assert.Equal(expected, references);
    }

    [Fact]
    public void Domain_depends_only_on_bson_types()
    {
        var references = ReferencedAssemblies(Domain);

        Assert.DoesNotContain(references, name => name.StartsWith("Reviews.", StringComparison.Ordinal));
        Assert.DoesNotContain("MongoDB.Driver", references);
        Assert.Contains("MongoDB.Bson", references);
    }

    [Fact]
    public void Application_does_not_know_infrastructure_or_the_mongodb_driver()
    {
        var references = ReferencedAssemblies(Application);

        Assert.DoesNotContain("Reviews.Infrastructure", references);
        Assert.DoesNotContain("Reviews.WebApi", references);
        Assert.DoesNotContain("MongoDB.Driver", references);
    }

    [Fact]
    public void Infrastructure_does_not_know_the_web_layer()
        => Assert.DoesNotContain("Reviews.WebApi", ReferencedAssemblies(Infrastructure));

    [Fact]
    public void No_controller_or_handler_touches_IMongoCollection_or_the_context()
    {
        var types = WebApi.GetTypes().Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .Concat(Application.GetTypes().Where(type => type.GetInterfaces().Any(i =>
                i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)
                                    || i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)))))
            .ToList();

        Assert.NotEmpty(types);
        foreach (var type in types)
        {
            var dependencies = type.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)
                .Concat(type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Select(f => f.FieldType));
            Assert.DoesNotContain(dependencies, dependency =>
                dependency == typeof(MongoDbContext)
                || dependency.IsGenericType && dependency.GetGenericTypeDefinition() == typeof(IMongoCollection<>));
        }
    }

    [Fact]
    public void Domain_classes_carry_no_bson_attributes()
    {
        var attributes = Domain.GetTypes()
            .SelectMany(type => type.GetCustomAttributes(inherit: false)
                .Concat(type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .SelectMany(member => member.GetCustomAttributes(inherit: false))))
            .Select(attribute => attribute.GetType().Namespace ?? "");

        Assert.DoesNotContain(attributes, ns => ns.StartsWith("MongoDB", StringComparison.Ordinal));
    }

    private static string[] ReferencedAssemblies(Assembly assembly)
        => assembly.GetReferencedAssemblies().Select(name => name.Name!).ToArray();

    private static string FindProjectFile(string project)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, project, $"{project}.csproj")))
            directory = directory.Parent;

        return directory is null
            ? throw new FileNotFoundException($"{project}.csproj not found above {AppContext.BaseDirectory}")
            : Path.Combine(directory.FullName, project, $"{project}.csproj");
    }
}
