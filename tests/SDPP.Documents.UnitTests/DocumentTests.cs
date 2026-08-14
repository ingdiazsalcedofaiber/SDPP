using FluentAssertions;
using SDPP.BuildingBlocks.Domain;
using SDPP.Documents.Domain.Aggregates;
using SDPP.Documents.Domain.Enums;
using SDPP.Documents.Domain.Events;
using Xunit;

namespace SDPP.Documents.UnitTests;

public class DocumentTests
{
    [Fact]
    public void Upload_raises_DocumentUploaded_domain_event()
    {
        var document = DocumentInstance.Upload(Guid.NewGuid(), "contrato.docx", "application/msword", 1024, Guid.NewGuid());

        document.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DocumentUploaded>();
        document.Status.Should().Be(DocumentStatus.Uploaded);
    }

    [Fact]
    public void RequestConversion_fails_when_document_is_blocked()
    {
        var document = DocumentInstance.Upload(Guid.NewGuid(), "contrato.docx", "application/msword", 1024, Guid.NewGuid());
        document.Block("Hallazgo crítico en la inspección automática");

        var act = () => document.RequestConversion(OperationType.WordToPdf);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void CompleteInspection_with_manual_review_keeps_document_in_Inspecting()
    {
        var document = DocumentInstance.Upload(Guid.NewGuid(), "contrato.docx", "application/msword", 1024, Guid.NewGuid());
        document.BeginInspection();

        document.CompleteInspection(requiresManualReview: true);

        document.Status.Should().Be(DocumentStatus.Inspecting);
    }

    [Fact]
    public void CompleteInspection_without_manual_review_marks_document_Ready()
    {
        var document = DocumentInstance.Upload(Guid.NewGuid(), "contrato.docx", "application/msword", 1024, Guid.NewGuid());
        document.BeginInspection();

        document.CompleteInspection(requiresManualReview: false);

        document.Status.Should().Be(DocumentStatus.Ready);
    }

    [Fact]
    public void QueueJob_then_MarkCompleted_transitions_through_expected_states()
    {
        var document = DocumentInstance.Upload(Guid.NewGuid(), "contrato.docx", "application/msword", 1024, Guid.NewGuid());
        document.BeginInspection();
        document.CompleteInspection(requiresManualReview: false);

        var job = document.RequestConversion(OperationType.WordToPdf);
        document.QueueJob(job.Id);
        document.StartProcessingJob(job.Id);

        var outputDocumentId = Guid.NewGuid();
        document.CompleteJob(job.Id, outputDocumentId, "LibreOffice", durationMs: 1200);

        job.Status.Should().Be(ConversionJobStatus.Completed);
        job.OutputDocumentId.Should().Be(outputDocumentId);
        job.EngineUsed.Should().Be("LibreOffice");
    }

    [Fact]
    public void CompleteJob_before_Processing_throws()
    {
        var document = DocumentInstance.Upload(Guid.NewGuid(), "contrato.docx", "application/msword", 1024, Guid.NewGuid());
        document.BeginInspection();
        document.CompleteInspection(requiresManualReview: false);
        var job = document.RequestConversion(OperationType.WordToPdf);

        var act = () => document.CompleteJob(job.Id, Guid.NewGuid(), "LibreOffice", 100);

        act.Should().Throw<DomainException>();
    }
}
