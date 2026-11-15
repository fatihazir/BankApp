using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.IndividualCustomers.Profiles;
using Application.Services.Repositories;
using BankingSystemCore.Repositories;
using Domain.Entities;
using MediatR;

namespace Application.Features.IndividualCustomers.Queries.GetList;

public class GetListIndividualCustomerQuery : IRequest<Paginate<GetListIndividualCustomerListItemDto>>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffOnly;

    /// <summary>Zero-based page index.</summary>
    public int Index { get; set; } = 0;

    public int Size { get; set; } = 10;
}

public class GetListIndividualCustomerQueryHandler
    : IRequestHandler<GetListIndividualCustomerQuery, Paginate<GetListIndividualCustomerListItemDto>>
{
    private readonly IIndividualCustomerRepository _repository;

    public GetListIndividualCustomerQueryHandler(IIndividualCustomerRepository repository)
    {
        _repository = repository;
    }

    public async Task<Paginate<GetListIndividualCustomerListItemDto>> Handle(
        GetListIndividualCustomerQuery request, CancellationToken cancellationToken)
    {
        Paginate<IndividualCustomer> customers = await _repository.GetListAsync(
            orderBy: q => q.OrderByDescending(c => c.CreateDate),
            index: request.Index,
            size: request.Size,
            enableTracking: false,
            cancellationToken: cancellationToken);

        return IndividualCustomerMapper.ToListResponse(customers);
    }
}
