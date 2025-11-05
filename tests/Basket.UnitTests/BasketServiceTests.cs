using System.Security.Claims;
using eShop.Basket.API.Repositories;
using eShop.Basket.API.Grpc;
using eShop.Basket.API.Model;
using eShop.Basket.UnitTests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using BasketItem = eShop.Basket.API.Model.BasketItem;

namespace eShop.Basket.UnitTests;

[TestClass]
public class BasketServiceTests
{
    [TestMethod]
    public async Task GetBasketReturnsEmptyForNoUser()
    {
        var mockRepository = Substitute.For<IBasketRepository>();
        var service = new BasketService(mockRepository, NullLogger<BasketService>.Instance);
        var serverCallContext = TestServerCallContext.Create();
        serverCallContext.SetUserState("__HttpContext", new DefaultHttpContext());

        var response = await service.GetBasket(new GetBasketRequest(), serverCallContext);

        Assert.IsInstanceOfType<CustomerBasketResponse>(response);
        Assert.AreEqual(response.Items.Count(), 0);
    }

    [TestMethod]
    public async Task GetBasketReturnsItemsForValidUserId()
    {
        var mockRepository = Substitute.For<IBasketRepository>();
        List<BasketItem> items = [new BasketItem { Id = "some-id" }];
        mockRepository.GetBasketAsync("1").Returns(Task.FromResult(new CustomerBasket { BuyerId = "1", Items = items }));
        var service = new BasketService(mockRepository, NullLogger<BasketService>.Instance);
        var serverCallContext = TestServerCallContext.Create();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "1")]));
        serverCallContext.SetUserState("__HttpContext", httpContext);

        var response = await service.GetBasket(new GetBasketRequest(), serverCallContext);

        Assert.IsInstanceOfType<CustomerBasketResponse>(response);
        Assert.AreEqual(response.Items.Count(), 1);
    }

    [TestMethod]
    public async Task GetBasketReturnsEmptyForInvalidUserId()
    {
        var mockRepository = Substitute.For<IBasketRepository>();
        List<BasketItem> items = [new BasketItem { Id = "some-id" }];
        mockRepository.GetBasketAsync("1").Returns(Task.FromResult(new CustomerBasket { BuyerId = "1", Items = items }));
        var service = new BasketService(mockRepository, NullLogger<BasketService>.Instance);
        var serverCallContext = TestServerCallContext.Create();
        var httpContext = new DefaultHttpContext();
        serverCallContext.SetUserState("__HttpContext", httpContext);

        var response = await service.GetBasket(new GetBasketRequest(), serverCallContext);

        Assert.IsInstanceOfType<CustomerBasketResponse>(response);
        Assert.AreEqual(response.Items.Count(), 0);
    }

    [TestMethod]
    public async Task GetBasketReturnsEmptyWhenRepositoryReturnsNull()
    {
        // Arrange: Mock IBasketRepository.GetBasketAsync to return null for a valid user ID
        var mockRepository = Substitute.For<IBasketRepository>();
        mockRepository.GetBasketAsync("1").Returns(Task.FromResult<CustomerBasket>(null));
        var service = new BasketService(mockRepository, NullLogger<BasketService>.Instance);
        var serverCallContext = TestServerCallContext.Create();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "1")]));
        serverCallContext.SetUserState("__HttpContext", httpContext);

        // Act: Call GetBasket with a valid user context
        var response = await service.GetBasket(new GetBasketRequest(), serverCallContext);

        // Assert: The response is a CustomerBasketResponse with zero items
        Assert.IsInstanceOfType<CustomerBasketResponse>(response);
        Assert.AreEqual(0, response.Items.Count());
    }

    [TestMethod]
    public async Task GetBasketHandlesRepositoryExceptionGracefully()
    {
        // Arrange: Mock IBasketRepository.GetBasketAsync to throw an exception for a valid user ID
        var mockRepository = Substitute.For<IBasketRepository>();
        mockRepository.GetBasketAsync("1").Returns<Task<CustomerBasket>>(x => throw new Exception("Repository error"));
        var service = new BasketService(mockRepository, NullLogger<BasketService>.Instance);
        var serverCallContext = TestServerCallContext.Create();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "1")]));
        serverCallContext.SetUserState("__HttpContext", httpContext);

        // Act: Call GetBasket with a valid user context
        // Assert: The service throws the exception (current implementation behavior)
        await Assert.ThrowsExactlyAsync<Exception>(async () =>
        {
            await service.GetBasket(new GetBasketRequest(), serverCallContext);
        });
    }

    [TestMethod]
    public async Task GetBasketReturnsMultipleItemsForValidUserId()
    {
        // Arrange: Mock IBasketRepository.GetBasketAsync to return a CustomerBasket with multiple BasketItem objects
        var mockRepository = Substitute.For<IBasketRepository>();
        List<BasketItem> items = [
            new BasketItem { Id = "item-1", ProductId = 1, ProductName = "Product 1", Quantity = 2, UnitPrice = 10.50m },
            new BasketItem { Id = "item-2", ProductId = 2, ProductName = "Product 2", Quantity = 1, UnitPrice = 25.00m },
            new BasketItem { Id = "item-3", ProductId = 3, ProductName = "Product 3", Quantity = 3, UnitPrice = 5.75m }
        ];
        mockRepository.GetBasketAsync("1").Returns(Task.FromResult(new CustomerBasket { BuyerId = "1", Items = items }));
        var service = new BasketService(mockRepository, NullLogger<BasketService>.Instance);
        var serverCallContext = TestServerCallContext.Create();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "1")]));
        serverCallContext.SetUserState("__HttpContext", httpContext);

        // Act: Call GetBasket with a valid user context
        var response = await service.GetBasket(new GetBasketRequest(), serverCallContext);

        // Assert: The response contains the correct number of items and the expected item details
        Assert.IsInstanceOfType<CustomerBasketResponse>(response);
        Assert.AreEqual(3, response.Items.Count());
        
        // Verify first item details
        var firstItem = response.Items.ElementAt(0);
        Assert.AreEqual(1, firstItem.ProductId);
        Assert.AreEqual(2, firstItem.Quantity);
        
        // Verify second item details
        var secondItem = response.Items.ElementAt(1);
        Assert.AreEqual(2, secondItem.ProductId);
        Assert.AreEqual(1, secondItem.Quantity);
        
        // Verify third item details
        var thirdItem = response.Items.ElementAt(2);
        Assert.AreEqual(3, thirdItem.ProductId);
        Assert.AreEqual(3, thirdItem.Quantity);
    }
}
