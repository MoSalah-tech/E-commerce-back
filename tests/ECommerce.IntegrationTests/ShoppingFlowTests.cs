using System.Net;
using System.Net.Http.Json;

namespace ECommerce.IntegrationTests;

/// <summary>Cart and checkout flow for an authenticated shopper, end to end through EF.</summary>
[Collection(ApiCollection.Name)]
public sealed class ShoppingFlowTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Cart_RequiresAuthentication()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/cart");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddToCart_ThenGetCart_PersistsAcrossRequests()
    {
        using var client = await Factory.CreateAuthenticatedCustomerClientAsync();

        await client.DeleteAsync("/api/cart");

        var addResponse = await client.PostAsJsonAsync(
            "/api/cart/items",
            new { productId = Factory.ProductId, quantity = 2 });

        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var added = (await ApiFactory.ReadAsync(addResponse)).GetProperty("data");
        added.GetProperty("total").GetDecimal().Should().Be(159.98m);
        added.GetProperty("linesCount").GetInt32().Should().Be(1);

        var cartResponse = await client.GetAsync("/api/cart");
        var cart = (await ApiFactory.ReadAsync(cartResponse)).GetProperty("data");

        cart.GetProperty("items")[0].GetProperty("quantity").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task AddToCart_UnknownProduct_ReturnsNotFound()
    {
        using var client = await Factory.CreateAuthenticatedCustomerClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/cart/items",
            new { productId = Guid.NewGuid(), quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddToCart_ZeroQuantity_IsRejectedByValidation()
    {
        using var client = await Factory.CreateAuthenticatedCustomerClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/cart/items",
            new { productId = Factory.ProductId, quantity = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateCartItem_UnknownLine_ReturnsNotFound()
    {
        using var client = await Factory.CreateAuthenticatedCustomerClientAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/cart/items/{Guid.NewGuid()}",
            new { quantity = 3 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveFromCart_UnknownLine_ReturnsNotFound()
    {
        using var client = await Factory.CreateAuthenticatedCustomerClientAsync();

        var response = await client.DeleteAsync($"/api/cart/items/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PlaceOrder_WithEmptyCart_ReturnsConflict()
    {
        using var client = await Factory.CreateAuthenticatedCustomerClientAsync();

        await client.DeleteAsync("/api/cart");

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            shippingAddress = ValidAddress(),
            shippingMethod = "Standard",
            markAsPaid = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await ApiFactory.ReadAsync(response);
        body.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("EMPTY_CART");
    }

    [Fact]
    public async Task PlaceOrder_IncompleteShippingAddress_IsRejectedByValidation()
    {
        using var client = await Factory.CreateAuthenticatedCustomerClientAsync();

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            shippingAddress = new
            {
                recipientName = "",
                phoneNumber = "",
                line1 = "",
                city = "",
                postalCode = "",
                country = ""
            },
            shippingMethod = "Standard",
            markAsPaid = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PlaceOrder_HappyPath_CreatesOrderReservesStockAndEmptiesTheCart()
    {
        using var client = await Factory.CreateAuthenticatedCustomerClientAsync();

        await client.DeleteAsync("/api/cart");

        await client.PostAsJsonAsync("/api/cart/items", new { productId = Factory.ProductId, quantity = 2 });

        using var anonymous = Factory.CreateClient();

        var stockBefore = (await ApiFactory.ReadAsync(
                await anonymous.GetAsync($"/api/products/{Factory.ProductId}")))
            .GetProperty("data").GetProperty("stockQuantity").GetInt32();

        var orderResponse = await client.PostAsJsonAsync("/api/orders", new
        {
            shippingAddress = ValidAddress(),
            shippingMethod = "Standard",
            markAsPaid = true
        });

        orderResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var location = orderResponse.Headers.Location;
        location.Should().NotBeNull();

        var order = (await ApiFactory.ReadAsync(orderResponse)).GetProperty("data");

        order.GetProperty("orderNumber").GetString().Should().StartWith("ORD-");
        order.GetProperty("subTotal").GetDecimal().Should().Be(159.98m);
        order.GetProperty("paymentStatus").GetString().Should().Be("Paid");
        order.GetProperty("items")[0].GetProperty("quantity").GetInt32().Should().Be(2);

        // The order id is the route value of the Location header.
        var orderId = ExtractId(location!.ToString());

        // The cart is emptied as part of checkout.
        var cart = (await ApiFactory.ReadAsync(await client.GetAsync("/api/cart"))).GetProperty("data");
        cart.GetProperty("totalQuantity").GetInt32().Should().Be(0);

        // Stock was reserved by the same unit of work that persisted the order.
        var product = (await ApiFactory.ReadAsync(await anonymous.GetAsync($"/api/products/{Factory.ProductId}")))
            .GetProperty("data");
        product.GetProperty("stockQuantity").GetInt32().Should().Be(stockBefore - 2);
        product.GetProperty("isInStock").GetBoolean().Should().BeTrue();

        // And the shopper can read their own order back.
        var ordersResponse = await client.GetAsync("/api/orders");
        ordersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var orders = (await ApiFactory.ReadAsync(ordersResponse)).GetProperty("data");
        orders.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetOrderById_UnknownId_ReturnsNotFound()
    {
        using var client = await Factory.CreateAuthenticatedCustomerClientAsync();

        var response = await client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelOrder_AfterCheckout_SucceedsAndReturnsStock()
    {
        using var client = await Factory.CreateAuthenticatedCustomerClientAsync();

        using var anonymous = Factory.CreateClient();

        var before = (await ApiFactory.ReadAsync(await anonymous.GetAsync($"/api/products/{Factory.ProductId}")))
            .GetProperty("data").GetProperty("stockQuantity").GetInt32();

        var orderId = await CheckoutAsync(client);

        var cancelResponse = await client.PostAsync($"/api/orders/{orderId}/cancel", null);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = (await ApiFactory.ReadAsync(await anonymous.GetAsync($"/api/products/{Factory.ProductId}")))
            .GetProperty("data").GetProperty("stockQuantity").GetInt32();

        after.Should().Be(before);
    }

    [Fact]
    public async Task PlaceReview_ForPurchasedProduct_IsStoredAndVisible()
    {
        using var client = await Factory.CreateAuthenticatedCustomerClientAsync();

        var orderId = await CheckoutAsync(client);

        // Reviewing unlocks on delivery, so staff walk the order through its lifecycle.
        using var admin = await Factory.CreateAuthenticatedAdminClientAsync();

        foreach (var status in new[] { "Confirmed", "Shipped", "Delivered" })
        {
            var fulfil = await admin.PostAsJsonAsync($"/api/orders/{orderId}/status", new { status });

            fulfil.StatusCode.Should().Be(HttpStatusCode.OK, $"expected the order to become {status}");
        }

        var response = await client.PostAsJsonAsync("/api/reviews", new
        {
            productId = Factory.ProductId,
            rating = 5,
            title = "Excellent",
            comment = "Comfortable for long sessions."
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var anonymous = Factory.CreateClient();
        var reviews = (await ApiFactory.ReadAsync(await anonymous.GetAsync($"/api/reviews?productId={Factory.ProductId}")))
            .GetProperty("data");

        reviews.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        reviews.GetProperty("items")[0].GetProperty("rating").GetInt32().Should().Be(5);
    }

    /// <summary>Fills the cart with the seeded product, checks out and returns the new order id.</summary>
    private async Task<string> CheckoutAsync(HttpClient client, int quantity = 1)
    {
        await client.DeleteAsync("/api/cart");
        await client.PostAsJsonAsync("/api/cart/items", new { productId = Factory.ProductId, quantity });

        var orderResponse = await client.PostAsJsonAsync("/api/orders", new
        {
            shippingAddress = ValidAddress(),
            shippingMethod = "Standard",
            markAsPaid = true
        });

        orderResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        orderResponse.Headers.Location.Should().NotBeNull();

        return ExtractId(orderResponse.Headers.Location!.ToString());
    }

    [Fact]
    public async Task CreateReview_InvalidRating_IsRejectedByValidation()
    {
        using var client = await Factory.CreateAuthenticatedCustomerClientAsync();

        var response = await client.PostAsJsonAsync("/api/reviews", new
        {
            productId = Factory.ProductId,
            rating = 9,
            title = (string?)null,
            comment = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static object ValidAddress() => new
    {
        recipientName = "Jane Doe",
        phoneNumber = "+1 555 0100",
        line1 = "1 Infinite Loop",
        line2 = (string?)null,
        city = "Cupertino",
        state = (string?)null,
        postalCode = "95014",
        country = "USA"
    };

    /// <summary>Pulls the trailing GUID out of a Location header such as <c>/api/orders/{id}</c>.</summary>
    private static string ExtractId(string location)
    {
        var trimmed = location.Trim('/');
        var segments = trimmed.Split('/');
        return segments[^1];
    }
}
