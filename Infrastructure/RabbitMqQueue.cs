using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Indexer.Core.Abstractions;
using Indexer.Core.Messages;

namespace Indexer.Infrastructure;

/// <summary>
/// <see cref="IMessageQueue"/> on RabbitMQ. Queues are durable, messages persistent, and publishing waits
/// for the broker's confirmation. A message whose handler throws is requeued once (after a short pause) and
/// then dead-lettered into <see cref="Queues.Failed"/>.
/// </summary>
public sealed class RabbitMqQueue : IMessageQueue, IAsyncDisposable
{
    private static readonly TimeSpan RetryPause = TimeSpan.FromSeconds(1);

    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqQueue(RabbitMqOptions options) => _options = options;

    public async Task DeclareAsync(CancellationToken cancellationToken)
    {
        var channel = await GetChannelAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            foreach (var queue in new[] { Queues.Failed, Queues.Discovered, Queues.Tokenized })
                await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
                    arguments: ArgumentsFor(queue), cancellationToken: cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task PurgeAsync(string queue, CancellationToken cancellationToken)
    {
        var channel = await GetChannelAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try { await channel.QueuePurgeAsync(queue, cancellationToken); }
        finally { _gate.Release(); }
    }

    public async Task PublishAsync<T>(string queue, T message, CancellationToken cancellationToken)
    {
        var channel = await GetChannelAsync(cancellationToken);
        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await channel.BasicPublishAsync(exchange: "", routingKey: queue, mandatory: false,
                basicProperties: new BasicProperties { Persistent = true, ContentType = "application/json" },
                body: body, cancellationToken: cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task<int> CountAsync(string queue, CancellationToken cancellationToken)
    {
        var channel = await GetChannelAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try { return (int)(await channel.QueueDeclarePassiveAsync(queue, cancellationToken)).MessageCount; }
        finally { _gate.Release(); }
    }

    public async Task<int> ConsumerCountAsync(string queue, CancellationToken cancellationToken)
    {
        var channel = await GetChannelAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try { return (int)(await channel.QueueDeclarePassiveAsync(queue, cancellationToken)).ConsumerCount; }
        finally { _gate.Release(); }
    }

    public async Task ConsumeAsync<T>(string queue, Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken)
    {
        await using var channel = await (await GetConnectionAsync(cancellationToken)).CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.BasicQosAsync(0, _options.Prefetch, global: false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<T>(delivery.Body.Span)
                    ?? throw new InvalidDataException("Empty message.");
                await handler(message, cancellationToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Shutting down: leave the message unacknowledged so the broker redelivers it.
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"{queue}: {(delivery.Redelivered ? "giving up" : "will retry")}: {ex.Message}");
                await Task.Delay(RetryPause, CancellationToken.None);
                // First failure: requeue once. Second failure: requeue=false sends it to the dead-letter queue.
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: !delivery.Redelivered, CancellationToken.None);
            }
        };

        await channel.BasicConsumeAsync(queue, autoAck: false, consumer, cancellationToken);
        try { await Task.Delay(Timeout.Infinite, cancellationToken); }
        catch (OperationCanceledException) { }
    }

    // Both work queues dead-letter into the failed queue; the failed queue itself is plain.
    private static Dictionary<string, object?>? ArgumentsFor(string queue) =>
        queue == Queues.Failed
            ? null
            : new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = Queues.Failed,
            };

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
            return _channel;
        _channel = await (await GetConnectionAsync(cancellationToken)).CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        return _channel;
    }

    // The broker may still be starting when a worker starts, so the first connection is retried.
    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
            return _connection;

        var factory = new ConnectionFactory { HostName = _options.Host, UserName = _options.User, Password = _options.Password };
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return _connection = await factory.CreateConnectionAsync(cancellationToken);
            }
            catch (Exception ex) when (attempt < 30 && ex is not OperationCanceledException)
            {
                Console.Error.WriteLine($"RabbitMQ at {_options.Host} is not ready ({ex.GetType().Name}); retry {attempt}/30.");
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
    }
}
