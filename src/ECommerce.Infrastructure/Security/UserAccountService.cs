using ECommerce.Application.Interfaces;
using ECommerce.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace ECommerce.Infrastructure.Security;

/// <summary>
/// Wraps ASP.NET Identity behind a framework-agnostic contract so the Application
/// layer can register, authenticate and update users without referencing Identity.
/// </summary>
public sealed class UserAccountService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    RoleManager<IdentityRole<Guid>> roleManager) : IUserAccountService
{
    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        await userManager.FindByEmailAsync(email) is not null;

    public Task<UserAccountResult> CreateCustomerAsync(
        string email,
        string password,
        string fullName,
        CancellationToken cancellationToken = default) =>
        CreateAsync(email, password, fullName, Roles.Customer);

    public Task<UserAccountResult> CreateAdminAsync(
        string email,
        string password,
        string fullName,
        CancellationToken cancellationToken = default) =>
        CreateAsync(email, password, fullName, Roles.Admin);

    public async Task<UserAccountResult> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
            return UserAccountResult.Fail("Invalid credentials.");

        var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: false);

        if (!result.Succeeded)
            return UserAccountResult.Fail("Invalid credentials.");

        var roles = await userManager.GetRolesAsync(user);

        return UserAccountResult.Success(user.Id, user.Email!, user.FullName, roles.ToList());
    }

    public async Task<UserAccountResult?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
            return null;

        var roles = await userManager.GetRolesAsync(user);
        return UserAccountResult.Success(user.Id, user.Email!, user.FullName, roles.ToList());
    }

    public async Task<bool> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
            return false;

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        return result.Succeeded;
    }

    private async Task<UserAccountResult> CreateAsync(
        string email,
        string password,
        string fullName,
        string role)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = fullName,
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var created = await userManager.CreateAsync(user, password);

        if (!created.Succeeded)
            return UserAccountResult.Fail(created.Errors.Select(x => x.Description).ToList());

        // Registration must work on a brand new database, so the role is created on
        // demand instead of relying on a seeding step having run first.
        await EnsureRoleExistsAsync(role);

        var addedToRole = await userManager.AddToRoleAsync(user, role);

        if (!addedToRole.Succeeded)
            return UserAccountResult.Fail(addedToRole.Errors.Select(x => x.Description).ToList());

        return UserAccountResult.Success(user.Id, user.Email!, user.FullName, [role]);
    }

    private async Task EnsureRoleExistsAsync(string role)
    {
        if (await roleManager.RoleExistsAsync(role))
            return;

        await roleManager.CreateAsync(new IdentityRole<Guid>
        {
            Id = Guid.NewGuid(),
            Name = role,
            NormalizedName = role.ToUpperInvariant()
        });
    }
}