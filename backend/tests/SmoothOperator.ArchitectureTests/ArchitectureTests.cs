using System.Reflection;
using NetArchTest.Rules;

namespace SmoothOperator.ArchitectureTests;

/// <summary>
/// Enforces Clean Architecture dependency rules at build/test time.
/// Failures here mean a layer boundary has been violated.
/// </summary>
public class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly =
        typeof(SmoothOperator.Domain.Models.Connection).Assembly;

    private static readonly Assembly ApplicationAssembly =
        typeof(SmoothOperator.Application.Features.Auth.Commands.LoginCommand).Assembly;

    private static readonly Assembly InfrastructureAssembly =
        typeof(SmoothOperator.Infrastructure.Data.AppDbContext).Assembly;

    private static readonly Assembly ApiAssembly =
        typeof(SmoothOperator.Api.Controllers.AuthController).Assembly;

    // ── Domain ──────────────────────────────────────────────────────────────

    [Fact]
    public void Domain_Should_Not_DependOn_Application()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("SmoothOperator.Application")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Domain must not reference Application. Violating types: {Violations(result)}");
    }

    [Fact]
    public void Domain_Should_Not_DependOn_Infrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("SmoothOperator.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Domain must not reference Infrastructure. Violating types: {Violations(result)}");
    }

    [Fact]
    public void Domain_Should_Not_DependOn_Api()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("SmoothOperator.Api")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Domain must not reference Api. Violating types: {Violations(result)}");
    }

    // ── Application ─────────────────────────────────────────────────────────

    [Fact]
    public void Application_Should_Not_DependOn_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("SmoothOperator.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Application must not reference Infrastructure. Violating types: {Violations(result)}");
    }

    [Fact]
    public void Application_Should_Not_DependOn_Api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("SmoothOperator.Api")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Application must not reference Api. Violating types: {Violations(result)}");
    }

    // ── Infrastructure ───────────────────────────────────────────────────────

    [Fact]
    public void Infrastructure_Should_Not_DependOn_Api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn("SmoothOperator.Api")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Infrastructure must not reference Api. Violating types: {Violations(result)}");
    }

    // ── Api (controller) conventions ────────────────────────────────────────

    [Fact]
    public void Controllers_Should_Reside_In_ControllersNamespace()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .Inherit(typeof(Microsoft.AspNetCore.Mvc.ControllerBase))
            .Should()
            .ResideInNamespace("SmoothOperator.Api.Controllers")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Controllers must reside in SmoothOperator.Api.Controllers. Violating types: {Violations(result)}");
    }

    // ── Application: handler naming & placement ──────────────────────────────

    [Fact]
    public void CommandHandlers_Should_Reside_In_Features_Namespace()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ImplementInterface(typeof(MediatR.IRequestHandler<,>))
            .Should()
            .ResideInNamespace("SmoothOperator.Application.Features")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"MediatR handlers must live under Application.Features. Violating types: {Violations(result)}");
    }

    [Fact]
    public void Application_Interfaces_Should_Reside_In_Interfaces_Namespace()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .AreInterfaces()
            .Should()
            .ResideInNamespace("SmoothOperator.Application")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"All Application interfaces must reside in SmoothOperator.Application.*. Violating types: {Violations(result)}");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string Violations(TestResult result) =>
        result.FailingTypeNames is { Count: > 0 }
            ? string.Join(", ", result.FailingTypeNames)
            : "(none)";
}
