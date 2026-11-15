using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using Application.Features.LoanApplications.Profiles;
using Application.Services.CustomerNaming;
using Application.Services.Repositories;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using BankingSystemCore.Repositories;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.LoanApplications.Queries.GetList;

public class GetListLoanApplicationQuery : IRequest<Paginate<GetListLoanApplicationListItemDto>>, ISecuredRequest
{
    IReadOnlyCollection<string> ISecuredRequest.Roles => BankRoles.StaffAndCustomer;

    /// <summary>Zero-based page index.</summary>
    public int Index { get; set; } = 0;

    public int Size { get; set; } = 10;

    /// <summary>Optional filter: only the applications of this customer.</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>Optional filter: only applications in this status.</summary>
    public LoanApplicationStatus? Status { get; set; }
}

public class GetListLoanApplicationQueryHandler
    : IRequestHandler<GetListLoanApplicationQuery, Paginate<GetListLoanApplicationListItemDto>>
{
    private readonly ILoanApplicationRepository _applications;
    private readonly CustomerAccessPolicy _access;

    public GetListLoanApplicationQueryHandler(ILoanApplicationRepository applications, CustomerAccessPolicy access)
    {
        _applications = applications;
        _access = access;
    }

    public async Task<Paginate<GetListLoanApplicationListItemDto>> Handle(
        GetListLoanApplicationQuery request, CancellationToken cancellationToken)
    {
        Guid? customerId = request.CustomerId;
        if (!_access.IsStaff)
        {
            // A customer always gets their own applications, never somebody else's.
            Guid own = _access.OwnCustomerId ?? throw new AuthorizationException("You can only access your own data.");
            if (customerId is not null)
                _access.EnsureCanAccess(customerId.Value);
            customerId = own;
        }

        LoanApplicationStatus? status = request.Status;

        Paginate<LoanApplication> page = await _applications.GetListAsync(
            predicate: a => (customerId == null || a.CustomerId == customerId)
                            && (status == null || a.Status == status),
            orderBy: q => q.OrderByDescending(a => a.CreateDate),
            include: q => q.Include(a => a.Customer).Include(a => a.LoanSubType).ThenInclude(s => s!.LoanType),
            index: request.Index,
            size: request.Size,
            enableTracking: false,
            cancellationToken: cancellationToken);

        Paginate<GetListLoanApplicationListItemDto> response = LoanApplicationMapper.ToListResponse(page);

        // The customer's name depends on its type (person or company), so it is filled in here.
        for (int i = 0; i < response.Items.Count; i++)
            response.Items[i].CustomerName = CustomerDisplayName.Of(page.Items[i].Customer);

        return response;
    }
}
