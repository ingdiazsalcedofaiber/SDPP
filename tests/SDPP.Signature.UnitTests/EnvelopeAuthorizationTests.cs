using FluentAssertions;
using SDPP.BuildingBlocks.Application;
using SDPP.Signature.Application.UseCases;
using SDPP.Signature.Domain.Aggregates;
using SDPP.Signature.Domain.Enums;
using Xunit;

namespace SDPP.Signature.UnitTests;

internal sealed class FakeCurrentActor(Guid userId, params string[] roles) : ICurrentActor
{
    public Guid UserId { get; } = userId;
    public string FullName => "Test User";
    public string Email => "test@example.com";
    public string Domain => "example.com";
    public string Department => "";
    public IReadOnlyCollection<string> Roles { get; } = roles;
    public string? IpAddress => "127.0.0.1";
    public string? UserAgent => "xunit";
    public bool IsAuthenticated => true;
}

/// <summary>Covers the multi-tenant isolation rule live-verified in Fase H (cross-organization
/// access attack simulation) at the unit level too — even an Administrador of one organization must
/// never manage another organization's envelope.</summary>
public class EnvelopeAuthorizationTests
{
    private static SignatureEnvelope CreateEnvelope(Guid createdByUserId, Guid organizationId) =>
        SignatureEnvelope.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Contrato", null, createdByUserId,
            SigningMode.Sequential, dueDateUtc: null, new string('a', 64), organizationId);

    [Fact]
    public void Owner_in_the_same_organization_can_manage()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var envelope = CreateEnvelope(userId, orgId);
        var actor = new FakeCurrentActor(userId);

        EnvelopeAuthorization.CanManage(envelope, actor, orgId).Should().BeTrue();
    }

    // --- Ataque: administrador de OTRA organización no debe poder gestionar el sobre ---
    [Fact]
    public void Administrator_from_a_different_organization_cannot_manage()
    {
        var envelope = CreateEnvelope(Guid.NewGuid(), Guid.NewGuid());
        var adminFromAnotherOrg = new FakeCurrentActor(Guid.NewGuid(), "Administrador");

        EnvelopeAuthorization.CanManage(envelope, adminFromAnotherOrg, Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Administrator_in_the_same_organization_can_manage_even_if_not_the_creator()
    {
        var orgId = Guid.NewGuid();
        var envelope = CreateEnvelope(Guid.NewGuid(), orgId);
        var admin = new FakeCurrentActor(Guid.NewGuid(), "Administrador");

        EnvelopeAuthorization.CanManage(envelope, admin, orgId).Should().BeTrue();
    }

    [Fact]
    public void Non_owner_non_admin_cannot_manage_even_within_the_same_organization()
    {
        var orgId = Guid.NewGuid();
        var envelope = CreateEnvelope(Guid.NewGuid(), orgId);
        var randomUser = new FakeCurrentActor(Guid.NewGuid());

        EnvelopeAuthorization.CanManage(envelope, randomUser, orgId).Should().BeFalse();
    }
}
