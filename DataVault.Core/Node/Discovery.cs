using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace DataVault.Core.Node;

public class Discovery(
    ILogger<Discovery> log,
    IConnection rabbit)
{
    public const string STATUS_QUEUE = "dv.node.status";

    public static readonly TimeSpan ANNOUNCEMENT_INTERVAL = TimeSpan.FromSeconds(30);

    public async Task BeginAnnounce(CancellationToken cancel)
    {
        using var channel = rabbit.CreateModel();

        channel.QueueDeclare(queue: STATUS_QUEUE,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        while (!cancel.IsCancellationRequested)
        {
            const string message = "Hello World!";
            var body = Encoding.UTF8.GetBytes(message);

            channel.BasicPublish(exchange: "",
                routingKey: STATUS_QUEUE,
                basicProperties: null,
                body: body);

            log.LogDebug("Send message: '{}'", message);

            try
            {
                await Task.Delay(ANNOUNCEMENT_INTERVAL, cancel);
            } catch (TaskCanceledException)
            {
            }
        }
    }

    public async Task BeginListen(CancellationToken token)
    {
        using var channel = rabbit.CreateModel();

        channel.QueueDeclare(queue: STATUS_QUEUE,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            log.LogDebug("Receive message: '{}'", message);
        };

        channel.BasicConsume(queue: STATUS_QUEUE,
            autoAck: true,
            consumer: consumer);
    }
}
