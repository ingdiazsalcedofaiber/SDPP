namespace SDPP.Signature.Application.Ports;

/// <summary>
/// Fail-closed authorization for FieldType.LegalApprovalStamp — only one specific, configured
/// recipient email may ever fill this field (gerencia.legal@clinaltec.com.co today). Checked both
/// when a field is added (AddFieldCommand, early UX feedback) and — the actually-enforced point —
/// when a recipient submits it (CompleteRecipientSigningCommand), so the restriction can never be
/// bypassed by calling the API directly, only by changing the deployment's own configuration.
/// </summary>
public interface ILegalApprovalStampPolicy
{
    bool IsAuthorized(string email);
}
