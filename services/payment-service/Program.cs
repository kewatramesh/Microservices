var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Json(new { service = "payment-service", status = "ok" }));

app.MapPost("/payments/authorize", (AuthorizeRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.OrderId) || request.Amount is null)
    {
        return Results.BadRequest(new { error = "orderId and amount required" });
    }

    var approved = request.Amount.Value < 1000;
    if (!approved)
    {
        return Results.Json(new { status = "DECLINED", reason = "Amount exceeds risk threshold" }, statusCode: StatusCodes.Status402PaymentRequired);
    }

    return Results.Json(new { status = "APPROVED", transactionId = $"tx_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}" });
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "4004";
app.Urls.Add($"http://0.0.0.0:{port}");
app.Run();

record AuthorizeRequest(string? OrderId, decimal? Amount);
