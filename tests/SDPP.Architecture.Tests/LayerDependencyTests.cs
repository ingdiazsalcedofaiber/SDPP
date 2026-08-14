using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace SDPP.Architecture.Tests;

/// <summary>
/// Enforces the Clean Architecture dependency rules described in
/// docs/01-architecture/solution-structure.md ("Reglas de dependencia") as executable tests
/// rather than just documentation — a PR that violates a layering rule fails CI, it doesn't rely
/// on a reviewer noticing. One explicit pair of tests per module (rather than a single
/// data-driven test) so each assertion runs against the correct module's assembly.
/// </summary>
public class LayerDependencyTests
{
    [Fact]
    public void Documents_Domain_has_no_dependency_on_Application_or_Infrastructure()
    {
        var result = Types.InAssembly(typeof(SDPP.Documents.Domain.Aggregates.DocumentInstance).Assembly)
            .That().ResideInNamespace("SDPP.Documents.Domain")
            .ShouldNot().HaveDependencyOnAny("SDPP.Documents.Application", "SDPP.Documents.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Documents_Application_has_no_dependency_on_Infrastructure()
    {
        var result = Types.InAssembly(typeof(SDPP.Documents.Application.DependencyInjection).Assembly)
            .That().ResideInNamespace("SDPP.Documents.Application")
            .ShouldNot().HaveDependencyOn("SDPP.Documents.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Classification_Domain_has_no_dependency_on_Application_or_Infrastructure()
    {
        var result = Types.InAssembly(typeof(SDPP.Classification.Domain.Aggregates.DlpRule).Assembly)
            .That().ResideInNamespace("SDPP.Classification.Domain")
            .ShouldNot().HaveDependencyOnAny("SDPP.Classification.Application", "SDPP.Classification.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Classification_Application_has_no_dependency_on_Infrastructure()
    {
        var result = Types.InAssembly(typeof(SDPP.Classification.Application.DependencyInjection).Assembly)
            .That().ResideInNamespace("SDPP.Classification.Application")
            .ShouldNot().HaveDependencyOn("SDPP.Classification.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Audit_Domain_has_no_dependency_on_Application_or_Infrastructure()
    {
        var result = Types.InAssembly(typeof(SDPP.Audit.Domain.Aggregates.AuditRecord).Assembly)
            .That().ResideInNamespace("SDPP.Audit.Domain")
            .ShouldNot().HaveDependencyOnAny("SDPP.Audit.Application", "SDPP.Audit.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Identity_Domain_has_no_dependency_on_Application_or_Infrastructure()
    {
        var result = Types.InAssembly(typeof(SDPP.Identity.Domain.Aggregates.User).Assembly)
            .That().ResideInNamespace("SDPP.Identity.Domain")
            .ShouldNot().HaveDependencyOnAny("SDPP.Identity.Application", "SDPP.Identity.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Identity_Application_has_no_dependency_on_Infrastructure()
    {
        var result = Types.InAssembly(typeof(SDPP.Identity.Application.DependencyInjection).Assembly)
            .That().ResideInNamespace("SDPP.Identity.Application")
            .ShouldNot().HaveDependencyOn("SDPP.Identity.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Signature_Domain_has_no_dependency_on_Application_or_Infrastructure()
    {
        var result = Types.InAssembly(typeof(SDPP.Signature.Domain.Aggregates.SignatureEnvelope).Assembly)
            .That().ResideInNamespace("SDPP.Signature.Domain")
            .ShouldNot().HaveDependencyOnAny("SDPP.Signature.Application", "SDPP.Signature.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Signature_Application_has_no_dependency_on_Infrastructure()
    {
        var result = Types.InAssembly(typeof(SDPP.Signature.Application.DependencyInjection).Assembly)
            .That().ResideInNamespace("SDPP.Signature.Application")
            .ShouldNot().HaveDependencyOn("SDPP.Signature.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Domain_assemblies_do_not_depend_on_MediatR_or_EntityFrameworkCore()
    {
        foreach (var domainType in new[]
                 {
                     typeof(SDPP.Documents.Domain.Aggregates.DocumentInstance),
                     typeof(SDPP.Classification.Domain.Aggregates.DlpRule),
                     typeof(SDPP.Audit.Domain.Aggregates.AuditRecord),
                     typeof(SDPP.Identity.Domain.Aggregates.User),
                     typeof(SDPP.Signature.Domain.Aggregates.SignatureEnvelope),
                 })
        {
            var result = Types.InAssembly(domainType.Assembly)
                .Should().NotHaveDependencyOnAny("MediatR", "Microsoft.EntityFrameworkCore")
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"{domainType.Assembly.GetName().Name}: {FailureMessage(result)}");
        }
    }

    private static string FailureMessage(TestResult result) =>
        "Tipos que violan la regla: " + string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []);
}
