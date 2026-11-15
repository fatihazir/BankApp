using BankingSystemCore.Security.Users;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Contexts;

public class BankAppDbContext : DbContext
{
    public BankAppDbContext(DbContextOptions<BankAppDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<IndividualCustomer> IndividualCustomers => Set<IndividualCustomer>();

    public DbSet<CorporateCustomer> CorporateCustomers => Set<CorporateCustomer>();

    public DbSet<LoanType> LoanTypes => Set<LoanType>();

    public DbSet<LoanSubType> LoanSubTypes => Set<LoanSubType>();

    public DbSet<LoanApplication> LoanApplications => Set<LoanApplication>();

    public DbSet<User> Users => Set<User>();

    public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BankAppDbContext).Assembly);
    }
}
