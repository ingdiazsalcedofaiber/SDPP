using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SDPP.Classification.Domain.Aggregates;

namespace SDPP.Classification.Infrastructure.Persistence;

public sealed class InspectionResultConfiguration : IEntityTypeConfiguration<InspectionResult>
{
    public void Configure(EntityTypeBuilder<InspectionResult> builder)
    {
        builder.ToTable("InspectionResults");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.DocumentId).IsRequired();
        builder.Property(i => i.TriggeredBy).HasConversion<byte>();
        builder.Property(i => i.SuggestedClassification).HasConversion<byte>();
        builder.Property(i => i.FinalClassification).HasConversion<byte>();
        builder.Property(i => i.Status).HasConversion<byte>();
        builder.Property(i => i.RiskScore);
        builder.Property(i => i.BusinessCategory).HasMaxLength(100);
        builder.Property(i => i.Labels)
            .HasConversion(EntityConfigurationHelpers.CsvConverter, EntityConfigurationHelpers.ListComparer)
            .HasMaxLength(500);
        builder.HasIndex(i => i.DocumentId);

        builder.Metadata.FindNavigation(nameof(InspectionResult.Findings))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(i => i.Findings).WithOne().HasForeignKey("InspectionResultId").OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class FindingConfiguration : IEntityTypeConfiguration<Finding>
{
    public void Configure(EntityTypeBuilder<Finding> builder)
    {
        builder.ToTable("Findings");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.DetectorId).HasMaxLength(100).IsRequired();
        builder.Property(f => f.Category).HasConversion<string>().HasMaxLength(30);
        builder.Property(f => f.Severity).HasConversion<byte>();
        builder.Property(f => f.Location).HasMaxLength(200);
        builder.Property(f => f.RuleVersion).HasMaxLength(20);
        builder.Property(f => f.Weight);
        builder.Property(f => f.BusinessCategory).HasMaxLength(100);
        builder.Property(f => f.Labels)
            .HasConversion(EntityConfigurationHelpers.CsvConverter, EntityConfigurationHelpers.ListComparer)
            .HasMaxLength(500);
    }
}

public sealed class ClassificationPolicyConfiguration : IEntityTypeConfiguration<ClassificationPolicy>
{
    public void Configure(EntityTypeBuilder<ClassificationPolicy> builder)
    {
        builder.ToTable("ClassificationPolicies");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Scope).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.ScopeValue).HasMaxLength(200);

        builder.Metadata.FindNavigation(nameof(ClassificationPolicy.Rules))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Rules).WithOne().HasForeignKey("ClassificationPolicyId").OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PolicyRuleConfiguration : IEntityTypeConfiguration<PolicyRule>
{
    public void Configure(EntityTypeBuilder<PolicyRule> builder)
    {
        builder.ToTable("PolicyRules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.ConditionClassification).HasConversion<byte?>();
        builder.Property(r => r.ConditionOperationType).HasMaxLength(30);
        builder.Property(r => r.ConditionAreaEquals).HasMaxLength(200);
        builder.Property(r => r.ConditionCategory).HasMaxLength(100);
        builder.Property(r => r.Effect).HasConversion<string>().HasMaxLength(20);
    }
}

public sealed class DlpRuleConfiguration : IEntityTypeConfiguration<DlpRule>
{
    public void Configure(EntityTypeBuilder<DlpRule> builder)
    {
        builder.ToTable("DlpRules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.DetectorType).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.PatternOrConfigJson).IsRequired();
        builder.Property(r => r.Category).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.DefaultSeverity).HasConversion<byte>();
        builder.Property(r => r.Weight);
        builder.Property(r => r.BusinessCategory).HasMaxLength(100);
        builder.Property(r => r.Labels)
            .HasConversion(EntityConfigurationHelpers.CsvConverter, EntityConfigurationHelpers.ListComparer)
            .HasMaxLength(500);
    }
}

public sealed class DocumentIntegrityRecordConfiguration : IEntityTypeConfiguration<DocumentIntegrityRecord>
{
    public void Configure(EntityTypeBuilder<DocumentIntegrityRecord> builder)
    {
        builder.ToTable("DocumentIntegrityRecords");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.DocumentId).IsRequired();
        builder.Property(r => r.DocumentVersionId).IsRequired();
        builder.Property(r => r.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(r => r.IntegritySignature).HasMaxLength(200);
        builder.Property(r => r.CreatedAtUtc).IsRequired();
        builder.Property(r => r.UpdatedAtUtc).IsRequired();

        builder.Property(r => r.ProtectionsApplied)
            .HasConversion(EntityConfigurationHelpers.CsvConverter, EntityConfigurationHelpers.ListComparer)
            .HasMaxLength(500);

        builder.OwnsOne(r => r.Sha256Hash, hash =>
        {
            hash.Property(h => h.Value).HasColumnName("Sha256Hash").HasMaxLength(64).IsRequired();
            hash.HasIndex(h => h.Value);
        });

        builder.HasIndex(r => r.DocumentId).IsUnique();
        builder.HasIndex(r => r.DocumentVersionId);
    }
}

public sealed class DocumentVersionFingerprintConfiguration : IEntityTypeConfiguration<DocumentVersionFingerprint>
{
    public void Configure(EntityTypeBuilder<DocumentVersionFingerprint> builder)
    {
        builder.ToTable("DocumentVersionFingerprints");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.DocumentVersionId).IsRequired();
        builder.Property(f => f.LogicalDocumentId).IsRequired();
        builder.Property(f => f.ContentFingerprint).HasMaxLength(64);
        builder.Property(f => f.StructuralSignature).HasMaxLength(16);
        builder.Property(f => f.Classification).HasConversion<byte>().IsRequired();
        builder.Property(f => f.ClassificationSource).HasConversion<byte>().IsRequired();
        builder.Property(f => f.Category).HasMaxLength(100);
        builder.Property(f => f.CreatedAtUtc).IsRequired();
        builder.Property(f => f.UpdatedAtUtc).IsRequired();

        builder.Property(f => f.Labels)
            .HasConversion(EntityConfigurationHelpers.CsvConverter, EntityConfigurationHelpers.ListComparer)
            .HasMaxLength(500);

        builder.HasIndex(f => f.DocumentVersionId).IsUnique();
        builder.HasIndex(f => f.ContentFingerprint);
        builder.HasIndex(f => f.LogicalDocumentId);
    }
}

internal static class EntityConfigurationHelpers
{
    /// <summary>Shared comma-separated-string &lt;-&gt; IReadOnlyList&lt;string&gt; conversion for
    /// every simple string-list property in this module (Labels) — one converter definition so
    /// the exact splitting/joining behavior can't drift between DlpRule, Finding and
    /// InspectionResult.</summary>
    public static readonly Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<IReadOnlyList<string>, string> CsvConverter =
        new(
            v => string.Join(',', v),
            v => v.Split(',', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Without this, EF Core 9 warns that it has no way to tell whether a replaced
    /// IReadOnlyList&lt;string&gt; instance actually changed (structural vs. reference equality) —
    /// compares by content instead of by reference, and hashes accordingly.</summary>
    public static readonly ValueComparer<IReadOnlyList<string>> ListComparer = new(
        (a, b) => (a ?? Array.Empty<string>()).SequenceEqual(b ?? Array.Empty<string>()),
        v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
        v => v.ToList());
}
