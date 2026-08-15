using FluentAssertions;
using SDPP.BuildingBlocks.Domain;
using SDPP.Identity.Domain.Aggregates;
using Xunit;

namespace SDPP.Identity.UnitTests;

public sealed class UserTests
{
    private static readonly Guid UsuarioRoleId = Guid.NewGuid();
    private static readonly Guid AdminRoleId = Guid.NewGuid();

    private static User CreateUser() =>
        User.Create("google-123", "Ana Pérez", "ana@clinaltec.com.co", null, "clinaltec.com.co", true, UsuarioRoleId);

    [Fact]
    public void Create_WithoutGoogleId_ThrowsDomainException()
    {
        var act = () => User.Create("", "Ana Pérez", "ana@clinaltec.com.co", null, "clinaltec.com.co", true, UsuarioRoleId);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_AssignsInitialRoleAndIsActiveByDefault()
    {
        var user = CreateUser();

        user.Active.Should().BeTrue();
        user.Roles.Should().ContainSingle(r => r.RoleId == UsuarioRoleId);
        user.MfaEnabled.Should().BeFalse();
    }

    [Fact]
    public void RecordLogin_RefreshesProfileSnapshotAndStampsLastLogin()
    {
        var user = CreateUser();

        user.RecordLogin("Ana P. Pérez", "https://example.com/photo.jpg", true);

        user.FullName.Should().Be("Ana P. Pérez");
        user.PhotoUrl.Should().Be("https://example.com/photo.jpg");
        user.LastLoginAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void GrantRoleIfMissing_CalledTwice_DoesNotDuplicateRole()
    {
        var user = CreateUser();

        user.GrantRoleIfMissing(AdminRoleId);
        user.GrantRoleIfMissing(AdminRoleId);

        user.Roles.Should().ContainSingle(r => r.RoleId == AdminRoleId);
    }

    [Fact]
    public void ReplaceRoles_ReplacesEntireSet()
    {
        var user = CreateUser();
        var newRoleId = Guid.NewGuid();

        user.ReplaceRoles([newRoleId], changedByUserId: Guid.NewGuid());

        user.Roles.Should().ContainSingle(r => r.RoleId == newRoleId);
        user.Roles.Should().NotContain(r => r.RoleId == UsuarioRoleId);
    }

    [Fact]
    public void ConfirmMfaEnrollment_EnablesMfaAndStoresBackupCodes()
    {
        var user = CreateUser();
        user.BeginMfaEnrollment("encrypted-secret");

        user.ConfirmMfaEnrollment(["hash1", "hash2"]);

        user.MfaEnabled.Should().BeTrue();
        user.MfaEnrolledAtUtc.Should().NotBeNull();
        user.MfaBackupCodes.Should().HaveCount(2);
    }

    [Fact]
    public void ConsumeBackupCodeIfValid_SameCodeTwice_OnlySucceedsOnce()
    {
        var user = CreateUser();
        user.BeginMfaEnrollment("encrypted-secret");
        user.ConfirmMfaEnrollment(["hash1"]);

        var firstAttempt = user.ConsumeBackupCodeIfValid("hash1");
        var secondAttempt = user.ConsumeBackupCodeIfValid("hash1");

        firstAttempt.Should().BeTrue();
        secondAttempt.Should().BeFalse("a backup code must be single-use");
    }

    [Fact]
    public void ConsumeBackupCodeIfValid_UnknownCode_ReturnsFalse()
    {
        var user = CreateUser();
        user.BeginMfaEnrollment("encrypted-secret");
        user.ConfirmMfaEnrollment(["hash1"]);

        var result = user.ConsumeBackupCodeIfValid("never-issued-hash");

        result.Should().BeFalse();
    }

    [Fact]
    public void SetActive_False_DeactivatesUser()
    {
        var user = CreateUser();

        user.SetActive(false);

        user.Active.Should().BeFalse();
    }
}
