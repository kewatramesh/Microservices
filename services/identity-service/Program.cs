using System.Collections.Concurrent;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var users = new ConcurrentDictionary<string, User>();

app.MapGet("/health", () => Results.Json(new { service = "identity-service", status = "ok" }));

app.MapPost("/auth/register", (RegisterRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { error = "email and password required" });
    }

    if (users.Values.Any(u => u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)))
    {
        return Results.Conflict(new { error = "user already exists" });
    }

    var user = new User(Guid.NewGuid().ToString(), request.Email, request.Password);
    users[user.Id] = user;
    return Results.Json(new { id = user.Id, email = user.Email }, statusCode: StatusCodes.Status201Created);
});

app.MapPost("/auth/login", (RegisterRequest request) =>
{
    var user = users.Values.FirstOrDefault(u =>
        u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase) && u.Password == request.Password);

    if (user is null)
    {
        return Results.Json(new { error = "invalid credentials" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var accessToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user.Id}:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"));
    return Results.Json(new { accessToken, tokenType = "Bearer" });
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "4001";
app.Urls.Add($"http://0.0.0.0:{port}");
app.Run();

record RegisterRequest(string Email, string Password);
record User(string Id, string Email, string Password);
