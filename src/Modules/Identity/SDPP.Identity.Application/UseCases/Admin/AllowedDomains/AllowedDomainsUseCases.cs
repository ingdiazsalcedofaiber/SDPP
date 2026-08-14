using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Identity.Application.Ports;
using SDPP.Identity.Domain.Aggregates;

namespace SDPP.Identity.Application.UseCases.Admin.AllowedDomains;

public sealed record AllowedDomainDto(Guid Id, string Domain, bool IsActive, bool IsDevOnly, DateTime CreatedAtUtc, string? Notes);

public sealed record ListAllowedDomainsQuery : IQuery<IReadOnlyList<AllowedDomainDto>>;

public sealed class ListAllowedDomainsHandler(IAllowedDomainRepository repository)
    : IRequestHandler<ListAllowedDomainsQuery, Result<IReadOnlyList<AllowedDomainDto>>>
{
    public async Task<Result<IReadOnlyList<AllowedDomainDto>>> Handle(ListAllowedDomainsQuery request, CancellationToken cancellationToken)
    {
        var domains = await repository.GetAllAsync(cancellationToken);
        IReadOnlyList<AllowedDomainDto> dtos = domains
            .Select(d => new AllowedDomainDto(d.Id, d.Domain, d.IsActive, d.IsDevOnly, d.CreatedAtUtc, d.Notes)).ToList();
        return Result.Success(dtos);
    }
}

public sealed record CreateAllowedDomainCommand(string Domain, bool IsDevOnly, string? Notes) : ICommand<AllowedDomainDto>;

public sealed class CreateAllowedDomainHandler(IAllowedDomainRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateAllowedDomainCommand, Result<AllowedDomainDto>>
{
    public async Task<Result<AllowedDomainDto>> Handle(CreateAllowedDomainCommand request, CancellationToken cancellationToken)
    {
        if (await repository.GetByDomainAsync(request.Domain, cancellationToken) is not null)
        {
            return Result.Failure<AllowedDomainDto>("Ese dominio ya está en la lista.", "DOMAIN_ALREADY_EXISTS");
        }

        var domain = AllowedDomain.Create(request.Domain, request.IsDevOnly, request.Notes);
        repository.Add(domain);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new AllowedDomainDto(domain.Id, domain.Domain, domain.IsActive, domain.IsDevOnly, domain.CreatedAtUtc, domain.Notes));
    }
}

public sealed record DeleteAllowedDomainCommand(Guid Id) : ICommand;

public sealed class DeleteAllowedDomainHandler(IAllowedDomainRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteAllowedDomainCommand, Result>
{
    public async Task<Result> Handle(DeleteAllowedDomainCommand request, CancellationToken cancellationToken)
    {
        var domain = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (domain is null)
        {
            return Result.Failure("El dominio no existe.", "DOMAIN_NOT_FOUND");
        }

        repository.Remove(domain);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
