using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace WireMockSample.Tests;

public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly WireMockServer _mockServer;

    const int operationId = 1;
    private readonly List<Operation> _operations = [new() { Id = operationId, Date = DateTime.Now.Date, EirCode = "EIRCODE" }];

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            });
        });

        _client = _factory.CreateClient();
        _mockServer = WireMockServer.Start(4000);
    }

    [Fact]
    public async Task GetOperations_ShouldReturnOkWithListOfOperations()
    {
        var operationsExpected = new List<Operation>();

        var response = await _client.GetAsync("/domain-request");

        response.Should().Be200Ok().And.BeAs(_operations);
    }

    [Fact]
    public async Task GetOperation_ShouldReturnOkWithOperation()
    {
        var operationExpected = _operations.Find(x => x.Id == operationId);

        var response = await _client.GetAsync($"/domain-request/{operationId}");

        response.Should().Be200Ok().And.BeAs(operationExpected);
    }

    [Fact]
    public async Task GetOperation_ShouldReturnNotFound_WhenOperationDoesNotExist()
    {
        var response = await _client.GetAsync("/domain-request/999");

        response.Should().Be404NotFound();
    }

    [Fact]
    public async Task PostOperation_ShouldReturnCreated()
    {
        var addressApiResponse = new AddressResponse() { Id = 1, Street = "Street 1", EirCode = "EIRCODE" };
        var newOperation = new Operation { Id = 2, Date = DateTime.Now.Date, EirCode = "EIRCODE" };

        _mockServer.Given(
            Request
                .Create()
                .WithPath($"/eircode/{newOperation.EirCode}")
                .UsingGet()
                .WithHeader("x-api-key", "test-api-key")
        ).RespondWith(
            Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBodyAsJson(addressApiResponse)
        );

        var response = await _client.PostAsJsonAsync("/domain-request", newOperation);

        response.Should().Be201Created();
    }

    [Fact]
    public async Task PostOperation_ShouldBadRequest_WhenAddressApiFetchReturnsEmpty()
    {
        var addressApiResponse = new AddressResponse();
        var newOperation = new Operation { Id = 2, Date = DateTime.Now.Date, EirCode = "EIRCODE" };

        _mockServer.Given(
            Request
                .Create()
                .WithPath($"/eircode/{newOperation.EirCode}")
                .UsingGet()
                .WithHeader("x-api-key", "test-api-key")
        ).RespondWith(
            Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBodyAsJson(addressApiResponse)
        );

        var response = await _client.PostAsJsonAsync("/domain-request", newOperation);

        response.Should().Be400BadRequest().And.MatchInContent("*Eircode does not exist.*");
    }

    [Fact]
    public async Task PostOperation_ShouldReturnServiceUnavailable_WhenAddressApiFetchFails()
    {
        var newOperation = new Operation { Id = 2, Date = DateTime.Now.Date, EirCode = "EIRCODE" };

        _mockServer.Given(
            Request
                .Create()
                .WithPath($"/eircode/{newOperation.EirCode}")
                .UsingGet()
                .WithHeader("x-api-key", "test-api-key")
        ).RespondWith(
            Response.Create()
                .WithStatusCode(HttpStatusCode.InternalServerError)
                .WithBodyAsJson(newOperation)
        );

        var response = await _client.PostAsJsonAsync("/domain-request", newOperation);

        response.Should().Be503ServiceUnavailable();
    }

    [Fact]
    public async Task PostOperation_ShouldReturnBadRequest_WhenInvalidFutureDate()
    {
        var invalidOperation = new Operation { Id = 3, Date = DateTime.Now.Date.AddDays(1), EirCode = "EIRCODE" };

        var response = await _client.PostAsJsonAsync("/domain-request", invalidOperation);

        response.Should().Be400BadRequest()
            .And.MatchInContent("*Operation date cannot be greater than today.*");
    }

    [Fact]
    public async Task PostOperation_ShouldReturnBadRequest_WhenInvalidOldDate()
    {
        var invalidOperation = new Operation { Id = 4, Date = DateTime.Now.Date.AddYears(-2), EirCode = "EIRCODE" };

        var response = await _client.PostAsJsonAsync("/domain-request", invalidOperation);

        response.Should().Be400BadRequest()
            .And.MatchInContent("*Operation date cannot be less than a year ago.*");
    }

    [Fact]
    public async Task DeleteOperation_ShouldReturnOk()
    {
        var response = await _client.DeleteAsync($"/domain-request/{operationId}");

        response.Should().Be200Ok();
    }

    [Fact]
    public async Task DeleteOperation_ShouldReturnNotFound_WhenOperationDoesNotExist()
    {
        var response = await _client.DeleteAsync("/domain-request/999");

        response.Should().Be404NotFound();
    }

    public void Dispose()
    {
        _mockServer.Stop();
        _mockServer.Dispose();
    }
}
