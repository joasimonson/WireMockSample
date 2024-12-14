using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AddressApiSettings>(builder.Configuration.GetSection("AddressApi"));

builder.Services.AddHttpClient("AddressApi", (sp, client) =>
{
    var apiSettings = sp.GetRequiredService<IOptions<AddressApiSettings>>().Value;
    client.BaseAddress = new Uri(apiSettings.BaseUrl);
    client.DefaultRequestHeaders.Add("x-api-key", apiSettings.ApiKey);
});

var app = builder.Build();

var operations = new List<Operation>() { new() { Id = 1, Date = DateTime.Now.Date, EirCode = "EIRCODE" } };

app.MapGet("/domain-request", () => Results.Ok(operations));

app.MapGet("/domain-request/{id:int}", (int id) =>
{
    var operation = operations.Find(item => item.Id == id);
    return operation is not null ? Results.Ok(operation) : Results.NotFound();
});

app.MapPost("/domain-request", async (Operation operation, IHttpClientFactory httpClientFactory) =>
{
    if (operation.Date > DateTime.Now.Date)
        return Results.BadRequest("Operation date cannot be greater than today.");

    if (operation.Date < DateTime.Now.Date.AddYears(-1))
        return Results.BadRequest(error: "Operation date cannot be less than a year ago.");

    var apiUrl = $"eircode/{operation.EirCode}";
    var httpClient = httpClientFactory.CreateClient("AddressApi");

    try
    {
        var response = await httpClient.GetFromJsonAsync<AddressResponse>(apiUrl);
        if (response?.Id == 0)
            return Results.BadRequest("Eircode does not exist.");

        operations.Add(operation);
    }
    catch (Exception)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Created($"/domain-request/{operation.Id}", operation);
});

app.MapDelete("/domain-request/{id:int}", (int id) =>
{
    var deletedCount = operations.RemoveAll(item => item.Id == id);
    return deletedCount > 0 ? Results.Ok() : Results.NotFound();
});

app.Run();

public partial class Program { }