using ECommerce.Api.Contracts;
using ECommerce.Api.Extensions;
using ECommerce.Application.Features.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

/// <summary>Registration, login and self-service password management.</summary>
[Route("api/auth")]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new RegisterCommand(
                request.Email,
                request.Password,
                request.ConfirmPassword,
                request.FullName,
                request.PhoneNumber),
            cancellationToken);

        return result.ToActionResult(this, value => Ok(ApiResponse.Ok(value, "Account created.")));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new LoginCommand(request.Email, request.Password),
            cancellationToken);

        return result.ToActionResult(this);
    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new ChangePasswordCommand(request.CurrentPassword, request.NewPassword, request.ConfirmPassword),
            cancellationToken);

        return result.ToActionResult(this, Ok(ApiResponse.Ok(message: "Password changed.")));
    }
}