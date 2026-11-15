using Application.Features.CorporateCustomers.Commands.Create;
using Application.Features.CorporateCustomers.Commands.Delete;
using Application.Features.CorporateCustomers.Commands.Update;
using Application.Features.CorporateCustomers.Profiles;
using Application.Features.CorporateCustomers.Queries.GetById;
using Application.Features.CorporateCustomers.Queries.GetList;
using Application.UnitTests.Support;
using BankingSystemCore.Repositories;
using Domain.Entities;

namespace Application.UnitTests.Mapping;

public class CorporateCustomerMapperContractTests
{
    // Values the system sets itself (or that belong to the login and application parts of the user); they must never
    // come from the client in this form.
    private static readonly string[] LoginAndSystemFields =
    [
        nameof(Customer.PasswordHash), nameof(Customer.FailedLoginCount), nameof(Customer.LockoutEnd), nameof(Customer.LastLoginDate),
        nameof(Customer.EmailVerified), nameof(Customer.EmailVerifiedDate), nameof(Customer.EmailVerificationTokenHash),
        nameof(Customer.EmailVerificationTokenExpiresAt), nameof(Customer.RowVersion), nameof(Customer.Role),
        nameof(Customer.BannedDate), nameof(Customer.BanReason), nameof(Customer.CustomerNumber), nameof(Customer.CreditScore),
        nameof(Customer.CreateDate), nameof(Customer.UpdateDate), nameof(Customer.DeleteDate)
    ];

    // On create the id and the active flag are set by the system; the e-mail comes from the command.
    private static readonly string[] SystemManagedOnCreate = [..LoginAndSystemFields, nameof(Customer.Id), nameof(Customer.IsActive)];

    // On update the key never changes and the e-mail (login name) is not changed here.
    private static readonly string[] SystemManagedOnUpdate = [..LoginAndSystemFields, nameof(Customer.Email)];

    // ---- command -> entity ----

    [Fact]
    public void Create_CopiesEveryCommandProperty_ToTheEntity()
    {
        var command = MappingContract.Filled<CreateCorporateCustomerCommand>();

        CorporateCustomer entity = CorporateCustomerMapper.ToEntity(command);

        // The password is hashed by the handler; it is not a property of the entity.
        MappingContract.AssertAllSourcePropertiesCopied(command, entity, "Password");
    }

    [Fact]
    public void Create_LeavesSystemManagedFieldsUntouched()
    {
        CorporateCustomer entity = CorporateCustomerMapper.ToEntity(MappingContract.Filled<CreateCorporateCustomerCommand>());

        entity.Id.ShouldBe(Guid.Empty);
        entity.CustomerNumber.ShouldBeEmpty();
        entity.CreditScore.ShouldBeNull();
        entity.CreateDate.ShouldBe(default);
        entity.UpdateDate.ShouldBeNull();
        entity.DeleteDate.ShouldBeNull();
    }

    [Fact]
    public void Create_HasEveryEntityPropertyAccountedFor()
    {
        MappingContract.AssertEntityPropertiesAreAccountedFor(
            typeof(CorporateCustomer), typeof(CreateCorporateCustomerCommand), SystemManagedOnCreate);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Update_AppliesEveryCommandProperty_ToTheEntity(bool isActive)
    {
        var command = MappingContract.Filled<UpdateCorporateCustomerCommand>(isActive);
        Guid ownId = Guid.NewGuid();
        var entity = new CorporateCustomer { Id = ownId, IsActive = !isActive };

        CorporateCustomerMapper.Apply(command, entity);

        // The key never changes, whatever the body says.
        entity.Id.ShouldBe(ownId);
        MappingContract.AssertAllSourcePropertiesCopied(command, entity, nameof(Customer.Id));
    }

    [Fact]
    public void Update_DoesNotOverwriteSystemManagedFields()
    {
        var entity = MappingContract.Filled<CorporateCustomer>();
        var before = (entity.Email, entity.PasswordHash, entity.Role, entity.EmailVerified, entity.CustomerNumber, entity.CreditScore,
            entity.CreateDate, entity.UpdateDate, entity.DeleteDate, entity.FailedLoginCount, entity.BannedDate);

        CorporateCustomerMapper.Apply(MappingContract.Filled<UpdateCorporateCustomerCommand>(), entity);

        (entity.Email, entity.PasswordHash, entity.Role, entity.EmailVerified, entity.CustomerNumber, entity.CreditScore,
            entity.CreateDate, entity.UpdateDate, entity.DeleteDate, entity.FailedLoginCount, entity.BannedDate).ShouldBe(before);
    }

    [Fact]
    public void Update_HasEveryEntityPropertyAccountedFor()
    {
        MappingContract.AssertEntityPropertiesAreAccountedFor(
            typeof(CorporateCustomer), typeof(UpdateCorporateCustomerCommand), SystemManagedOnUpdate);
    }

    // ---- entity -> responses ----

    [Theory]
    [InlineData(typeof(CreatedCorporateCustomerResponse))]
    [InlineData(typeof(UpdatedCorporateCustomerResponse))]
    [InlineData(typeof(DeletedCorporateCustomerResponse))]
    [InlineData(typeof(GetByIdCorporateCustomerResponse))]
    [InlineData(typeof(GetListCorporateCustomerListItemDto))]
    public void Response_HasOnlyPropertiesThatExistOnTheEntity(Type responseType)
    {
        MappingContract.AssertAllTargetPropertiesHaveASource(typeof(CorporateCustomer), responseType);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllResponses_CopyEveryPropertyFromTheEntity(bool isActive)
    {
        var entity = MappingContract.Filled<CorporateCustomer>(isActive);

        MappingContract.AssertAllSourcePropertiesCopied(entity, CorporateCustomerMapper.ToCreatedResponse(entity), Unexposed(typeof(CreatedCorporateCustomerResponse)));
        MappingContract.AssertAllSourcePropertiesCopied(entity, CorporateCustomerMapper.ToUpdatedResponse(entity), Unexposed(typeof(UpdatedCorporateCustomerResponse)));
        MappingContract.AssertAllSourcePropertiesCopied(entity, CorporateCustomerMapper.ToDeletedResponse(entity), Unexposed(typeof(DeletedCorporateCustomerResponse)));
        MappingContract.AssertAllSourcePropertiesCopied(entity, CorporateCustomerMapper.ToGetByIdResponse(entity), Unexposed(typeof(GetByIdCorporateCustomerResponse)));
        MappingContract.AssertAllSourcePropertiesCopied(entity, CorporateCustomerMapper.ToListItem(entity), Unexposed(typeof(GetListCorporateCustomerListItemDto)));
    }

    [Fact]
    public void NoResponse_EverExposesLoginSecrets()
    {
        Type[] responses = [typeof(CreatedCorporateCustomerResponse), typeof(UpdatedCorporateCustomerResponse), typeof(DeletedCorporateCustomerResponse),
            typeof(GetByIdCorporateCustomerResponse), typeof(GetListCorporateCustomerListItemDto)];

        foreach (Type response in responses)
            response.GetProperties().Select(p => p.Name).ShouldNotContain(
                n => n.Contains("Password") || n.Contains("Hash") || n.Contains("RowVersion") || n.Contains("Token"),
                $"{response.Name} exposes a secret.");
    }

    [Fact]
    public void GetByIdResponse_DoesNotExposeDeleteDate()
    {
        typeof(GetByIdCorporateCustomerResponse).GetProperty(nameof(Customer.DeleteDate)).ShouldBeNull();
    }

    // ---- nulls, paging, round trip ----

    [Fact]
    public void OptionalValues_StayNull_WhenNotProvided()
    {
        var command = MappingContract.Filled<CreateCorporateCustomerCommand>();
        command.PostalCode = null;
        command.TradeRegistryNumber = null;
        command.Sector = null;
        command.AuthorizedPersonTitle = null;
        command.AuthorizedPersonNationalId = null;

        CorporateCustomer entity = CorporateCustomerMapper.ToEntity(command);
        GetByIdCorporateCustomerResponse response = CorporateCustomerMapper.ToGetByIdResponse(entity);

        entity.PostalCode.ShouldBeNull();
        response.PostalCode.ShouldBeNull();
        response.TradeRegistryNumber.ShouldBeNull();
        response.Sector.ShouldBeNull();
        response.AuthorizedPersonTitle.ShouldBeNull();
        response.AuthorizedPersonNationalId.ShouldBeNull();
    }

    [Fact]
    public void ListResponse_MapsEveryItem_AndPagingInformation()
    {
        var page = new Paginate<CorporateCustomer>
        {
            Index = 2, Size = 3, Count = 8, Pages = 3,
            Items = [MappingContract.Filled<CorporateCustomer>(), MappingContract.Filled<CorporateCustomer>()]
        };

        var result = CorporateCustomerMapper.ToListResponse(page);

        (result.Index, result.Size, result.Count, result.Pages).ShouldBe((2, 3, 8, 3));
        result.Items.Count.ShouldBe(2);
        for (int i = 0; i < page.Items.Count; i++)
            MappingContract.AssertAllSourcePropertiesCopied(page.Items[i], result.Items[i], Unexposed(typeof(GetListCorporateCustomerListItemDto)));
    }

    [Fact]
    public void ListResponse_OfAnEmptyPage_IsEmpty()
    {
        var result = CorporateCustomerMapper.ToListResponse(new Paginate<CorporateCustomer> { Size = 10 });

        result.Items.ShouldBeEmpty();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public void RoundTrip_CommandToEntityToGetByIdResponse_KeepsEveryClientValue()
    {
        var command = MappingContract.Filled<CreateCorporateCustomerCommand>();

        var response = CorporateCustomerMapper.ToGetByIdResponse(CorporateCustomerMapper.ToEntity(command));

        MappingContract.AssertAllSourcePropertiesCopied(command, response, "Password");
    }

    /// <summary>Entity properties the given response type intentionally does not expose.</summary>
    private static string[] Unexposed(Type responseType) =>
        typeof(CorporateCustomer).GetProperties().Select(p => p.Name)
            .Where(name => responseType.GetProperty(name) is null).ToArray();
}
