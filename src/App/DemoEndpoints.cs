using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using StackExchange.Redis;

public static class DemoEndpoints
{
    public static void MapDemo(this WebApplication app)
    {
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
        app.Map("/ws", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var buffer = new byte[64 * 1024];
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var length = 0; WebSocketReceiveResult received;
                    do
                    {
                        received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), context.RequestAborted);
                        if (received.MessageType == WebSocketMessageType.Close)
                        {
                            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", context.RequestAborted); return;
                        }
                        length += received.Count;
                        if (length == buffer.Length && !received.EndOfMessage)
                        {
                            await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "64 KiB limit", context.RequestAborted); return;
                        }
                    } while (!received.EndOfMessage);
                    await socket.SendAsync(new ArraySegment<byte>(buffer, 0, length), received.MessageType, true, context.RequestAborted);
                }
            }
            catch (Exception exception) when (exception is WebSocketException or OperationCanceledException) { }
        });
        var data = app.MapGroup("/api/data").AddEndpointFilter(async (context, next) =>
        {
            var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var expected = configuration["API_TOKEN"];
            var actual = context.HttpContext.Request.Headers["X-Demo-Token"].ToString();
            if (string.IsNullOrEmpty(expected) || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)), SHA256.HashData(Encoding.UTF8.GetBytes(actual))))
                return Results.Json(new { error = "demo_token_required" }, statusCode: 401);
            try { return await next(context); }
            catch (Exception exception) when (exception is DataUnavailableException or NpgsqlException or RedisException or IOException or TimeoutException)
            { return Results.Json(new { error = "data_service_unavailable" }, statusCode: 503); }
        });
        data.MapPut("/postgres/{key}", async (string key, Item item, DemoData store, CancellationToken cancellation) =>
        {
            if (!Valid(key, item.Value)) return Results.BadRequest();
            await store.PutDatabaseAsync(key, item.Value, cancellation); return Results.Ok(new { key, item.Value });
        });
        data.MapGet("/postgres/{key}", async (string key, DemoData store, CancellationToken cancellation) =>
        { var value = await store.GetDatabaseAsync(key, cancellation); return Results.Ok(new { key, value, found = value is not null }); });
        data.MapPut("/redis/{key}", async (string key, Item item, DemoData store) =>
        {
            if (!Valid(key, item.Value)) return Results.BadRequest();
            await store.PutRedisAsync(key, item.Value); return Results.Ok(new { key, item.Value });
        });
        data.MapGet("/redis/{key}", async (string key, DemoData store) =>
        { var value = await store.GetRedisAsync(key); return Results.Ok(new { key, value, found = value is not null }); });
        data.MapPut("/config", async (JsonElement value, DemoData store, CancellationToken cancellation) =>
        {
            if (Encoding.UTF8.GetByteCount(value.GetRawText()) > 65536) return Results.BadRequest();
            await store.WriteConfigAsync(value, cancellation); return Results.Ok(new { saved = true });
        });
        data.MapGet("/config", async (DemoData store, CancellationToken cancellation) =>
        { var value = await store.ReadConfigAsync(cancellation); return Results.Ok(new { found = value is not null, value }); });
    }
    private static bool Valid(string key, string value) => key.Length is > 0 and <= 64 && value.Length <= 4096;
}
public sealed record Item(string Value);
