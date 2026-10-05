using ECommerce.Application.Common;
using ECommerce.Application.Features.Auth;
using ECommerce.Application.Interfaces;
using Moq;

namespace ECommerce.UnitTests.ApplicationTests;

public sealed class AuthCommandHandlerTests
{
    private readonly Mock<IUserAccountService> _userAccounts = new();
    private readonly Mock<ICustomerRepository> _customers = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IAccessTokenGenerator> _tokens = new();
    private readonly FakeEmailSender _email = new();

    private readonly Guid UserId = Guid.NewGuid();
    private readonly DateTime ExpiresAt = new(2026, 12, 31, 23, 59, 0, DateTimeKind.Utc);

    private void SetupTokenGenerator() =>
        _tokens
            .Setup(x => x.Generate(It.IsAny<AccessTokenRequest>()))
            .Returns(new AccessToken("header.payload.signature", ExpiresAt));

    private RegisterCommandHandler CreateRegisterHandler() =>
        new(_userAccounts.Object, _customers.Object, _unitOfWork.Object, _tokens.Object, _email);

    private static RegisterCommand RegisterCommand(string email = "new@example.com") =>
        new(email, "Passw0rd!", "Passw0rd!", "Jane Doe", null);

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        _userAccounts.Setup(x => x.EmailExistsAsync("new@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateRegisterHandler().Handle(RegisterCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be(ErrorCodes.DuplicateEmail);
        _customers.Verify(x => x.Add(It.IsAny<ECommerce.Domain.Entities.Customer>()), Times.Never);
    }

    [Fact]
    public async Task Register_IdentityRejectsPassword_ReturnsValidationFailure()
    {
        _userAccounts.Setup(x => x.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userAccounts.Setup(x => x.CreateCustomerAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserAccountResult.Fail(["Passwords must have at least one digit."]));

        var result = await CreateRegisterHandler().Handle(RegisterCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("at least one digit");
        _customers.Verify(x => x.Add(It.IsAny<ECommerce.Domain.Entities.Customer>()), Times.Never);
    }

    [Fact]
    public async Task Register_HappyPath_CreatesProfileSendsEmailAndReturnsToken()
    {
        _userAccounts.Setup(x => x.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userAccounts.Setup(x => x.CreateCustomerAsync(
                "new@example.com",
                "Passw0rd!",
                "Jane Doe",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserAccountResult.Success(UserId, "new@example.com", "Jane Doe", [Roles.Customer]));
        SetupTokenGenerator();

        var result = await CreateRegisterHandler().Handle(RegisterCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("header.payload.signature");
        result.Value.TokenType.Should().Be("Bearer");
        result.Value.Email.Should().Be("new@example.com");
        result.Value.Roles.Should().Contain(Roles.Customer);

        _customers.Verify(
            x => x.Add(It.Is<ECommerce.Domain.Entities.Customer>(c => c.UserId == UserId && c.Email == "new@example.com")),
            Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _email.WelcomeEmails.Should().ContainSingle().Which.Email.Should().Be("new@example.com");
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        _userAccounts.Setup(x => x.ValidateCredentialsAsync(
                "new@example.com",
                "wrong",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserAccountResult.Fail("nope"));

        var handler = new LoginCommandHandler(_userAccounts.Object, _tokens.Object);

        var result = await handler.Handle(new LoginCommand("new@example.com", "wrong"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be(ErrorCodes.InvalidCredentials);
    }

    [Fact]
    public async Task Login_UnknownEmailAndWrongPassword_ReturnIdenticalMessages()
    {
        _userAccounts.Setup(x => x.ValidateCredentialsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserAccountResult.Fail("nope"));

        var handler = new LoginCommandHandler(_userAccounts.Object, _tokens.Object);

        var unknown = await handler.Handle(new LoginCommand("nobody@example.com", "x"), CancellationToken.None);
        var wrongPassword = await handler.Handle(new LoginCommand("known@example.com", "x"), CancellationToken.None);

        unknown.Error.Message.Should().Be(wrongPassword.Error.Message);
        unknown.Error.Code.Should().Be(wrongPassword.Error.Code);
    }

    [Fact]
    public async Task Login_HappyPath_ReturnsTokenWithRoles()
    {
        _userAccounts.Setup(x => x.ValidateCredentialsAsync(
                "admin@example.com",
                "Passw0rd!",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserAccountResult.Success(UserId, "admin@example.com", "Admin", [Roles.Admin]));
        SetupTokenGenerator();

        var handler = new LoginCommandHandler(_userAccounts.Object, _tokens.Object);

        var result = await handler.Handle(new LoginCommand("admin@example.com", "Passw0rd!"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Roles.Should().ContainSingle().Which.Should().Be(Roles.Admin);
        result.Value.ExpiresAtUtc.Should().Be(ExpiresAt);
        _tokens.Verify(
            x => x.Generate(It.Is<AccessTokenRequest>(r => r.UserId == UserId && r.DisplayName == "Admin")),
            Times.Once);
    }

    [Fact]
    public async Task ChangePassword_AnonymousUser_ReturnsUnauthorized()
    {
        var handler = new ChangePasswordCommandHandler(_userAccounts.Object, FakeCurrentUserService.Anonymous());

        var result = await handler.Handle(new ChangePasswordCommand("old", "New0!", "New0!"), CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        _userAccounts.Verify(
            x => x.ChangePasswordAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_ReturnsValidationFailure()
    {
        _userAccounts.Setup(x => x.ChangePasswordAsync(
                UserId,
                "old",
                "New0!",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new ChangePasswordCommandHandler(
            _userAccounts.Object,
            new FakeCurrentUserService { UserId = UserId });

        var result = await handler.Handle(new ChangePasswordCommand("old", "New0!", "New0!"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("current password");
    }

    [Fact]
    public async Task ChangePassword_HappyPath_ReturnsSuccess()
    {
        _userAccounts.Setup(x => x.ChangePasswordAsync(UserId, "old", "New0!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new ChangePasswordCommandHandler(
            _userAccounts.Object,
            new FakeCurrentUserService { UserId = UserId });

        var result = await handler.Handle(new ChangePasswordCommand("old", "New0!", "New0!"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}