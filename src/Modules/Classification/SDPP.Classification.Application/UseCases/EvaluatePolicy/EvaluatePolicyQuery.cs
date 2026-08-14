using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Classification.Application.Ports;
using SDPP.Classification.Domain.Enums;

namespace SDPP.Classification.Application.UseCases.EvaluatePolicy;

public sealed record EvaluatePolicyResult(string Effect, string? Reason);

/// <summary>Implements the two worked examples from docs/05-security/dlp-engine.md §5, plus the
/// category-based blocking example (e.g. "Historia Clínica no puede convertirse a DOCX") from the
/// automatic protection feature.</summary>
public sealed record EvaluatePolicyQuery(
    ClassificationLevel Classification, string OperationType, string Area, string? Category = null) : IQuery<EvaluatePolicyResult>;

public sealed class EvaluatePolicyHandler(IClassificationPolicyRepository policyRepository)
    : IRequestHandler<EvaluatePolicyQuery, Result<EvaluatePolicyResult>>
{
    public async Task<Result<EvaluatePolicyResult>> Handle(EvaluatePolicyQuery request, CancellationToken cancellationToken)
    {
        var policies = await policyRepository.GetActivePoliciesAsync(cancellationToken);

        // Across multiple active policies, the most restrictive effect wins (Block > RequireApproval > Allow) —
        // fail-safe combination consistent with docs/05-security/dlp-engine.md §8. A policy that
        // abstains (null — no rule matched, see ClassificationPolicy.Evaluate) contributes nothing;
        // it must never be conflated with an explicit Allow or forced into a fail-closed
        // RequireApproval just because it happens to be scoped to a different operation/area than
        // this request. The fail-closed default belongs here, at the aggregate level — it applies
        // only when EVERY active policy had nothing to say, not per individual narrowly-scoped policy.
        var effects = policies
            .Select(p => p.Evaluate(request.Classification, request.OperationType, request.Area, request.Category))
            .Where(e => e is not null)
            .Select(e => e!.Value)
            .ToList();

        var effect = effects.Contains(PolicyEffect.Block)
            ? PolicyEffect.Block
            : effects.Contains(PolicyEffect.RequireApproval)
                ? PolicyEffect.RequireApproval
                : effects.Count == 0 && request.Classification >= ClassificationLevel.Confidencial
                    ? PolicyEffect.RequireApproval
                    : PolicyEffect.Allow;

        var reason = effect switch
        {
            PolicyEffect.Block when request.Category is not null =>
                $"La política de clasificación bloquea la conversión de documentos de categoría '{request.Category}' a '{request.OperationType}'.",
            PolicyEffect.Block => "La política de clasificación bloquea esta combinación de clasificación/operación/área.",
            PolicyEffect.RequireApproval => "Esta operación requiere aprobación de un Supervisor del área declarada.",
            _ => null,
        };

        return Result.Success(new EvaluatePolicyResult(effect.ToString(), reason));
    }
}
