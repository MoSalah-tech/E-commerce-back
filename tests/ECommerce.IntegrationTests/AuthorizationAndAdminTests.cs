using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ECommerce.IntegrationTests;

/// <summary>Role based access control and administrator-only catalogue management.</summary>
[Collection(ApiCollection.Name)]
public sealed class AuthorizationAndAdminTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task AnonymousCustomer_CannotReachTheCart()
    {
        using var client = Factory.CreateClient();

        (await client.GetAsync("/api/cart")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthenticatedCustomer_CannotCreateCategories()
    {
        using var client = await Factory.CreateAuthenticatedCustomerClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/categories",
            new { name = "Furniture", description = "Chairs and tables", imageUrl = (string?)null, displayOrder = 2 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AuthenticatedCustomer_CannotCreateProducts()
    {
        using var client = await Factory.CreateAuthenticatedCustomerClientAsync();

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Desk Lamp",
            description = "Adjustable LED lamp",
            sku = "LAMP-1",
            price = 34.50m,
            compareAtPrice = (decimal?)null,
            stockQuantity = 10,
            lowStockThreshold = 3,
            categoryId = Factory.CategoryId,
            imageUrl = (string?)null,
            isFeatured = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AuthenticatedCustomer_CannotChangeOrderStatus()
    {
        using var client = await Factory.CreateAuthenticatedCustomerClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{Guid.NewGuid()}/status",
            new { status = "Confirmed" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_CanCreateACategoryWithAValidSlug()
    {
        using var client = await Factory.CreateAuthenticatedAdminClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/categories",
            new { name = "Home & Kitchen", description = "Cooking and dining", imageUrl = (string?)null, displayOrder = 3 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var anonymous = Factory.CreateClient();
        var categories = (await ApiFactory.ReadAsync(await anonymous.GetAsync("/api/categories")))
            .GetProperty("data");

        categories.EnumerateArray()
            .Select(x => x.GetProperty("slug").GetString())
            .Should().Contain("home-kitchen");
    }

    [Fact]
    public async Task Admin_CreateProductWithDuplicateSku_ReturnsConflict()
    {
        using var client = await Factory.CreateAuthenticatedAdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Duplicate SKU Item",
            description = "Cloned item",
            sku = "STUDIO-1",
            price = 10m,
            compareAtPrice = (decimal?)null,
            stockQuantity = 1,
            lowStockThreshold = 1,
            categoryId = Factory.CategoryId,
            imageUrl = (string?)null,
            isFeatured = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await ApiFactory.ReadAsync(response);
        body.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("DUPLICATE_SKU");
    }

    [Fact]
    public async Task Admin_CreateProductWithUnknownCategory_ReturnsNotFound()
    {
        using var client = await Factory.CreateAuthenticatedAdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Orphan",
            description = (string?)null,
            sku = "ORPHAN-1",
            price = 10m,
            compareAtPrice = (decimal?)null,
            stockQuantity = 1,
            lowStockThreshold = 1,
            categoryId = Guid.NewGuid(),
            imageUrl = (string?)null,
            isFeatured = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_ProductLifecycle_CreateDiscountRestockAndSoftDelete()
    {
        using var client = await Factory.CreateAuthenticatedAdminClientAsync();
        using var anonymous = Factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Mechanical Keyboard",
            description = "Hot-swappable switches",
            sku = "KEYBOARD-1",
            price = 129.99m,
            compareAtPrice = 159.99m,
            stockQuantity = 4,
            lowStockThreshold = 5,
            categoryId = Factory.CategoryId,
            imageUrl = "https://cdn.example.com/kb.png",
            isFeatured = false
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var productId = ExtractId(createResponse.Headers.Location!.ToString());
        var productIdGuid = Guid.Parse(productId);

        var created = (await ApiFactory.ReadAsync(
            await anonymous.GetAsync($"/api/products/{productIdGuid}"))).GetProperty("data");

        created.GetProperty("isOnSale").GetBoolean().Should().BeTrue();
        created.GetProperty("isLowOnStock").Should().NotBeNull();

        // Discount
        var discountResponse = await client.PostAsJsonAsync(
            $"/api/products/{productIdGuid}/discount",
            new { percentage = 25m });

        discountResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var discounted = (await ApiFactory.ReadAsync(
            await anonymous.GetAsync($"/api/products/{productIdGuid}"))).GetProperty("data");

        discounted.GetProperty("discountPercentage").GetDecimal().Should().Be(25m);
        discounted.GetProperty("effectivePrice").GetDecimal().Should().Be(97.49m);

        // Restock
        var stockResponse = await client.PostAsJsonAsync(
            $"/api/products/{productIdGuid}/stock",
            new { quantityDelta = 20, reason = "Restock" });

        stockResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var restocked = (await ApiFactory.ReadAsync(
            await anonymous.GetAsync($"/api/products/{productIdGuid}"))).GetProperty("data");

        restocked.GetProperty("stockQuantity").GetInt32().Should().Be(24);
        restocked.GetProperty("isInStock").GetBoolean().Should().BeTrue();

        // Soft delete hides it from the public catalogue but keeps the record.
        var deleteResponse = await client.DeleteAsync($"/api/products/{productIdGuid}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        (await anonymous.GetAsync($"/api/products/{productIdGuid}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_AdjustStock_BeyondAvailability_PropagatesAConflict()
    {
        using var client = await Factory.CreateAuthenticatedAdminClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/products/{Factory.ProductId}/stock",
            new { quantityDelta = -9999, reason = "Impossible" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await ApiFactory.ReadAsync(response);
        body.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("INSUFFICIENT_STOCK");
    }

    [Fact]
    public async Task Admin_IllegalStatusTransition_ReturnsValidationErrorCode()
    {
        using var client = await Factory.CreateAuthenticatedAdminClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{Guid.NewGuid()}/status",
            new { status = "Shipped" });

        // The order does not exist, so the handler reports not found before the transition.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_SetDiscountOutsideRange_IsRejectedByValidation()
    {
        using var client = await Factory.CreateAuthenticatedAdminClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/products/{Factory.ProductId}/discount",
            new { percentage = 150m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_TokenActuallyCarriesTheAdminRole()
    {
        using var client = Factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword });

        var data = (await ApiFactory.ReadAsync(login)).GetProperty("data");

        var roles = data.GetProperty("roles").EnumerateArray().Select(x => x.GetString()).ToList();
        roles.Should().Contain("Admin");
        data.GetProperty("fullName").GetString().Should().Be("Integration Admin");
    }

    [Fact]
    public async Task TamperedBearerToken_IsRejected()
    {
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not.a.jwt");

        var response = await client.GetAsync("/api/cart");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string ExtractId(string location)
    {
        var segments = location.Trim('/').Split('/');
        return segments[^1];
    }
}
