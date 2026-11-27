using System.Diagnostics;
using Indexer.Core.Abstractions;
using Indexer.Core.Messages;

namespace Indexer.Application;

/// <summary>
/// Pipeline stage 3. Stores each <see cref="DocumentTokenized"/> message in the index. The write is
/// idempotent, so a message that is delivered twice does not create a second document.
/// </summary>
public sealed class WriterWorker
{
    private readonly IDocumentWriter _writer;
    private readonly IMessageQueue _queue;

    public WriterWorker(IDocumentWriter writer, IMessageQueue queue)
    {
        _writer = writer;
        _queue = queue;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await _queue.DeclareAsync(cancellationToken);
        await _queue.ConsumeAsync<DocumentTokenized>(Queues.Tokenized, HandleAsync, cancellationToken);
    }

    public Task HandleAsync(DocumentTokenized message, CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            _writer.Write(message.Url, message.CreatedAt, message.Words.ToHashSet());
            succeeded = true;
        }
        finally
        {
            IndexerMetrics.RecordDocument("writer", Stopwatch.GetElapsedTime(start), succeeded);
        }
        return Task.CompletedTask;
    }
}
