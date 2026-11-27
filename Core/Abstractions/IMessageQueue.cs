namespace Indexer.Core.Abstractions;

/// <summary>
/// The port to the message bus that connects the indexing stages. Queues are durable and
/// messages persistent. A message that a handler cannot process is retried once and then
/// moved to the dead-letter queue (<see cref="Messages.Queues.Failed"/>).
/// </summary>
public interface IMessageQueue
{
    /// <summary>Creates the queues if they do not exist yet. Safe to call from every stage.</summary>
    Task DeclareAsync(CancellationToken cancellationToken);

    /// <summary>Removes every waiting message from <paramref name="queue"/>.</summary>
    Task PurgeAsync(string queue, CancellationToken cancellationToken);

    /// <summary>Publishes one message and returns when the broker has confirmed it.</summary>
    Task PublishAsync<T>(string queue, T message, CancellationToken cancellationToken);

    /// <summary>
    /// Handles messages from <paramref name="queue"/> one at a time until cancelled. A message is
    /// acknowledged only after <paramref name="handler"/> returns, so a crashed worker's message is
    /// delivered to another worker.
    /// </summary>
    Task ConsumeAsync<T>(string queue, Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken);

    /// <summary>The number of messages waiting in <paramref name="queue"/> (not counting unacknowledged ones).</summary>
    Task<int> CountAsync(string queue, CancellationToken cancellationToken);

    /// <summary>The number of workers currently consuming <paramref name="queue"/>.</summary>
    Task<int> ConsumerCountAsync(string queue, CancellationToken cancellationToken);
}
