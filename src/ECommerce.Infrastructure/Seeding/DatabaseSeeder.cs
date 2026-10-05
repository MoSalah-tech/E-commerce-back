using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.ValueObjects;
using ECommerce.Infrastructure.Identity;
using ECommerce.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Infrastructure.Seeding;

/// <summary>
/// Idempotent development seed: roles, an administrator account and a small
/// catalogue so a fresh database is immediately explorable through Swagger.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        await SeedRolesAsync(provider);
        await SeedAdministratorsAsync(provider);
        await SeedCatalogueAsync(provider, cancellationToken);
    }

    private static async Task SeedRolesAsync(IServiceProvider provider)
    {
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var role in Roles.All)
        {
            if (await roleManager.RoleExistsAsync(role))
                continue;

            var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create role '{role}': " + string.Join("; ", result.Errors.Select(x => x.Description)));
            }
        }
    }

    private static async Task SeedAdministratorsAsync(IServiceProvider provider)
    {
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        const string email = "admin@ecommerce.com";
        const string password = "Admin!2345";

        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            var created = await userManager.CreateAsync(
                new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = "Store Administrator"
                },
                password);

            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to seed administrator '{email}': " +
                    string.Join("; ", created.Errors.Select(x => x.Description)));
            }

            user = await userManager.FindByEmailAsync(email);
        }

        if (user is null)
            throw new InvalidOperationException($"Failed to load administrator '{email}' after creation.");

        if (!await userManager.IsInRoleAsync(user, Roles.Admin))
            await userManager.AddToRoleAsync(user, Roles.Admin);
    }

    private static async Task SeedCatalogueAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        var context = provider.GetRequiredService<AppDbContext>();

        if (await context.Categories.AnyAsync(cancellationToken))
            return;

        var electronics = new Category("Electronics", "Phones, laptops and audio.", displayOrder: 1);
        var home = new Category("Home & Kitchen", "Everyday appliances and decor.", displayOrder: 2);
        var books = new Category("Books", "Fiction and non-fiction.", displayOrder: 3);

        context.Categories.AddRange(electronics, home, books);
        await context.SaveChangesAsync(cancellationToken);

        context.Products.AddRange(
            Build("Wireless Headphones", "WH-1000XM5", 299.99m, 24, electronics.Id),
            Build("Mechanical Keyboard", "KB-RGB-87", 149.50m, 40, electronics.Id),
            Build("4K Action Camera", "CAM-4K-01", 349.00m, 0, electronics.Id),
            Build("Espresso Machine", "ESM-2000", 599.99m, 6, home.Id),
            Build("Cast Iron Skillet", "SK-12IN", 39.95m, 80, home.Id),
            Build("Clean Architecture", "BK-CLEAN-01", 44.99m, 120, books.Id));

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Product Build(string name, string sku, decimal price, int stock, Guid categoryId)
    {
        var product = new Product(
            name,
            sku,
            new Money(price),
            categoryId,
            $"{name} description.",
            imageUrl: null,
            stockQuantity: stock);

        product.SetLowStockThreshold(5);
        return product;
    }
}