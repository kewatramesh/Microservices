using System.Net;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var httpClient = new HttpClient();
var routes = new Dictionary<string, string>
{
    ["/auth"] = Environment.GetEnvironmentVariable("IDENTITY_SERVICE_URL") ?? "http://localhost:4001",
    ["/catalog"] = Environment.GetEnvironmentVariable("CATALOG_SERVICE_URL") ?? "http://localhost:4002",
    ["/orders"] = Environment.GetEnvironmentVariable("ORDER_SERVICE_URL") ?? "http://localhost:4003"
};

app.MapGet("/health", () => Results.Json(new { service = "api-gateway", status = "ok" }));

foreach (var route in routes)
{
    var prefix = route.Key;
    var target = route.Value;
    app.MapMethods($"{prefix}/{{**rest}}", new[] { "GET", "POST", "PUT", "PATCH", "DELETE" },
        async (HttpContext context, string? rest) =>
        {
            var path = string.IsNullOrEmpty(rest) ? prefix : $"{prefix}/{rest}";
            var upstream = $"{target}{path}{context.Request.QueryString}";
            var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), upstream);

            if (context.Request.ContentLength > 0)
            {
                request.Content = new StreamContent(context.Request.Body);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            }

            try
            {
                using var response = await httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();
                context.Response.StatusCode = (int)response.StatusCode;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(body);
            }
            catch
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
                await context.Response.WriteAsJsonAsync(new { error = "Upstream service unavailable" });
            }
        });
}

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");
app.Run();
