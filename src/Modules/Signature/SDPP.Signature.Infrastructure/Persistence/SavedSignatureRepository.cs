using Microsoft.EntityFrameworkCore;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Aggregates;

namespace SDPP.Signature.Infrastructure.Persistence;

public sealed class SavedSignatureRepository(SignatureDbContext dbContext) : ISavedSignatureRepository
{
    public Task<SavedSignature?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.SavedSignatures.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SavedSignature>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.SavedSignatures.Where(s => s.UserId == userId).OrderByDescending(s => s.CreatedAtUtc).ToListAsync(cancellationToken);

    public void Add(SavedSignature signature) => dbContext.SavedSignatures.Add(signature);

    public void Remove(SavedSignature signature) => dbContext.SavedSignatures.Remove(signature);
}
