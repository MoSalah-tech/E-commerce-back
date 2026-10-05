using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ECommerce.IntegrationTests;

/// <summary>Anonymous (unauthenticated) endpoint behaviour: browsing, auth and validation.</summary>
[Collection(ApiCollection.Name)]
public sealed class PublicEndpointTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task GetProducts_Anonymous_ReturnsEnvelopeWithSeededProduct()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var data = (await ApiFactory.ReadAsync(response)).GetProperty("data");
        var items = data.GetProperty("items");

        items.GetArrayLength().Should().BeGreaterThan(0);
        items[0].GetProperty("name").GetString().Should().Be("Studio Headphones");
        data.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GetProductById_UnknownId_ReturnsNotFoundEnvelope()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync($"/api/products/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var body = await ApiFactory.ReadAsync(response);
        body.GetProperty("success").GetBoolean().Should().BeFalse();
        body.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("NOT_FOUND");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetFeaturedProducts_ReturnsTheFeaturedProduct()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/products/featured");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = (await ApiFactory.ReadAsync(response)).GetProperty("data");

        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("name").GetString().Should().Be("Studio Headphones");
        items[0].GetProperty("isFeatured").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetProducts_WithSearchFilter_DoesNotMatchUnrelatedTerms()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/products?search=zzzz-no-such-product");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var data = (await ApiFactory.ReadAsync(response)).GetProperty("data");
        data.GetProperty("totalCount").GetInt32().Should().Be(0);
        data.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetProducts_ClampsInvalidPagingInsteadOfFailing()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/products?pageNumber=0&pageSize=9999");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var data = (await ApiFactory.ReadAsync(response)).GetProperty("data");
        data.GetProperty("pageNumber").GetInt32().Should().Be(1);
        data.GetProperty("pageSize").GetInt32().Should().Be(100);
    }

    [Fact]
    public async Task GetCategories_Anonymous_ReturnsSeededCategory()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = (await ApiFactory.ReadAsync(response)).GetProperty("data");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("slug").GetString().Should().Be("audio");
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorizedWithoutLeakingWhichFieldFailed()
    {
        using var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = ApiFactory.CustomerEmail, password = "Wrong!2345" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body = await ApiFactory.ReadAsync(response);
        body.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("INVALID_CREDENTIALS");
        body.GetProperty("message").GetString().Should().Be("Email or password is incorrect.");
    }

    [Fact]
    public async Task Login_InvalidEmailFormat_IsRejectedByTheValidationPipeline()
    {
        using var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "not-an-email", password = "Customer!2345" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await ApiFactory.ReadAsync(response);
        body.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        using var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = ApiFactory.CustomerEmail,
            password = "Another!2345",
            confirmPassword = "Another!2345",
            fullName = "Impostor",
            phoneNumber = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await ApiFactory.ReadAsync(response);
        body.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("DUPLICATE_EMAIL");
    }

    [Fact]
    public async Task Register_PasswordMismatch_IsRejectedByTheValidationPipeline()
    {
        using var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "fresh@integration.test",
            password = "Fresh!2345",
            confirmPassword = "Different!2345",
            fullName = "Fresh Shopper",
            phoneNumber = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SwaggerDocument_IsServed()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = await ApiFactory.ReadAsync(response);
        document.GetProperty("paths").TryGetProperty("/api/products", out _).Should().BeTrue();
    }
}
