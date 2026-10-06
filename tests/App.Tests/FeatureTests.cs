using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class FeatureTests
{
    [Fact]
    public async Task WebSocketEchoesFragmentedMessage()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var socket = await factory.Server.CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/ws"), CancellationToken.None);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.SendAsync(Encoding.UTF8.GetBytes("hel"), WebSocketMessageType.Text, false, deadline.Token);
        await socket.SendAsync(Encoding.UTF8.GetBytes("lo"), WebSocketMessageType.Text, true, deadline.Token);
        var buffer = new byte[64]; var result = await socket.ReceiveAsync(buffer, deadline.Token);
        Assert.Equal("hello", Encoding.UTF8.GetString(buffer, 0, result.Count));
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", deadline.Token);
    }
    [Fact]
    public async Task ConfigSurvivesApplicationReplacementAndRequiresToken()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "config.json");
        var settings = new Dictionary<string, string?> { ["CONFIG_PATH"] = path, ["API_TOKEN"] = "local-test-only-token" };
        try
        {
            await using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(settings))))
            {
                using var client = factory.CreateClient();
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/data/config", new { theme = "dark" })).StatusCode);
                client.DefaultRequestHeaders.Add("X-Demo-Token", "local-test-only-token");
                Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/data/config", new { theme = "dark", revision = 1 })).StatusCode);
            }
            await using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(settings))))
            {
                using var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Demo-Token", "local-test-only-token");
                var text = await client.GetStringAsync("/api/data/config"); Assert.Contains("dark", text); Assert.Contains("revision", text);
            }
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
