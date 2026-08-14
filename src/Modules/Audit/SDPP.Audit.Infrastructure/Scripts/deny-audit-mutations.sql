-- Run by the privileged deployment/migration account AFTER `dotnet ef database update`,
-- never by the runtime application login itself (a principal cannot be trusted to restrict its
-- own permissions — see docs/05-security/audit-and-traceability.md §3).
--
-- sdpp_audit_svc is the SQL login SDPP.Audit.Api connects with at runtime (configured via
-- ConnectionStrings:AuditDb). After this script runs, even a full SQL injection compromise of
-- the Audit API cannot alter or delete a single audit record — only insert and read.

DENY UPDATE ON audit.AuditRecords TO sdpp_audit_svc;
DENY DELETE ON audit.AuditRecords TO sdpp_audit_svc;

-- Belt-and-suspenders: reject any UPDATE/DELETE even from a principal that somehow still has the
-- permission (e.g. sysadmin bypassing DENY), and record the attempt as a security event.
GO
CREATE TRIGGER audit.TR_AuditRecords_PreventMutation
ON audit.AuditRecords
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    INSERT INTO audit.AuditRecords (OccurredAtUtc, EventType, ActorUserId, ActorFullName, ActorEmail, ActorDomain, PayloadJson, PreviousRecordHash, RecordHash, RecordGuid)
    SELECT
        SYSUTCDATETIME(), 'AuditChainMutationAttempt', ORIGINAL_LOGIN(), ORIGINAL_LOGIN(), ORIGINAL_LOGIN(), 'SYSTEM',
        '{"warning":"UPDATE or DELETE attempted against audit.AuditRecords and was rejected"}',
        (SELECT TOP 1 RecordHash FROM audit.AuditRecords ORDER BY Id DESC),
        '0000000000000000000000000000000000000000000000000000000000000000', -- recomputed by the app on next legitimate insert
        NEWID();

    RAISERROR('audit.AuditRecords es de solo inserción (append-only). UPDATE/DELETE rechazado.', 16, 1);
    ROLLBACK TRANSACTION;
END;
GO
