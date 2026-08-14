using Microsoft.EntityFrameworkCore;
using SDPP.Classification.Application.Ports;
using SDPP.Classification.Domain.Aggregates;

namespace SDPP.Classification.Infrastructure.Persistence;

public sealed class DlpRuleRepository(ClassificationDbContext dbContext) : IDlpRuleRepository
{
    public async Task<IReadOnlyList<DlpRule>> GetEnabledRulesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.DlpRules.Where(r => r.Enabled).ToListAsync(cancellationToken);

    public void Add(DlpRule rule) => dbContext.DlpRules.Add(rule);
}

public sealed class ClassificationPolicyRepository(ClassificationDbContext dbContext) : IClassificationPolicyRepository
{
    public async Task<IReadOnlyList<ClassificationPolicy>> GetActivePoliciesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.ClassificationPolicies.Include(p => p.Rules).Where(p => p.Active).ToListAsync(cancellationToken);

    public void Add(ClassificationPolicy policy) => dbContext.ClassificationPolicies.Add(policy);
}

public sealed class InspectionResultRepository(ClassificationDbContext dbContext) : IInspectionResultRepository
{
    public Task<InspectionResult?> GetByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        dbContext.InspectionResults
            .Include(i => i.Findings)
            .Where(i => i.DocumentId == documentId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(InspectionResult inspectionResult) => dbContext.InspectionResults.Add(inspectionResult);
}

public sealed class DocumentIntegrityRecordRepository(ClassificationDbContext dbContext) : IDocumentIntegrityRecordRepository
{
    public Task<DocumentIntegrityRecord?> GetByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        dbContext.DocumentIntegrityRecords.FirstOrDefaultAsync(r => r.DocumentId == documentId, cancellationToken);

    public Task<DocumentIntegrityRecord?> GetByHashAsync(string sha256Hash, CancellationToken cancellationToken = default) =>
        dbContext.DocumentIntegrityRecords.FirstOrDefaultAsync(r => r.Sha256Hash.Value == sha256Hash, cancellationToken);

    public async Task<IReadOnlyList<DocumentIntegrityRecord>> GetByDocumentIdsAsync(
        IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken = default) =>
        await dbContext.DocumentIntegrityRecords.Where(r => documentIds.Contains(r.DocumentId)).ToListAsync(cancellationToken);

    public Task<DocumentIntegrityRecord?> GetLatestByDocumentVersionIdAsync(Guid documentVersionId, CancellationToken cancellationToken = default) =>
        dbContext.DocumentIntegrityRecords
            .Where(r => r.DocumentVersionId == documentVersionId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(DocumentIntegrityRecord record) => dbContext.DocumentIntegrityRecords.Add(record);
}

public sealed class DocumentVersionFingerprintRepository(ClassificationDbContext dbContext) : IDocumentVersionFingerprintRepository
{
    public Task<DocumentVersionFingerprint?> GetByDocumentVersionIdAsync(Guid documentVersionId, CancellationToken cancellationToken = default) =>
        dbContext.DocumentVersionFingerprints.FirstOrDefaultAsync(f => f.DocumentVersionId == documentVersionId, cancellationToken);

    public Task<DocumentVersionFingerprint?> GetLatestByContentFingerprintAsync(string contentFingerprint, CancellationToken cancellationToken = default) =>
        dbContext.DocumentVersionFingerprints
            .Where(f => f.ContentFingerprint == contentFingerprint)
            .OrderByDescending(f => f.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(DocumentVersionFingerprint fingerprint) => dbContext.DocumentVersionFingerprints.Add(fingerprint);
}
