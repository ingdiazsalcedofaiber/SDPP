using FluentValidation;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Identity.Application.Ports;

namespace SDPP.Identity.Application.UseCases.Admin.Reporting;

public sealed record GetUsersOverviewQuery(DateTime FromUtc, DateTime ToUtc, ReportingTimeBucket Bucket) : IQuery<UsersOverview>;

public sealed class GetUsersOverviewValidator : AbstractValidator<GetUsersOverviewQuery>
{
    public GetUsersOverviewValidator()
    {
        RuleFor(q => q.ToUtc).GreaterThanOrEqualTo(q => q.FromUtc)
            .WithMessage("La fecha 'hasta' debe ser igual o posterior a la fecha 'desde'.");
        RuleFor(q => q).Must(q => (q.ToUtc - q.FromUtc).TotalDays <= 366)
            .WithMessage("El rango de fechas no puede superar un año.");
    }
}

public sealed class GetUsersOverviewHandler(IUsersReportingQueries reportingQueries)
    : IRequestHandler<GetUsersOverviewQuery, Result<UsersOverview>>
{
    public async Task<Result<UsersOverview>> Handle(GetUsersOverviewQuery request, CancellationToken cancellationToken)
    {
        var overview = await reportingQueries.GetOverviewAsync(request.FromUtc, request.ToUtc, request.Bucket, cancellationToken);
        return Result.Success(overview);
    }
}
