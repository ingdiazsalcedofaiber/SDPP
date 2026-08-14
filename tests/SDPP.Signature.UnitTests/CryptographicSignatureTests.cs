using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Aggregates;
using SDPP.Signature.Domain.Enums;
using SDPP.Signature.Infrastructure.Security;
using Xunit;

namespace SDPP.Signature.UnitTests;

/// <summary>Minimal IConfiguration fake — DatabaseKeyManagementService only ever calls the string
/// indexer (see ResolveEncryptionKey), so nothing else needs a real implementation.</summary>
internal sealed class FakeConfiguration(string keyEncryptionKey) : IConfiguration
{
    public string? this[string key]
    {
        get => key == "Signature:KeyEncryptionKey" ? keyEncryptionKey : null;
        set => throw new NotSupportedException();
    }

    public IEnumerable<IConfigurationSection> GetChildren() => throw new NotSupportedException();
    public IChangeToken GetReloadToken() => throw new NotSupportedException();
    public IConfigurationSection GetSection(string key) => throw new NotSupportedException();
}

/// <summary>In-memory ISignatureKeyRepository — enough to exercise DatabaseKeyManagementService's
/// real ECDSA generate/sign/verify logic without a database.</summary>
internal sealed class InMemorySignatureKeyRepository : ISignatureKeyRepository
{
    private readonly Dictionary<Guid, SignatureKey> _keys = [];

    public Task<SignatureKey?> GetActiveAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_keys.Values.FirstOrDefault(k => k.Status == SignatureKeyStatus.Active));

    public Task<SignatureKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_keys.GetValueOrDefault(id));

    public void Add(SignatureKey key) => _keys[key.Id] = key;
}

/// <summary>
/// Exercises the REAL ECDSA P-256 signing/verification path (DatabaseKeyManagementService), not a
/// reimplementation — same class the live handler uses. Covers the explicit spec requirement:
/// "verificación ECDSA (firmar y alterar un byte → debe fallar)".
/// </summary>
public class CryptographicSignatureTests
{
    private static DatabaseKeyManagementService CreateService() =>
        new(new InMemorySignatureKeyRepository(), new FakeConfiguration("sdpp-unit-test-key-0123456789abc")); // exactly 32 bytes (AES-256)

    [Fact]
    public async Task SignAsync_produces_a_signature_that_verifies_against_the_same_payload()
    {
        var service = CreateService();
        var payload = "envelopeId|recipientId|documentHash"u8.ToArray();

        var result = await service.SignAsync(payload);
        var isValid = await service.VerifyAsync(payload, result.SignatureBase64, result.KeyId);

        isValid.Should().BeTrue();
        result.Algorithm.Should().Be("ES256");
    }

    // --- Ataque: alterar un solo byte del payload firmado invalida la verificación ---
    [Fact]
    public async Task VerifyAsync_rejects_a_tampered_payload()
    {
        var service = CreateService();
        var payload = "envelopeId|recipientId|documentHash"u8.ToArray();
        var result = await service.SignAsync(payload);

        var tampered = (byte[])payload.Clone();
        tampered[0] ^= 0xFF;
        var isValid = await service.VerifyAsync(tampered, result.SignatureBase64, result.KeyId);

        isValid.Should().BeFalse();
    }

    // --- Ataque: alterar un solo byte de la firma invalida la verificación ---
    [Fact]
    public async Task VerifyAsync_rejects_a_tampered_signature()
    {
        var service = CreateService();
        var payload = "envelopeId|recipientId|documentHash"u8.ToArray();
        var result = await service.SignAsync(payload);

        var signatureBytes = Convert.FromBase64String(result.SignatureBase64);
        signatureBytes[0] ^= 0xFF;
        var tamperedSignatureBase64 = Convert.ToBase64String(signatureBytes);
        var isValid = await service.VerifyAsync(payload, tamperedSignatureBase64, result.KeyId);

        isValid.Should().BeFalse();
    }

    // --- Ataque: reutilizar una firma con la clave pública incorrecta (id de clave inexistente) ---
    [Fact]
    public async Task VerifyAsync_returns_false_for_an_unknown_key_id()
    {
        var service = CreateService();
        var payload = "envelopeId|recipientId|documentHash"u8.ToArray();
        var result = await service.SignAsync(payload);

        var isValid = await service.VerifyAsync(payload, result.SignatureBase64, Guid.NewGuid());

        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task SignAsync_reuses_the_same_active_key_across_multiple_calls()
    {
        var service = CreateService();

        var first = await service.SignAsync("payload-1"u8.ToArray());
        var second = await service.SignAsync("payload-2"u8.ToArray());

        first.KeyId.Should().Be(second.KeyId, "un único par de claves activo debe reutilizarse, no regenerarse por cada firma");
    }
}
