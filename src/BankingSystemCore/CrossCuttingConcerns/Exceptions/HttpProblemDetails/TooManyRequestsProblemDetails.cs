using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BankingSystemCore.CrossCuttingConcerns.Exceptions.HttpProblemDetails;

public class TooManyRequestsProblemDetails : ProblemDetails
{
    public TooManyRequestsProblemDetails(string detail)
    {
        Title = "Too many requests";
        Detail = detail;
        Status = StatusCodes.Status429TooManyRequests;
        Type = "urn:problem:rate-limit";
    }
}
