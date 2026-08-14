using FluentValidation;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Documents.Application.Ports;

namespace SDPP.Documents.Application.UseCases.Reporting;

/// <summary>Backs both the personal and admin reporting endpoints — see
/// IDocumentsReportingQueries. OwnerId null means platform-wide; a real value scopes to that
/// owner. Endpoints are what decide which one they're allowed to ask for, this query trusts
/// whatever it's given (the API layer, not this layer, enforces "a non-admin can only ever pass
/// their own id" — see ReportingEndpoints.cs).</summary>
public sealed record GetDocumentsOverviewQuery(Guid? OwnerId, DateTime FromUtc, DateTime ToUtc, TimeBucket Bucket)
    : IQuery<DocumentsOverview>;

public sealed class GetDocumentsOverviewValidator : AbstractValidator<GetDocumentsOverviewQuery>
{
    public GetDocumentsOverviewValidator()
    {
        RuleFor(q => q.ToUtc).GreaterThanOrEqualTo(q => q.FromUtc)
            .WithMessage("La fecha 'hasta' debe ser igual o posterior a la fecha 'desde'.");
        RuleFor(q => q).Must(q => (q.ToUtc - q.FromUtc).TotalDays <= 366)
            .WithMessage("El rango de fechas no puede superar un año.");
    }
}

public sealed class GetDocumentsOverviewHandler(IDocumentsReportingQueries reportingQueries)
    : IRequestHandler<GetDocumentsOverviewQuery, Result<DocumentsOverview>>
{
    public async Task<Result<DocumentsOverview>> Handle(GetDocumentsOverviewQuery request, CancellationToken cancellationToken)
    {
        var overview = await reportingQueries.GetOverviewAsync(
            request.OwnerId, request.FromUtc, request.ToUtc, request.Bucket, cancellationToken);
        return Result.Success(overview);
    }
}
