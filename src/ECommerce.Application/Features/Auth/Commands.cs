using ECommerce.Application.Common;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using MediatR;

namespace ECommerce.Application.Features.Auth;

public sealed record RegisterCommand(
    string Email,
    string Password,
    string ConfirmPassword,
    string FullName,
    string? PhoneNumber) : IRequest<Result<AuthResponse>>;

public sealed class RegisterCommandHandler(
    IUserAccountService userAccounts,
    ICustomerRepository customers,
    IUnitOfWork unitOfWork,
    IAccessTokenGenerator tokenGenerator,
    IEmailSender emailSender) : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (await userAccounts.EmailExistsAsync(request.Email, cancellationToken))
        {
            return Result.Failure<AuthResponse>(
                Error.Conflict(ErrorCodes.DuplicateEmail, $"An account already exists for '{request.Email}'."));
        }

        var account = await userAccounts.CreateCustomerAsync(
            request.Email,
            request.Password,
            request.FullName,
            cancellationToken);

        if (!account.Succeeded)
            return Result.Failure<AuthResponse>(Error.Validation(string.Join(" ", account.Errors)));

        var customer = new Customer(account.UserId, account.Email, request.FullName, request.PhoneNumber);
        customers.Add(customer);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailSender.SendWelcomeEmailAsync(customer.Email, customer.FullName, cancellationToken);

        var token = tokenGenerator.Generate(
            new AccessTokenRequest(account.UserId, account.Email, account.FullName, account.Roles));

        return Result.Success(AuthResponse.From(account, token));
    }
}

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<AuthResponse>>;

public sealed class LoginCommandHandler(
    IUserAccountService userAccounts,
    IAccessTokenGenerator tokenGenerator) : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var account = await userAccounts.ValidateCredentialsAsync(
            request.Email,
            request.Password,
            cancellationToken);

        if (!account.Succeeded)
        {
            // Deliberately identical message for unknown email and wrong password.
            return Result.Failure<AuthResponse>(
                Error.Unauthorized(ErrorCodes.InvalidCredentials, "Email or password is incorrect."));
        }

        var token = tokenGenerator.Generate(
            new AccessTokenRequest(account.UserId, account.Email, account.FullName, account.Roles));

        return Result.Success(AuthResponse.From(account, token));
    }
}

public sealed record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword) : IRequest<Result>;

public sealed class ChangePasswordCommandHandler(
    IUserAccountService userAccounts,
    ICurrentUserService currentUser) : IRequestHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure(Error.Unauthorized("You must be signed in to change your password."));

        var changed = await userAccounts.ChangePasswordAsync(
            userId,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        return changed
            ? Result.Success()
            : Result.Failure(Error.Validation("The current password is incorrect."));
    }
}