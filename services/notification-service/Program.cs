using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHostedService<NotificationConsumer>();

var app = builder.Build();
app.MapGet("/health", () => Results.Json(new { service = "notification-service", status = "ok" }));

var port = Environment.GetEnvironmentVariable("PORT") ?? "4005";
app.Urls.Add($"http://0.0.0.0:{port}");
app.Run();

class NotificationConsumer : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rabbitUrl = Environment.GetEnvironmentVariable("RABBITMQ_URL") ?? "amqp://localhost:5672";

        try
        {
            var factory = new ConnectionFactory { Uri = new Uri(rabbitUrl) };
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();

            channel.ExchangeDeclare("domain.events", ExchangeType.Topic, durable: false);
            channel.QueueDeclare("notification.order.confirmed", durable: false, exclusive: false, autoDelete: false);
            channel.QueueBind("notification.order.confirmed", "domain.events", "order.confirmed");

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (_, args) =>
            {
                var message = Encoding.UTF8.GetString(args.Body.ToArray());
                Console.WriteLine($"Notification sent for order event: {message}");
                channel.BasicAck(args.DeliveryTag, multiple: false);
            };

            channel.BasicConsume("notification.order.confirmed", autoAck: false, consumer);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not connect to RabbitMQ: {ex.Message}");
        }

        return Task.CompletedTask;
    }
}
