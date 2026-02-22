using System.Collections.Concurrent;
using System.Text;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var orders = new ConcurrentBag<Order>();
var rabbitUrl = Environment.GetEnvironmentVariable("RABBITMQ_URL") ?? "amqp://localhost:5672";
var paymentUrl = Environment.GetEnvironmentVariable("PAYMENT_SERVICE_URL") ?? "http://localhost:4004";
var http = new HttpClient();

IModel? channel = null;
try
{
    var factory = new ConnectionFactory { Uri = new Uri(rabbitUrl) };
    var connection = factory.CreateConnection();
    channel = connection.CreateModel();
    channel.ExchangeDeclare("domain.events", ExchangeType.Topic, durable: false);
}
catch (Exception ex)
{
    Console.WriteLine($"RabbitMQ unavailable; events disabled. {ex.Message}");
}

var breaker = new SimpleCircuitBreaker();

app.MapGet("/health", () => Results.Json(new { service = "order-service", status = "ok" }));

app.MapPost("/orders", async (CreateOrderRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.UserId) || request.Items is null || request.Items.Count == 0)
    {
        return Results.BadRequest(new { error = "userId and items are required" });
    }

    var amount = request.Items.Sum(i => (i.Price ?? 100m) * (i.Quantity ?? 1));
    var order = new Order(Guid.NewGuid().ToString(), request.UserId!, request.Items, amount, "PENDING_PAYMENT");
    orders.Add(order);

    try
    {
        var payment = await breaker.Execute(async () =>
        {
            var response = await http.PostAsJsonAsync($"{paymentUrl}/payments/authorize", new { orderId = order.Id, amount });
            if (!response.IsSuccessStatusCode)
            {
                return new PaymentResponse("DECLINED");
            }

            var body = await response.Content.ReadFromJsonAsync<PaymentResponse>();
            return body ?? new PaymentResponse("DECLINED");
        });

        order.Status = payment.Status switch
        {
            "APPROVED" => "CONFIRMED",
            "PENDING_RETRY" => "PAYMENT_RETRY",
            _ => "PAYMENT_FAILED"
        };

        if (order.Status == "CONFIRMED" && channel is not null)
        {
            var payload = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(order));
            channel.BasicPublish("domain.events", "order.confirmed", body: payload);
        }
    }
    catch
    {
        order.Status = "PAYMENT_FAILED";
    }

    return Results.Json(order, statusCode: StatusCodes.Status201Created);
});

app.MapGet("/orders", () => Results.Json(orders));

var port = Environment.GetEnvironmentVariable("PORT") ?? "4003";
app.Urls.Add($"http://0.0.0.0:{port}");
app.Run();

record CreateOrderRequest(string? UserId, List<OrderItem>? Items);
record OrderItem(string ProductId, int? Quantity, decimal? Price);
record PaymentResponse(string Status);

class Order
{
    public string Id { get; }
    public string UserId { get; }
    public List<OrderItem> Items { get; }
    public decimal Amount { get; }
    public string Status { get; set; }

    public Order(string id, string userId, List<OrderItem> items, decimal amount, string status)
    {
        Id = id;
        UserId = userId;
        Items = items;
        Amount = amount;
        Status = status;
    }
}

class SimpleCircuitBreaker
{
    private int _failures;
    private DateTime _openUntil = DateTime.MinValue;

    public async Task<PaymentResponse> Execute(Func<Task<PaymentResponse>> action)
    {
        if (_openUntil > DateTime.UtcNow)
        {
            return new PaymentResponse("PENDING_RETRY");
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var task = action();
            var done = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token));
            if (done != task)
            {
                throw new TimeoutException();
            }

            _failures = 0;
            return await task;
        }
        catch
        {
            _failures++;
            if (_failures >= 3)
            {
                _openUntil = DateTime.UtcNow.AddSeconds(5);
            }

            return new PaymentResponse("PENDING_RETRY");
        }
    }
}
