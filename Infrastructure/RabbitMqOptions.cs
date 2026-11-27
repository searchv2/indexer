namespace Indexer.Infrastructure;

/// <summary>
/// Connection settings for the message bus, read from <c>RABBITMQ_HOST</c>, <c>RABBITMQ_USER</c> and
/// <c>RABBITMQ_PASSWORD</c> (the password comes from a Kubernetes Secret). The defaults target a local broker.
/// </summary>
public sealed class RabbitMqOptions
{
    public string Host { get; init; } = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";

    public string User { get; init; } = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest";

    public string Password { get; init; } = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest";

    /// <summary>How many unacknowledged messages one worker may hold; the rest stay in the queue for other workers.</summary>
    public ushort Prefetch { get; init; } = 10;
}
