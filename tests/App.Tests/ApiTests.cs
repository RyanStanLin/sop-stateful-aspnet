using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public sealed class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public ApiTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();
    [Fact]
    public async Task HealthEndpointReturnsOk()
    {
        var response = await _client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    [Fact]
    public async Task ApiUsesServerSideLogic()
    {
        var greeting = await _client.GetFromJsonAsync<GreetingResult>("/api/hello?name=Ryan");
        Assert.Equal("Hello, Ryan!", greeting?.Message);
    }
    [Fact]
    public void BoundsUserInput() => Assert.True(Greeting.Create(new string('a', 500)).Message.Length < 100);
}
