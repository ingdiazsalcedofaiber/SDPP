using Microsoft.EntityFrameworkCore;
using SDPP.Classification.Domain.Aggregates;
using SDPP.Classification.Domain.Enums;
using SDPP.Classification.Infrastructure.Persistence;

namespace SDPP.Classification.Infrastructure.Seeding;

/// <summary>
/// Seeds the default DLP rule catalog (docs/05-security/dlp-engine.md §3) and the two worked
/// policy examples (docs/05-security/dlp-engine.md §5) on first run, so the platform is not
/// running with zero controls out of the box. Administrators are expected to review/extend this
/// catalog through the admin UI (UC-06) — this seeder never overwrites existing rows.
/// </summary>
public static class DefaultDataSeeder
{
    public static async Task SeedAsync(ClassificationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.DlpRules.AnyAsync(cancellationToken))
        {
            var creditCard = DlpRule.Create(
                "PII.CreditCard", DetectorType.Checksum,
                """{"algorithm":"Luhn","candidateRegex":"\\b(?:\\d[ -]*?){13,19}\\b"}""",
                FindingCategory.Financial, Severity.High);

            // Narrowed to require a nearby Spanish ID label (cédula/C.C./NIT/documento de
            // identidad) — a bare \d{6,10} matched *any* 6-10 digit run (invoice numbers, dates
            // like 20260726, phone extensions, zip/PO codes), so virtually every business
            // document tripped this rule and jumped straight to Confidencial (see
            // InspectionResult.Complete's severity→level mapping). Requiring the label first is
            // the same "reduce false positives at the pattern, not the severity" approach already
            // used by PII.CreditCard's Luhn check below.
            var nationalId = DlpRule.Create(
                "PII.NationalId", DetectorType.Regex,
                """{"pattern":"\\b(?:c[ée]dula(?:\\s+de\\s+ciudadan[ií]a)?|c\\.?c\\.?|nit|documento\\s+de\\s+identidad|n[uú]mero\\s+de\\s+identificaci[oó]n)\\b[^0-9]{0,15}\\d[\\d.,]{4,14}\\d\\b","searchFileName":false}""",
                FindingCategory.PII, Severity.Medium);

            // Info (not Low) — a signature-block email address is present in almost every
            // business document and isn't sensitive on its own; it still contributes to the
            // aggregate RiskScore/Labels, it just no longer single-handedly forces a minimum of
            // Privada on every document (Severity.Low was mapping straight to Privada for any
            // single email match).
            var email = DlpRule.Create(
                "PII.Email", DetectorType.Regex,
                """{"pattern":"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}","searchFileName":false}""",
                FindingCategory.PII, Severity.Info);

            var privateKey = DlpRule.Create(
                "Credentials.PrivateKeyPem", DetectorType.Regex,
                """{"pattern":"-----BEGIN (RSA |EC )?PRIVATE KEY-----","searchFileName":false}""",
                FindingCategory.Credentials, Severity.Critical);

            // Low (not Medium) — KeywordDetector counts a single incidental mention (e.g. one
            // "contrato") as a full match with no minimum count, and Medium mapped straight to
            // Confidencial, so any document that so much as referenced a contract was
            // over-classified. Low still flags it (→ Privada) without maxing it out from one word.
            var contractKeywords = DlpRule.Create(
                "Legal.ContractKeywords", DetectorType.Keyword,
                """{"keywords":["contrato","cláusula","las partes acuerdan","nda","acuerdo de confidencialidad"]}""",
                FindingCategory.Legal, Severity.Low);

            // Weight 175 so a single match already lands at the example score from the automatic
            // protection spec — BusinessCategory "HistoriaClinica" is what the category-based
            // block rule below (and ProtectionEngine's blockEditableConversion check) match on.
            var historiaClinica = DlpRule.Create(
                "Medical.HistoriaClinicaKeywords", DetectorType.Keyword,
                """{"keywords":["historia clínica","expediente médico","diagnóstico médico","antecedentes médicos","receta médica"]}""",
                FindingCategory.Medical, Severity.Critical,
                weight: 175,
                labels: ["MEDICO", "CONFIDENCIAL", "PII", "HISTORIA_CLINICA", "SALUD"],
                businessCategory: "HistoriaClinica");

            dbContext.DlpRules.AddRange(creditCard, nationalId, email, privateKey, contractKeywords, historiaClinica);
        }

        if (!await dbContext.ClassificationPolicies.AnyAsync(cancellationToken))
        {
            var systemUserId = Guid.Empty;

            var blockConfidentialToImage = ClassificationPolicy.Create(
                "Bloqueo de exportación de Confidenciales a imagen", null, PolicyScope.Global, null, systemUserId);
            blockConfidentialToImage.AddRule(
                Domain.Enums.ClassificationLevel.Confidencial, "PdfToImage", null, PolicyEffect.Block, priority: 10);

            var restrictedLegalOnly = ClassificationPolicy.Create(
                "Restringidos solo procesables por Legal (o requieren aprobación)", null, PolicyScope.Global, null, systemUserId);
            restrictedLegalOnly.AddRule(
                Domain.Enums.ClassificationLevel.Restringida, null, "Legal", PolicyEffect.Allow, priority: 5);
            restrictedLegalOnly.AddRule(
                Domain.Enums.ClassificationLevel.Restringida, null, null, PolicyEffect.RequireApproval, priority: 20);

            // Example from the automatic-protection spec: "SI Clasificación = Historia Clínica Y
            // Destino = DOCX ENTONCES Bloquear conversión". Only the "→ editable format" operations
            // the platform actually implements are listed — PDF-native operations (Compress,
            // Watermark, Rotate, ...) stay allowed, so a Historia Clínica document can still be
            // protected and handled as PDF, just never handed out as an editable Office file.
            var historiaClinicaNoEditable = ClassificationPolicy.Create(
                "Historia Clínica no puede convertirse a formatos editables", null, PolicyScope.Global, null, systemUserId);
            foreach (var editableFormatOperation in new[] { "PdfToWord", "PdfToExcel", "PdfToPpt" })
            {
                historiaClinicaNoEditable.AddRule(
                    null, editableFormatOperation, null, PolicyEffect.Block, priority: 1, conditionCategory: "HistoriaClinica");
            }

            dbContext.ClassificationPolicies.AddRange(blockConfidentialToImage, restrictedLegalOnly, historiaClinicaNoEditable);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
