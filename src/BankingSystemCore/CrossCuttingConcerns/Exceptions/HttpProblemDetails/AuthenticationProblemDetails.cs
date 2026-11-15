using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BankingSystemCore.CrossCuttingConcerns.Exceptions.HttpProblemDetails;

public class AuthenticationProblemDetails : ProblemDetails
{
    public AuthenticationProblemDetails(string detail)
    {
        Title = "Authentication error";
        Detail = detail;
        Status = StatusCodes.Status401Unauthorized;
        Type = "urn:problem:authentication";
    }
}
