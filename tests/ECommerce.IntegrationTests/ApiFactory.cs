using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.ValueObjects;
using ECommerce.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ECommerce.IntegrationTests;

/// <summary>
/// Boots the real API pipeline in memory. Only the database is substituted
/// (SQLite instead of SQL Server) so routing, JWT auth, validation, the MediatR
/// pipeline and the EF mappings are all exercised for real.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@integration.test";
    public const string AdminPassword = "Admin!2345";
    public const string CustomerEmail = "customer@integration.test";
    public const string CustomerPassword = "Customer!2345";

    private const string SigningKey =
        "integration-test-signing-key-that-is-long-enough-for-hmac-sha256";

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"ecommerce-tests-{Guid.NewGuid():N}.db");

    public Guid CategoryId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid CustomerId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ApplyMigrationsOnStartup"] = "false",
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
                ["Jwt:Issuer"] = "ECommerce.IntegrationTests",
                ["Jwt:Audience"] = "ECommerce.IntegrationTests.Client",
                ["Jwt:SigningKey"] = SigningKey,
                ["Jwt:ExpiryMinutes"] = "60"
            }));

        builder.ConfigureServices(services =>
        {
            RemoveSqlServerProvider(services);

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={_databasePath}"));
        });
    }

    /// <summary>
    /// EF Core only supports one provider per service provider, and it registers a
    /// provider's services as soon as <c>UseSqlServer</c> is evaluated while the
    /// options are built. The application's own <c>AddDbContext</c> configuration
    /// action therefore has to be removed, not just the resulting options.
    /// </summary>
    private static void RemoveSqlServerProvider(IServiceCollection services)
    {
        services.RemoveAll<DbContextOptions<AppDbContext>>();
        services.RemoveAll<AppDbContext>();
        services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        await ResetAsync();
    }

    /// <summary>
    /// Empties every table and re-seeds the deterministic fixtures, so each test
    /// starts from the same catalogue, accounts and customer profile regardless of
    /// the order xUnit happens to run them in.
    /// </summary>
    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<AppDbContext>();

        await TruncateAllTablesAsync(db);

        var category = new Category("Audio", "Headphones and speakers", null, 1);
        db.Categories.Add(category);

        var product = new Product(
            "Studio Headphones",
            "STUDIO-1",
            Money.From(79.99m),
            category.Id,
            "Reference quality over-ear headphones.",
            "https://cdn.example.com/headphones.png",
            stockQuantity: 25);

        product.SetFeatured(true);
        db.Products.Add(product);

        await db.SaveChangesAsync();

        CategoryId = category.Id;
        ProductId = product.Id;

        var accounts = provider.GetRequiredService<IUserAccountService>();
        var customers = provider.GetRequiredService<ICustomerRepository>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();

        var customerAccount = await accounts.CreateCustomerAsync(CustomerEmail, CustomerPassword, "Integration Shopper");
        customerAccount.Succeeded.Should().BeTrue(string.Join("; ", customerAccount.Errors));

        var profile = new Customer(customerAccount.UserId, CustomerEmail, "Integration Shopper");
        customers.Add(profile);
        await unitOfWork.SaveChangesAsync();

        CustomerId = profile.Id;

        var adminAccount = await accounts.CreateAdminAsync(AdminEmail, AdminPassword, "Integration Admin");
        adminAccount.Succeeded.Should().BeTrue(string.Join("; ", adminAccount.Errors));
    }

    /// <summary>
    /// Deletes every row in the mapped tables. Foreign keys are disabled for the
    /// duration so the order of the deletes does not matter.
    /// </summary>
    private static async Task TruncateAllTablesAsync(AppDbContext db)
    {
        await db.Database.OpenConnectionAsync();

        try
        {
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");

            foreach (var table in db.Model.GetEntityTypes()
                         .Select(entityType => entityType.GetTableName())
                         .Where(name => name is not null)
                         .Distinct())
            {
                // The table names come from the EF model metadata, never from input.
#pragma warning disable EF1002
                await db.Database.ExecuteSqlRawAsync($"DELETE FROM \"{table}\";");
#pragma warning restore EF1002
            }
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
            await db.Database.CloseConnectionAsync();
        }
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }

    /// <summary>Returns a client that already carries a valid customer bearer token.</summary>
    public async Task<HttpClient> CreateAuthenticatedCustomerClientAsync()
    {
        var client = CreateClient();
        await AuthenticateAsync(client, CustomerEmail, CustomerPassword);
        return client;
    }

    /// <summary>Returns a client that already carries a valid admin bearer token.</summary>
    public async Task<HttpClient> CreateAuthenticatedAdminClientAsync()
    {
        var client = CreateClient();
        await AuthenticateAsync(client, AdminEmail, AdminPassword);
        return client;
    }

    public static async Task AuthenticateAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });

        response.EnsureSuccessStatusCode();

        var token = (await ReadAsync(response)).GetProperty("data").GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>Reads the uniform <c>{ success, data, message }</c> envelope.</summary>
    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}

/// <summary>xUnit fixture that shares a single booted API across the whole test class.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

/// <summary>
/// Base class for the integration tests. xUnit builds a new instance of the test
/// class for every test method, so resetting the shared database in
/// <see cref="IAsyncLifetime.InitializeAsync"/> gives each test a clean slate.
/// </summary>
public abstract class ApiTestBase(ApiFactory factory) : IAsyncLifetime
{
    protected ApiFactory Factory { get; } = factory;

    public Task InitializeAsync() => Factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}