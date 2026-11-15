using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.LoanTypes.Profiles;
using Application.Services.Repositories;
using BankingSystemCore.Repositories;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.LoanTypes.Queries.GetList;

public class GetListLoanTypeQuery : IRequest<Paginate<GetListLoanTypeListItemDto>>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.AnyAuthenticated;

    /// <summary>Zero-based page index.</summary>
    public int Index { get; set; } = 0;

    public int Size { get; set; } = 10;

    /// <summary>Optional filter: only the loan types of this customer segment.</summary>
    public CustomerSegment? Segment { get; set; }
}

public class GetListLoanTypeQueryHandler : IRequestHandler<GetListLoanTypeQuery, Paginate<GetListLoanTypeListItemDto>>
{
    private readonly ILoanTypeRepository _loanTypes;

    public GetListLoanTypeQueryHandler(ILoanTypeRepository loanTypes)
    {
        _loanTypes = loanTypes;
    }

    public async Task<Paginate<GetListLoanTypeListItemDto>> Handle(
        GetListLoanTypeQuery request, CancellationToken cancellationToken)
    {
        CustomerSegment? segment = request.Segment;

        Paginate<LoanType> page = await _loanTypes.GetListAsync(
            predicate: t => t.IsActive && (segment == null || t.Segment == segment),
            orderBy: q => q.OrderBy(t => t.Segment).ThenBy(t => t.Name),
            include: q => q.Include(t => t.SubTypes.Where(s => s.IsActive).OrderBy(s => s.Name)),
            index: request.Index,
            size: request.Size,
            enableTracking: false,
            cancellationToken: cancellationToken);

        return LoanTypeMapper.ToListResponse(page);
    }
}
