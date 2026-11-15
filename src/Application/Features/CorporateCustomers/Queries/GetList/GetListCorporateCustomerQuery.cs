using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.CorporateCustomers.Profiles;
using Application.Services.Repositories;
using BankingSystemCore.Repositories;
using Domain.Entities;
using MediatR;

namespace Application.Features.CorporateCustomers.Queries.GetList;

public class GetListCorporateCustomerQuery : IRequest<Paginate<GetListCorporateCustomerListItemDto>>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffOnly;

    /// <summary>Zero-based page index.</summary>
    public int Index { get; set; } = 0;

    public int Size { get; set; } = 10;
}

public class GetListCorporateCustomerQueryHandler
    : IRequestHandler<GetListCorporateCustomerQuery, Paginate<GetListCorporateCustomerListItemDto>>
{
    private readonly ICorporateCustomerRepository _repository;

    public GetListCorporateCustomerQueryHandler(ICorporateCustomerRepository repository)
    {
        _repository = repository;
    }

    public async Task<Paginate<GetListCorporateCustomerListItemDto>> Handle(
        GetListCorporateCustomerQuery request, CancellationToken cancellationToken)
    {
        Paginate<CorporateCustomer> customers = await _repository.GetListAsync(
            orderBy: q => q.OrderByDescending(c => c.CreateDate),
            index: request.Index,
            size: request.Size,
            enableTracking: false,
            cancellationToken: cancellationToken);

        return CorporateCustomerMapper.ToListResponse(customers);
    }
}
