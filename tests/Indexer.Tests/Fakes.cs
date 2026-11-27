using System.Collections.Concurrent;
using Indexer.Core.Abstractions;
using Indexer.Core.Entities;

namespace Indexer.Tests;

/// <summary>An in-memory bus that records what was published and answers counts for the pipeline.</summary>
internal sealed class FakeQueue : IMessageQueue
{
    public ConcurrentQueue<(string Queue, object Message)> Published { get; } = new();
    public List<string> Calls { get; } = new();
    public Dictionary<string, int> Counts { get; } = new();
    public Dictionary<string, int> Consumers { get; } = new();

    public Task DeclareAsync(CancellationToken cancellationToken) { Calls.Add("declare"); return Task.CompletedTask; }
    public Task PurgeAsync(string queue, CancellationToken cancellationToken) { Calls.Add($"purge {queue}"); return Task.CompletedTask; }

    public Task PublishAsync<T>(string queue, T message, CancellationToken cancellationToken)
    {
        Calls.Add($"publish {queue}");
        Published.Enqueue((queue, message!));
        return Task.CompletedTask;
    }

    public Task ConsumeAsync<T>(string queue, Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<int> CountAsync(string queue, CancellationToken cancellationToken) =>
        Task.FromResult(Counts.GetValueOrDefault(queue));

    public Task<int> ConsumerCountAsync(string queue, CancellationToken cancellationToken) =>
        Task.FromResult(Consumers.GetValueOrDefault(queue));
}

internal sealed class FakeWriter : IDocumentWriter
{
    public List<(string Url, string CreatedAt, string[] Words)> Written { get; } = new();
    public List<string> Calls { get; } = new();
    public int Count;

    public void ResetSchema() => Calls.Add("reset");
    public void Write(string url, string createdAt, IReadOnlyCollection<string> words) =>
        Written.Add((url, createdAt, words.ToArray()));
    public int DocumentCount => Volatile.Read(ref Count);
}

internal sealed class FakeSource : IDocumentSource
{
    public Dictionary<string, string> Files { get; } = new();

    public IEnumerable<SourceDocument> EnumerateDocuments() =>
        Files.Select(f => new SourceDocument(f.Key, "2026-01-01", f.Value));
    public IEnumerable<DocumentReference> EnumerateReferences() =>
        Files.Keys.Select(k => new DocumentReference(k, "2026-01-01"));
    public string Read(string url) => Files[url];
}
