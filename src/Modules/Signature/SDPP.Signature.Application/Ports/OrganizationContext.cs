namespace SDPP.Signature.Application.Ports;

/// <summary>
/// Resolves the "current organization" every envelope is created under and every envelope query is
/// scoped by — the isolation boundary for THIS module only (see the module's multitenancy phase:
/// scoped to Signature, ready for the rest of the platform to adopt later). Identity has no concept
/// of organizations yet, so today's only implementation (DefaultOrganizationContextProvider) always
/// returns the same fixed id — real, backend-enforced isolation logic already exists everywhere it's
/// needed, it's just resolving a constant for now. Swapping in a real per-user organization (e.g.
/// once Identity gains a JWT claim for it) means changing ONLY this one implementation.
/// </summary>
public interface IOrganizationContextProvider
{
    Guid GetCurrentOrganizationId();
}
