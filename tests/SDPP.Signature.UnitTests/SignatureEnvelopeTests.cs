using FluentAssertions;
using SDPP.BuildingBlocks.Domain;
using SDPP.Signature.Domain.Aggregates;
using SDPP.Signature.Domain.Enums;
using Xunit;

namespace SDPP.Signature.UnitTests;

public class SignatureEnvelopeTests
{
    private static SignatureEnvelope CreateDraft(SigningMode mode = SigningMode.Sequential) =>
        SignatureEnvelope.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Contrato de prueba", null,
            Guid.NewGuid(), mode, dueDateUtc: null, originalSha256Hash: new string('a', 64), Guid.NewGuid());

    private static EnvelopeRecipient AddSignerField(SignatureEnvelope envelope, string email, int order, out Guid fieldId)
    {
        var recipient = envelope.AddRecipient(email, email, order);
        var field = envelope.AddField(recipient.Id, FieldType.Signature, 1, 0.1, 0.1, 0.3, 0.1, required: true);
        fieldId = field.Id;
        return recipient;
    }

    // --- Ataque: firmar fuera de turno (orden secuencial) ---
    [Fact]
    public void RegisterSignature_out_of_sequential_turn_is_rejected()
    {
        var envelope = CreateDraft();
        var first = AddSignerField(envelope, "primero@example.com", 1, out var firstFieldId);
        var second = AddSignerField(envelope, "segundo@example.com", 2, out var secondFieldId);
        envelope.Send();
        envelope.RegisterConsent(second.Id, "1.2.3.4", "agent", "EmailOtp");

        var act = () => envelope.RegisterSignature(
            second.Id, [(secondFieldId, null, [1, 2, 3], SignatureMethodUsed.Uploaded)], "1.2.3.4", "agent", "EmailOtp");

        act.Should().Throw<DomainException>().WithMessage("*turno*");
    }

    // --- Ataque: firmar sin haber aceptado el consentimiento (fail-closed) ---
    [Fact]
    public void RegisterSignature_without_consent_is_rejected()
    {
        var envelope = CreateDraft();
        var recipient = AddSignerField(envelope, "firmante@example.com", 1, out var fieldId);
        envelope.Send();

        var act = () => envelope.RegisterSignature(
            recipient.Id, [(fieldId, null, [1, 2, 3], SignatureMethodUsed.Uploaded)], "1.2.3.4", "agent", "EmailOtp");

        act.Should().Throw<DomainException>().WithMessage("*consentimiento*");
    }

    // --- Ataque: modificar un campo/firmante después de enviar el sobre (ya no es Draft) ---
    [Fact]
    public void AddField_after_send_is_rejected()
    {
        var envelope = CreateDraft();
        var recipient = AddSignerField(envelope, "firmante@example.com", 1, out _);
        envelope.Send();

        var act = () => envelope.AddField(recipient.Id, FieldType.Date, 1, 0.5, 0.5, 0.1, 0.1, required: false);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void UpdateField_after_send_is_rejected()
    {
        var envelope = CreateDraft();
        AddSignerField(envelope, "firmante@example.com", 1, out var fieldId);
        envelope.Send();

        var act = () => envelope.UpdateField(fieldId, 0.5, 0.5, 0.2, 0.2);

        act.Should().Throw<DomainException>();
    }

    // --- Ataque: intentar firmar un campo que pertenece a otro firmante ---
    [Fact]
    public void RegisterSignature_with_field_belonging_to_another_recipient_is_rejected()
    {
        var envelope = CreateDraft(SigningMode.Parallel);
        var first = AddSignerField(envelope, "primero@example.com", 1, out _);
        var second = AddSignerField(envelope, "segundo@example.com", 2, out var secondFieldId);
        envelope.Send();
        envelope.RegisterConsent(first.Id, "1.2.3.4", "agent", "EmailOtp");

        var act = () => envelope.RegisterSignature(
            first.Id, [(secondFieldId, null, [1, 2, 3], SignatureMethodUsed.Uploaded)], "1.2.3.4", "agent", "EmailOtp");

        act.Should().Throw<DomainException>().WithMessage("*no pertenece*");
    }

    // --- Ataque: intentar firmar después de que el sobre expiró/fue cancelado/completado ---
    [Fact]
    public void RegisterSignature_after_expiration_is_rejected()
    {
        var envelope = CreateDraft();
        var recipient = AddSignerField(envelope, "firmante@example.com", 1, out var fieldId);
        envelope.Send();
        envelope.RegisterConsent(recipient.Id, "1.2.3.4", "agent", "EmailOtp");
        envelope.Expire();

        var act = () => envelope.RegisterSignature(
            recipient.Id, [(fieldId, null, [1, 2, 3], SignatureMethodUsed.Uploaded)], "1.2.3.4", "agent", "EmailOtp");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Cancel_after_already_terminal_is_rejected()
    {
        var envelope = CreateDraft();
        AddSignerField(envelope, "firmante@example.com", 1, out _);
        envelope.Send();
        envelope.Cancel();

        var act = () => envelope.Cancel();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Expire_from_a_terminal_state_is_rejected()
    {
        var envelope = CreateDraft();
        AddSignerField(envelope, "firmante@example.com", 1, out _);
        envelope.Send();
        envelope.Cancel();

        var act = () => envelope.Expire();

        act.Should().Throw<DomainException>();
    }

    // --- Firma real de dos campos del mismo firmante produce hashes distintos (unicidad) ---
    [Fact]
    public void Distinct_fields_for_the_same_recipient_get_distinct_signature_hashes()
    {
        var envelope = CreateDraft(SigningMode.Parallel);
        var recipient = envelope.AddRecipient("firmante@example.com", "Firmante", 1);
        var field1 = envelope.AddField(recipient.Id, FieldType.Signature, 1, 0.1, 0.1, 0.3, 0.1, required: true);
        var field2 = envelope.AddField(recipient.Id, FieldType.Initials, 1, 0.6, 0.6, 0.1, 0.1, required: true);
        envelope.Send();
        envelope.RegisterConsent(recipient.Id, "1.2.3.4", "agent", "EmailOtp");

        envelope.RegisterSignature(
            recipient.Id,
            [(field1.Id, null, [1, 2, 3], SignatureMethodUsed.Uploaded), (field2.Id, null, [4, 5, 6], SignatureMethodUsed.Uploaded)],
            "1.2.3.4", "agent", "EmailOtp");

        var signedField1 = envelope.Fields.Single(f => f.Id == field1.Id);
        var signedField2 = envelope.Fields.Single(f => f.Id == field2.Id);
        signedField1.SignatureHash.Should().NotBeNullOrEmpty();
        signedField2.SignatureHash.Should().NotBeNullOrEmpty();
        signedField1.SignatureHash.Should().NotBe(signedField2.SignatureHash);
    }

    // --- Recordatorios: respeta el intervalo y el máximo configurado ---
    [Fact]
    public void GetRecipientsDueForReminder_excludes_recipients_reminded_too_recently()
    {
        var envelope = CreateDraft(SigningMode.Parallel);
        envelope.AddRecipient("firmante@example.com", "Firmante", 1);
        envelope.AddField(envelope.Recipients[0].Id, FieldType.Signature, 1, 0.1, 0.1, 0.3, 0.1, required: true);
        envelope.Send();

        // Freshly sent (SentAtUtc ~= now) is NOT yet 3 days old, so with a real 3-day interval it must not be due.
        var freshlySent = envelope.GetRecipientsDueForReminder(DateTime.UtcNow, TimeSpan.FromDays(3), maxReminders: 3);
        freshlySent.Should().BeEmpty();

        // But evaluated as if "now" were 4 days later, the same SentAtUtc IS due.
        var dueMuchLater = envelope.GetRecipientsDueForReminder(DateTime.UtcNow.AddDays(4), TimeSpan.FromDays(3), maxReminders: 3);
        dueMuchLater.Should().ContainSingle();
    }

    [Fact]
    public void GetRecipientsDueForReminder_respects_max_reminder_cap()
    {
        var envelope = CreateDraft(SigningMode.Parallel);
        var recipient = envelope.AddRecipient("firmante@example.com", "Firmante", 1);
        envelope.AddField(recipient.Id, FieldType.Signature, 1, 0.1, 0.1, 0.3, 0.1, required: true);
        envelope.Send();

        envelope.MarkReminderSent(recipient.Id);
        envelope.MarkReminderSent(recipient.Id);

        var due = envelope.GetRecipientsDueForReminder(DateTime.UtcNow.AddDays(10), TimeSpan.FromDays(3), maxReminders: 2);

        due.Should().BeEmpty("ya alcanzó el máximo de 2 recordatorios configurado");
    }

    // --- Consentimiento: el texto y la versión quedan fijados como evidencia ---
    [Fact]
    public void RegisterConsent_creates_a_ConsentRecord_with_the_declaration_text()
    {
        var envelope = CreateDraft();
        var recipient = envelope.AddRecipient("firmante@example.com", "Firmante", 1);

        var consent = envelope.RegisterConsent(recipient.Id, "1.2.3.4", "agent", "EmailOtp");

        consent.ConsentText.Should().Be(ConsentRecord.DeclarationText);
        consent.ConsentVersion.Should().Be(ConsentRecord.CurrentVersion);
        envelope.ConsentRecords.Should().ContainSingle();
    }

    // --- Envolvente/hash del sobre: alterar cualquier dato de un firmante cambia el hash ---
    [Fact]
    public void PreviewEnvelopeHash_changes_when_recipient_state_changes()
    {
        var envelope = CreateDraft(SigningMode.Parallel);
        var recipient = envelope.AddRecipient("firmante@example.com", "Firmante", 1);
        var field = envelope.AddField(recipient.Id, FieldType.Signature, 1, 0.1, 0.1, 0.3, 0.1, required: true);
        envelope.Send();
        envelope.RegisterConsent(recipient.Id, "1.2.3.4", "agent", "EmailOtp");
        var hashBeforeSigning = envelope.PreviewEnvelopeHash();

        envelope.RegisterSignature(recipient.Id, [(field.Id, null, [1, 2, 3], SignatureMethodUsed.Uploaded)], "1.2.3.4", "agent", "EmailOtp");
        var hashAfterSigning = envelope.PreviewEnvelopeHash();

        hashAfterSigning.Should().NotBe(hashBeforeSigning);
    }
}
