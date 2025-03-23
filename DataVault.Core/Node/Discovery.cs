using DataVault.Ef.Models;
using DataVault.Ef.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace DataVault.Core.Node;

public class Discovery(
    ILogger<Discovery> log,
    IOptionsMonitor<AppSettings> appSettings,
    NodeIdentity identity,
    PeerRepo peerRepo,
    IConnection rabbit)
{
    public const string STATUS_EXCHANGE = "dv.node.status";

    public static readonly TimeSpan ANNOUNCEMENT_INTERVAL = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan CONFIRM_TIMEOUT = TimeSpan.FromSeconds(5);

    private static string CreateMessage(StatusMessageType type, object body)
    {
        return $"{type};{JsonSerializer.Serialize(body)}";
    }

    public async Task BeginAnnounce(CancellationToken cancel)
    {
        using var channel = await rabbit.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(STATUS_EXCHANGE, ExchangeType.Fanout);
        //TODO What is the replacement for this?
        //channel.ConfirmSelect();

        while (!cancel.IsCancellationRequested)
        {
            try
            {
                var content = new AnnounceIdentityDto(
                    identity.Identity,
                    appSettings.CurrentValue.SelfAddress,
                    appSettings.CurrentValue.SelfPort,
                    identity.Status);

                var message = CreateMessage(StatusMessageType.AnnounceIdentity, content);
                var body = Encoding.UTF8.GetBytes(message);

                log.LogDebug("Sending message: '{}'", message);

                await channel.BasicPublishAsync(STATUS_EXCHANGE, "", body);
                //TODO What is the replacement for this?
                //channel.WaitForConfirmsOrDie(CONFIRM_TIMEOUT);

                log.LogDebug("Sent");
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

    private async Task HandleReceiveAnnounceIdentity(AnnounceIdentityDto content)
    {
        if (content.Identity.Id == identity.Identity.Id)
        {
            log.LogDebug("Ignoring announcement from self");
            return;
        }

        await peerRepo.UpdatePeerStatus(content);
    }

    public async Task BeginListen(CancellationToken token)
    {
        using var channel = await rabbit.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(STATUS_EXCHANGE, ExchangeType.Fanout);

        var queue = await channel.QueueDeclareAsync();
        await channel.QueueBindAsync(queue, STATUS_EXCHANGE, "");

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, e) =>
        {
            try
            {
                var body = e.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                log.LogDebug("Received message: '{}'", message);

                var messageParts = message.Split(";", 2);
                if (messageParts.Length != 2)
                {
                    throw new ApplicationException("Expected message type name and body content");
                }

                switch (Enum.TryParse<StatusMessageType>(messageParts[0], out var _v) ? _v : default)
                {
                    case StatusMessageType.AnnounceIdentity:
                        {
                            var content = JsonSerializer.Deserialize<AnnounceIdentityDto>(messageParts[1]);
                            await HandleReceiveAnnounceIdentity(content!);
                        }
                        break;
                    default:
                        throw new ApplicationException($"Unexpected message type '{messageParts[0]}'");
                }

                log.LogDebug("Processed message");
            } catch (Exception ex)
            {
                log.LogDebug(ex, "Failed to receive status update");
            }
        };
        var consumerTag = await channel.BasicConsumeAsync(queue, true, consumer);

        await Task.Run(token.WaitHandle.WaitOne);

        await channel.BasicCancelAsync(consumerTag);
    }
}
