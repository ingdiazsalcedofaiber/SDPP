namespace SDPP.BuildingBlocks.Domain;

/// <summary>Raised when a domain invariant is violated. Never caught silently by application code.</summary>
public class DomainException(string message) : Exception(message);
