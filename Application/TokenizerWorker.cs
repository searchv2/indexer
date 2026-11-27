using System.Diagnostics;
using Indexer.Core.Abstractions;
using Indexer.Core.Messages;

namespace Indexer.Application;

/// <summary>
/// Pipeline stage 2. Takes a <see cref="DocumentDiscovered"/> message, reads the file, extracts its
/// words and passes them on as a <see cref="DocumentTokenized"/> message. Stateless, so any number of
/// tokenizers can consume the same queue.
/// </summary>
public sealed class TokenizerWorker
{
    private readonly IDocumentSource _source;
    private readonly IMessageQueue _queue;

    public TokenizerWorker(IDocumentSource source, IMessageQueue queue)
    {
        _source = source;
        _queue = queue;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await _queue.DeclareAsync(cancellationToken);
        await _queue.ConsumeAsync<DocumentDiscovered>(Queues.Discovered, HandleAsync, cancellationToken);
    }

    public async Task HandleAsync(DocumentDiscovered message, CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var words = WordExtractor.Extract(_source.Read(message.Url))
                .Order(StringComparer.Ordinal) // a fixed order keeps concurrent writers from deadlocking on word rows
                .ToList();
            await _queue.PublishAsync(Queues.Tokenized, new DocumentTokenized(message.Url, message.CreatedAt, words), cancellationToken);
            succeeded = true;
        }
        finally
        {
            IndexerMetrics.RecordDocument("tokenizer", Stopwatch.GetElapsedTime(start), succeeded);
        }
    }
}
