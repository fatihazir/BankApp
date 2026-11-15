using Application.Features.IndividualCustomers.Commands.Create;
using Application.Features.IndividualCustomers.Commands.Delete;
using Application.Features.IndividualCustomers.Commands.Update;
using Application.Features.IndividualCustomers.Profiles;
using Application.Features.IndividualCustomers.Queries.GetById;
using Application.Features.IndividualCustomers.Queries.GetList;
using Application.UnitTests.Support;
using BankingSystemCore.Repositories;
using Domain.Entities;

namespace Application.UnitTests.Mapping;

public class IndividualCustomerMapperContractTests
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
        var command = MappingContract.Filled<CreateIndividualCustomerCommand>();

        IndividualCustomer entity = IndividualCustomerMapper.ToEntity(command);

        // The password is hashed by the handler; it is not a property of the entity.
        MappingContract.AssertAllSourcePropertiesCopied(command, entity, "Password");
    }

    [Fact]
    public void Create_LeavesSystemManagedFieldsUntouched()
    {
        IndividualCustomer entity = IndividualCustomerMapper.ToEntity(MappingContract.Filled<CreateIndividualCustomerCommand>());

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
            typeof(IndividualCustomer), typeof(CreateIndividualCustomerCommand), SystemManagedOnCreate);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Update_AppliesEveryCommandProperty_ToTheEntity(bool isActive)
    {
        var command = MappingContract.Filled<UpdateIndividualCustomerCommand>(isActive);
        Guid ownId = Guid.NewGuid();
        var entity = new IndividualCustomer { Id = ownId, IsActive = !isActive };

        IndividualCustomerMapper.Apply(command, entity);

        // The key never changes, whatever the body says.
        entity.Id.ShouldBe(ownId);
        MappingContract.AssertAllSourcePropertiesCopied(command, entity, nameof(Customer.Id));
    }

    [Fact]
    public void Update_DoesNotOverwriteSystemManagedFields()
    {
        var entity = MappingContract.Filled<IndividualCustomer>();
        var before = (entity.Email, entity.PasswordHash, entity.Role, entity.EmailVerified, entity.CustomerNumber, entity.CreditScore,
            entity.CreateDate, entity.UpdateDate, entity.DeleteDate, entity.FailedLoginCount, entity.BannedDate);

        IndividualCustomerMapper.Apply(MappingContract.Filled<UpdateIndividualCustomerCommand>(), entity);

        (entity.Email, entity.PasswordHash, entity.Role, entity.EmailVerified, entity.CustomerNumber, entity.CreditScore,
            entity.CreateDate, entity.UpdateDate, entity.DeleteDate, entity.FailedLoginCount, entity.BannedDate).ShouldBe(before);
    }

    [Fact]
    public void Update_HasEveryEntityPropertyAccountedFor()
    {
        MappingContract.AssertEntityPropertiesAreAccountedFor(
            typeof(IndividualCustomer), typeof(UpdateIndividualCustomerCommand), SystemManagedOnUpdate);
    }

    // ---- entity -> responses ----

    [Theory]
    [InlineData(typeof(CreatedIndividualCustomerResponse))]
    [InlineData(typeof(UpdatedIndividualCustomerResponse))]
    [InlineData(typeof(DeletedIndividualCustomerResponse))]
    [InlineData(typeof(GetByIdIndividualCustomerResponse))]
    [InlineData(typeof(GetListIndividualCustomerListItemDto))]
    public void Response_HasOnlyPropertiesThatExistOnTheEntity(Type responseType)
    {
        MappingContract.AssertAllTargetPropertiesHaveASource(typeof(IndividualCustomer), responseType);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllResponses_CopyEveryPropertyFromTheEntity(bool isActive)
    {
        var entity = MappingContract.Filled<IndividualCustomer>(isActive);

        MappingContract.AssertAllSourcePropertiesCopied(entity, IndividualCustomerMapper.ToCreatedResponse(entity), Unexposed(typeof(CreatedIndividualCustomerResponse)));
        MappingContract.AssertAllSourcePropertiesCopied(entity, IndividualCustomerMapper.ToUpdatedResponse(entity), Unexposed(typeof(UpdatedIndividualCustomerResponse)));
        MappingContract.AssertAllSourcePropertiesCopied(entity, IndividualCustomerMapper.ToDeletedResponse(entity), Unexposed(typeof(DeletedIndividualCustomerResponse)));
        MappingContract.AssertAllSourcePropertiesCopied(entity, IndividualCustomerMapper.ToGetByIdResponse(entity), Unexposed(typeof(GetByIdIndividualCustomerResponse)));
        MappingContract.AssertAllSourcePropertiesCopied(entity, IndividualCustomerMapper.ToListItem(entity), Unexposed(typeof(GetListIndividualCustomerListItemDto)));
    }

    [Fact]
    public void NoResponse_EverExposesLoginSecrets()
    {
        Type[] responses = [typeof(CreatedIndividualCustomerResponse), typeof(UpdatedIndividualCustomerResponse), typeof(DeletedIndividualCustomerResponse),
            typeof(GetByIdIndividualCustomerResponse), typeof(GetListIndividualCustomerListItemDto)];

        foreach (Type response in responses)
            response.GetProperties().Select(p => p.Name).ShouldNotContain(
                n => n.Contains("Password") || n.Contains("Hash") || n.Contains("RowVersion") || n.Contains("Token"),
                $"{response.Name} exposes a secret.");
    }

    [Fact]
    public void GetByIdResponse_DoesNotExposeDeleteDate()
    {
        typeof(GetByIdIndividualCustomerResponse).GetProperty(nameof(Customer.DeleteDate)).ShouldBeNull();
    }

    // ---- nulls, paging, round trip ----

    [Fact]
    public void OptionalValues_StayNull_WhenNotProvided()
    {
        var command = MappingContract.Filled<CreateIndividualCustomerCommand>();
        command.PostalCode = null;
        command.MotherName = null;
        command.Occupation = null;
        command.EmployerName = null;

        IndividualCustomer entity = IndividualCustomerMapper.ToEntity(command);
        GetByIdIndividualCustomerResponse response = IndividualCustomerMapper.ToGetByIdResponse(entity);

        entity.PostalCode.ShouldBeNull();
        response.PostalCode.ShouldBeNull();
        response.MotherName.ShouldBeNull();
        response.Occupation.ShouldBeNull();
        response.EmployerName.ShouldBeNull();
    }

    [Fact]
    public void ListResponse_MapsEveryItem_AndPagingInformation()
    {
        var page = new Paginate<IndividualCustomer>
        {
            Index = 2, Size = 3, Count = 8, Pages = 3,
            Items = [MappingContract.Filled<IndividualCustomer>(), MappingContract.Filled<IndividualCustomer>()]
        };

        var result = IndividualCustomerMapper.ToListResponse(page);

        (result.Index, result.Size, result.Count, result.Pages).ShouldBe((2, 3, 8, 3));
        result.Items.Count.ShouldBe(2);
        for (int i = 0; i < page.Items.Count; i++)
            MappingContract.AssertAllSourcePropertiesCopied(page.Items[i], result.Items[i], Unexposed(typeof(GetListIndividualCustomerListItemDto)));
    }

    [Fact]
    public void ListResponse_OfAnEmptyPage_IsEmpty()
    {
        var result = IndividualCustomerMapper.ToListResponse(new Paginate<IndividualCustomer> { Size = 10 });

        result.Items.ShouldBeEmpty();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public void RoundTrip_CommandToEntityToGetByIdResponse_KeepsEveryClientValue()
    {
        var command = MappingContract.Filled<CreateIndividualCustomerCommand>();

        var response = IndividualCustomerMapper.ToGetByIdResponse(IndividualCustomerMapper.ToEntity(command));

        MappingContract.AssertAllSourcePropertiesCopied(command, response, "Password");
    }

    /// <summary>Entity properties the given response type intentionally does not expose.</summary>
    private static string[] Unexposed(Type responseType) =>
        typeof(IndividualCustomer).GetProperties().Select(p => p.Name)
            .Where(name => responseType.GetProperty(name) is null).ToArray();
}
