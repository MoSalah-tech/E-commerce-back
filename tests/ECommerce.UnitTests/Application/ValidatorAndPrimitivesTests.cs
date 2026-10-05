using ECommerce.Application.Common;
using ECommerce.Application.Features.Auth;
using ECommerce.Application.Features.Cart;
using ECommerce.Application.Features.Orders;
using ECommerce.Application.Features.Products;
using ECommerce.Application.Features.Reviews;
using FluentValidation;
using FluentValidation.Results;
using ECommerce.Domain.Enums;

namespace ECommerce.UnitTests.ApplicationTests;

public sealed class ValidatorTests
{
    private static async Task<ValidationResult> ValidateAsync<T>(IValidator<T> validator, T instance)
    {
        var context = new ValidationContext<T>(instance);
        return await validator.ValidateAsync(context);
    }

    [Fact]
    public async Task LoginValidator_AcceptsWellFormedRequest()
    {
        var result = await ValidateAsync(
            new LoginCommandValidator(),
            new LoginCommand("shopper@example.com", "Passw0rd!"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task LoginValidator_RejectsInvalidEmail(string email)
    {
        var result = await ValidateAsync(
            new LoginCommandValidator(),
            new LoginCommand(email, "Passw0rd!"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName.Contains("Email"));
    }

    [Fact]
    public async Task RegisterValidator_RejectsPasswordMismatch()
    {
        var result = await ValidateAsync(
            new RegisterCommandValidator(),
            new RegisterCommand("shopper@example.com", "Passw0rd!", "Different1!", "Jane Doe", null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName.Contains("ConfirmPassword"));
    }

    [Fact]
    public async Task RegisterValidator_AcceptsMatchingPasswords()
    {
        var result = await ValidateAsync(
            new RegisterCommandValidator(),
            new RegisterCommand("shopper@example.com", "Passw0rd!", "Passw0rd!", "Jane Doe", null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task AddToCartValidator_RejectsNonPositiveQuantity()
    {
        var result = await ValidateAsync(
            new AddToCartCommandValidator(),
            new AddToCartCommand(Guid.NewGuid(), 0));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task AddToCartValidator_RejectsEmptyProductId()
    {
        var result = await ValidateAsync(
            new AddToCartCommandValidator(),
            new AddToCartCommand(Guid.Empty, 1));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task CreateProductValidator_RejectsNegativePrice()
    {
        var result = await ValidateAsync(
            new CreateProductCommandValidator(),
            new CreateProductCommand("Widget", null, "SKU-1", -1m, Guid.NewGuid()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName.Contains("Price"));
    }

    [Fact]
    public async Task CreateProductValidator_RejectsBlankSku()
    {
        var result = await ValidateAsync(
            new CreateProductCommandValidator(),
            new CreateProductCommand("Widget", null, "  ", 10m, Guid.NewGuid()));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task CreateReviewValidator_RejectsRatingOutsideOneToFive(int rating)
    {
        var result = await ValidateAsync(
            new CreateReviewCommandValidator(),
            new CreateReviewCommand(Guid.NewGuid(), rating, null, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName.Contains("Rating"));
    }

    [Fact]
    public async Task CreateReviewValidator_AcceptsValidReview()
    {
        var result = await ValidateAsync(
            new CreateReviewCommandValidator(),
            new CreateReviewCommand(Guid.NewGuid(), 4, "Title", "Comment"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task PlaceOrderValidator_RejectsMissingShippingAddressFields()
    {
        var result = await ValidateAsync(
            new PlaceOrderCommandValidator(),
            new PlaceOrderCommand(new ShippingAddressDto { RecipientName = string.Empty }));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task PlaceOrderValidator_AcceptsCompleteAddress()
    {
        var result = await ValidateAsync(
            new PlaceOrderCommandValidator(),
            new PlaceOrderCommand(
                new ShippingAddressDto
                {
                    RecipientName = "Jane Doe",
                    PhoneNumber = "+1 555 0100",
                    Line1 = "1 Infinite Loop",
                    City = "Cupertino",
                    PostalCode = "95014",
                    Country = "USA"
                },
                ShippingMethod.Standard));

        result.IsValid.Should().BeTrue();
    }
}

public sealed class ResultTests
{
    [Fact]
    public void Success_CarriesNoError() =>
        Result.Success().Error.Should().Be(Error.None);

    [Fact]
    public void Failure_RequiresAnError() =>
        FluentActions.Invoking(() => Result.Failure(Error.None))
            .Should().Throw<InvalidOperationException>();

    [Fact]
    public void Failure_WithError_CarriesItThrough()
    {
        var error = Error.NotFound("missing");

        var result = Result.Failure(error);

        result.Error.Should().BeSameAs(error);
        result.Error.Message.Should().Be("missing");
    }

    [Fact]
    public void GenericFailure_HasNoValue()
    {
        var result = Result.Failure<Guid>(Error.NotFound("missing"));

        result.IsFailure.Should().BeTrue();
        result.Value.Should().Be(Guid.Empty);
    }

    [Fact]
    public void GenericSuccess_ExposesValue() =>
        Result.Success(42).Value.Should().Be(42);

    [Fact]
    public void FailureError_IsClassifiedForHttpMapping()
    {
        Error.Validation("x").Type.Should().Be(ErrorType.Validation);
        Error.NotFound("x").Type.Should().Be(ErrorType.NotFound);
        Error.Unauthorized("x").Type.Should().Be(ErrorType.Unauthorized);
        Error.Forbidden("x").Type.Should().Be(ErrorType.Forbidden);
        Error.Conflict(ErrorCodes.Conflict, "x").Type.Should().Be(ErrorType.Conflict);
        Error.Failure("x").Type.Should().Be(ErrorType.Unexpected);
    }

    [Fact]
    public void ValidationErrors_ShareTheStandardCode()
    {
        var errors = Error.ValidationErrors(["first", "second"]);

        errors.Should().HaveCount(2);
        errors.Should().OnlyContain(x => x.Code == ErrorCodes.ValidationFailed);
    }

    [Fact]
    public void IsFailure_IsTheInverseOfIsSuccess()
    {
        var ok = Result.Success();
        var bad = Result.Failure(Error.NotFound("x"));

        ok.IsSuccess.Should().BeTrue();
        ok.IsFailure.Should().BeFalse();
        bad.IsSuccess.Should().BeFalse();
        bad.IsFailure.Should().BeTrue();
    }
}

public sealed class PagedRequestTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Constructor_NonPositivePageNumber_Throws(int pageNumber) =>
        FluentActions.Invoking(() => new PagedRequest(pageNumber))
            .Should().Throw<ArgumentOutOfRangeException>();

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Constructor_NonPositivePageSize_Throws(int pageSize) =>
        FluentActions.Invoking(() => new PagedRequest(1, pageSize))
            .Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void Constructor_ClampsPageSizeToMaximum()
    {
        var request = new PagedRequest(1, 5_000);

        request.PageSize.Should().Be(PagedRequest.MaxPageSize);
    }

    [Theory]
    [InlineData(1, 20, 0)]
    [InlineData(2, 20, 20)]
    [InlineData(4, 25, 75)]
    public void Skip_IsCalculatedFromPageNumber(int pageNumber, int pageSize, int expected) =>
        new PagedRequest(pageNumber, pageSize).Skip.Should().Be(expected);

    [Fact]
    public void Defaults_AreFirstPageOfTwenty()
    {
        var request = new PagedRequest();

        request.PageNumber.Should().Be(1);
        request.PageSize.Should().Be(PagedRequest.DefaultPageSize);
    }
}

public sealed class PagedResultTests
{
    private static PagedResult<int> Page(int total, int pageNumber = 1, int pageSize = 10) =>
        new(Enumerable.Range(1, Math.Min(pageSize, total)).ToList(), total, pageNumber, pageSize);

[Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(10, 1)]
    [InlineData(11, 2)]
    [InlineData(25, 3)]
    public void TotalPages_IsCeilingOfTotalOverPageSize(int total, int expected) =>
        Page(total).TotalPages.Should().Be(expected);

    [Fact]
    public void HasPrevious_IsTrueAfterFirstPage()
    {
        Page(30, 1).HasPrevious.Should().BeFalse();
        Page(30, 2).HasPrevious.Should().BeTrue();
    }

    [Fact]
    public void HasNext_IsTrueUntilLastPage()
    {
        Page(30, 2, 10).TotalPages.Should().Be(3);
        Page(30, 2, 10).HasNext.Should().BeTrue();
        Page(30, 3, 10).HasNext.Should().BeFalse();
    }

    [Fact]
    public void Empty_HasNoItemsButKeepsPagingMetadata()
    {
        var request = new PagedRequest(3, 25);

        var empty = PagedResult<string>.Empty(request);

        empty.Items.Should().BeEmpty();
        empty.TotalCount.Should().Be(0);
        empty.PageNumber.Should().Be(3);
        empty.PageSize.Should().Be(25);
        empty.TotalPages.Should().Be(0);
        empty.HasNext.Should().BeFalse();
    }

    [Fact]
    public void Map_ProjectsItemsAndKeepsMetadata()
    {
        var mapped = Page(3, 1, 10).Map(x => x * 2);

        mapped.Items.Should().Equal(2, 4, 6);
        mapped.TotalCount.Should().Be(3);
    }
}