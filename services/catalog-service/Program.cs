using System.Collections.Concurrent;
using System.Text.Json;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var products = new ConcurrentBag<Product>();
var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? "redis://localhost:6379";
IDatabase? cache = null;

try
{
    var parsed = redisUrl.Replace("redis://", string.Empty);
    var mux = await ConnectionMultiplexer.ConnectAsync(parsed);
    cache = mux.GetDatabase();
}
catch
{
    Console.WriteLine("Redis unavailable; running without cache.");
}

app.MapGet("/health", () => Results.Json(new { service = "catalog-service", status = "ok" }));

app.MapPost("/catalog/products", async (ProductRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || request.Price is null)
    {
        return Results.BadRequest(new { error = "name and numeric price required" });
    }

    var product = new Product(Guid.NewGuid().ToString(), request.Name!, request.Price.Value);
    products.Add(product);

    if (cache is not null)
    {
        await cache.KeyDeleteAsync("catalog:all");
    }

    return Results.Json(product, statusCode: StatusCodes.Status201Created);
});

app.MapGet("/catalog/products", async () =>
{
    if (cache is not null)
    {
        var cached = await cache.StringGetAsync("catalog:all");
        if (cached.HasValue)
        {
            var parsed = JsonSerializer.Deserialize<List<Product>>(cached!);
            return Results.Json(new { source = "cache", data = parsed });
        }
    }

    var all = products.ToArray();
    if (cache is not null)
    {
        await cache.StringSetAsync("catalog:all", JsonSerializer.Serialize(all), TimeSpan.FromSeconds(30));
    }

    return Results.Json(new { source = "service", data = all });
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "4002";
app.Urls.Add($"http://0.0.0.0:{port}");
app.Run();

record ProductRequest(string? Name, decimal? Price);
record Product(string Id, string Name, decimal Price);
