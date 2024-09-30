using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace DataVault.Core.Node;

public class Discovery(
    ILogger<Discovery> log,
    IConnection rabbit)
{
    public const string STATUS_EXCHANGE = "dv.node.status";

    public static readonly TimeSpan ANNOUNCEMENT_INTERVAL = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan CONFIRM_TIMEOUT = TimeSpan.FromSeconds(5);

    public async Task BeginAnnounce(CancellationToken cancel)
    {
        using var channel = rabbit.CreateModel();

        channel.ExchangeDeclare(STATUS_EXCHANGE, ExchangeType.Fanout);
        channel.ConfirmSelect();

        while (!cancel.IsCancellationRequested)
        {
            try
            {
                const string message = "Hello World!";
                var body = Encoding.UTF8.GetBytes(message);

                channel.BasicPublish(STATUS_EXCHANGE, "", null, body);
                channel.WaitForConfirmsOrDie(CONFIRM_TIMEOUT);

                log.LogDebug("Send message: '{}'", message);
            } catch (Exception ex)
            {
                log.LogError(ex, "Failed to broadcast status update");
            }

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

        channel.ExchangeDeclare(STATUS_EXCHANGE, ExchangeType.Fanout);

        var queue = channel.QueueDeclare();
        channel.QueueBind(queue, STATUS_EXCHANGE, "");

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (_, e) =>
        {
            try
            {
                var body = e.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                log.LogDebug("Receive message: '{}'", message);
            } catch (Exception ex)
            {
                log.LogDebug(ex, "Failed to receive status update");
            }
        };
        var consumerTag = channel.BasicConsume(queue, true, consumer);

        await Task.Run(token.WaitHandle.WaitOne);

        channel.BasicCancel(consumerTag);
    }
}
