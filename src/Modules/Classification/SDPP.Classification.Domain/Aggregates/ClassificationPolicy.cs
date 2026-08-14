using SDPP.BuildingBlocks.Domain;
using SDPP.Classification.Domain.Enums;

namespace SDPP.Classification.Domain.Aggregates;

public enum PolicyScope { Global, Area, Project }

public sealed class PolicyRule : Entity<Guid>
{
    public ClassificationLevel? ConditionClassification { get; private set; }
    public string? ConditionOperationType { get; private set; }
    public string? ConditionAreaEquals { get; private set; }

    /// <summary>Matches InspectionResult/Document.Category (e.g. "HistoriaClinica") — the business
    /// category asserted by LabelEngine, distinct from ConditionOperationType which already plays
    /// the role of "destination format" (PdfToWord = DOCX, etc.) for rules like "Historia Clínica
    /// no puede convertirse a DOCX".</summary>
    public string? ConditionCategory { get; private set; }

    public PolicyEffect Effect { get; private set; }
    public int Priority { get; private set; }

    private PolicyRule() { } // EF Core

    public static PolicyRule Create(
        ClassificationLevel? conditionClassification, string? conditionOperationType, string? conditionAreaEquals,
        PolicyEffect effect, int priority, string? conditionCategory = null) => new()
    {
        Id = Guid.NewGuid(),
        ConditionClassification = conditionClassification,
        ConditionOperationType = conditionOperationType,
        ConditionAreaEquals = conditionAreaEquals,
        ConditionCategory = conditionCategory,
        Effect = effect,
        Priority = priority,
    };

    /// <summary>True when every non-null condition on this rule matches the evaluation context.</summary>
    public bool Matches(ClassificationLevel classification, string operationType, string area, string? category)
    {
        if (ConditionClassification is not null && classification < ConditionClassification) return false;
        if (ConditionOperationType is not null && !ConditionOperationType.Equals(operationType, StringComparison.OrdinalIgnoreCase)) return false;
        if (ConditionAreaEquals is not null && !ConditionAreaEquals.Equals(area, StringComparison.OrdinalIgnoreCase)) return false;
        if (ConditionCategory is not null && !ConditionCategory.Equals(category, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}

/// <summary>
/// Configurable policy engine (Specification pattern) — see docs/05-security/dlp-engine.md §5.
/// Rules are evaluated in ascending Priority order; the first match wins (short-circuit), which
/// is what lets an Allow rule of higher priority (lower number) carve out an exception to a more
/// general Block/RequireApproval rule.
/// </summary>
public sealed class ClassificationPolicy : AggregateRoot<Guid>
{
    private readonly List<PolicyRule> _rules = [];

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public PolicyScope Scope { get; private set; }
    public string? ScopeValue { get; private set; }
    public bool Active { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid CreatedBy { get; private set; }

    public IReadOnlyList<PolicyRule> Rules => _rules.AsReadOnly();

    private ClassificationPolicy() { } // EF Core

    public static ClassificationPolicy Create(string name, string? description, PolicyScope scope, string? scopeValue, Guid createdBy) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = description,
        Scope = scope,
        ScopeValue = scopeValue,
        Active = true,
        Version = 1,
        CreatedAtUtc = DateTime.UtcNow,
        CreatedBy = createdBy,
    };

    public void AddRule(
        ClassificationLevel? conditionClassification, string? conditionOperationType, string? conditionAreaEquals,
        PolicyEffect effect, int priority, string? conditionCategory = null)
    {
        _rules.Add(PolicyRule.Create(conditionClassification, conditionOperationType, conditionAreaEquals, effect, priority, conditionCategory));
        Version++;
    }

    /// <summary>Null means this policy has no opinion on this request (no rule matched) — it must
    /// NOT be treated as an implicit Allow or an implicit fail-closed RequireApproval here. A
    /// policy scoped to one operation/area (e.g. "blockConfidentialToImage" only concerns
    /// PdfToImage) has nothing to say about a Compress request; deciding what happens when *every*
    /// active policy abstains is the aggregate's job (see EvaluatePolicyHandler's fail-closed
    /// default), not something each narrowly-scoped policy should assert independently.</summary>
    public PolicyEffect? Evaluate(ClassificationLevel classification, string operationType, string area, string? category = null)
    {
        var matched = _rules
            .Where(r => r.Matches(classification, operationType, area, category))
            .OrderBy(r => r.Priority)
            .FirstOrDefault();

        return matched?.Effect;
    }
}
