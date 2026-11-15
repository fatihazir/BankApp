namespace Application.Features.Auth.Queries.Me;

public class MeResponse
{
    public Guid? UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
}
