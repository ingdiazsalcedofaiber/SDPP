using Microsoft.EntityFrameworkCore;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Aggregates;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Infrastructure.Persistence;

public sealed class SignatureKeyRepository(SignatureDbContext dbContext) : ISignatureKeyRepository
{
    public Task<SignatureKey?> GetActiveAsync(CancellationToken cancellationToken = default) =>
        dbContext.SignatureKeys
            .Where(k => k.Status == SignatureKeyStatus.Active)
            .OrderByDescending(k => k.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<SignatureKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.SignatureKeys.FirstOrDefaultAsync(k => k.Id == id, cancellationToken);

    public void Add(SignatureKey key) => dbContext.SignatureKeys.Add(key);
}
