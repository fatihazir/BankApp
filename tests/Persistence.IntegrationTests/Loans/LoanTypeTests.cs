using Application.Features.LoanTypes.Queries.GetList;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.EntityConfigurations;
using Persistence.IntegrationTests.Infrastructure;

namespace Persistence.IntegrationTests.Loans;

[Collection(DatabaseCollection.Name)]
public class LoanTypeTests(SqlServerFixture database) : IntegrationTestBase(database)
{
    [Fact]
    public async Task Migration_SeedsTwoLoanTypes_AndSixSubTypes()
    {
        int types = await UsingDbContext(db => db.LoanTypes.CountAsync(TestContext.Current.CancellationToken));
        int subTypes = await UsingDbContext(db => db.LoanSubTypes.CountAsync(TestContext.Current.CancellationToken));

        (types, subTypes).ShouldBe((2, 6));
    }

    [Fact]
    public async Task GetList_ReturnsTypesWithTheirSubTypes()
    {
        var result = (await Send(new GetListLoanTypeQuery())).Items;

        result.Count.ShouldBe(2);
        result.Select(t => t.Segment).ShouldBe([CustomerSegment.Individual, CustomerSegment.Corporate]);

        var individual = result.Single(t => t.Segment == CustomerSegment.Individual);
        individual.SubTypes.Select(s => s.Name).ShouldBe(["Auto Loan", "Mortgage Loan", "Personal Loan"]);

        var corporate = result.Single(t => t.Segment == CustomerSegment.Corporate);
        corporate.SubTypes.Select(s => s.Name).ShouldBe(["Commercial Loan", "Investment Loan", "Working Capital Loan"]);
    }

    [Theory]
    [InlineData(CustomerSegment.Individual, 3)]
    [InlineData(CustomerSegment.Corporate, 3)]
    public async Task GetList_FiltersBySegment(CustomerSegment segment, int expectedSubTypes)
    {
        var result = (await Send(new GetListLoanTypeQuery { Segment = segment })).Items;

        result.Count.ShouldBe(1);
        result[0].Segment.ShouldBe(segment);
        result[0].SubTypes.Count.ShouldBe(expectedSubTypes);
    }

    [Fact]
    public async Task GetList_SubTypeCarriesItsLimitsAndRate()
    {
        var personal = (await Send(new GetListLoanTypeQuery { Segment = CustomerSegment.Individual })).Items[0]
            .SubTypes.Single(s => s.Id == LoanSeedData.PersonalLoanId);

        (personal.MinAmount, personal.MaxAmount, personal.MinTermInMonths, personal.MaxTermInMonths, personal.AnnualInterestRate)
            .ShouldBe((5_000m, 1_000_000m, 3, 60, 48.00m));
    }

    [Fact]
    public async Task GetList_HidesInactiveSubTypesAndTypes()
    {
        await UsingDbContext(async db =>
        {
            (await db.LoanSubTypes.FindAsync([LoanSeedData.AutoLoanId], TestContext.Current.CancellationToken))!.IsActive = false;
            (await db.LoanTypes.FindAsync([LoanSeedData.CorporateLoanTypeId], TestContext.Current.CancellationToken))!.IsActive = false;
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var result = (await Send(new GetListLoanTypeQuery())).Items;

        result.Count.ShouldBe(1);
        result[0].SubTypes.Select(s => s.Name).ShouldBe(["Mortgage Loan", "Personal Loan"]);
    }

    [Fact]
    public async Task GetList_IsPaged_LikeEveryOtherList()
    {
        var first = await Send(new GetListLoanTypeQuery { Index = 0, Size = 1 });
        var second = await Send(new GetListLoanTypeQuery { Index = 1, Size = 1 });

        (first.Count, first.Pages, first.Items.Count, first.HasNext, first.HasPrevious).ShouldBe((2, 2, 1, true, false));
        (second.Items.Count, second.HasNext, second.HasPrevious).ShouldBe((1, false, true));
        first.Items[0].Segment.ShouldBe(CustomerSegment.Individual);
        second.Items[0].Segment.ShouldBe(CustomerSegment.Corporate);
    }
}
