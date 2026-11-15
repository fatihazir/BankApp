namespace Application.Features.IndividualCustomers.Commands.Update;

public class UpdatedIndividualCustomerResponse
{
    public Guid Id { get; set; }
    public string CustomerNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime? UpdateDate { get; set; }
}
