using Indexer.Core.Abstractions;
using Indexer.Core.Messages;

namespace Indexer.Application;

/// <summary>How the pipeline run polls for progress.</summary>
public sealed record PipelineOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>The run fails if no document is stored or dead-lettered for this long.</summary>
    public TimeSpan StallTimeout { get; init; } = TimeSpan.FromMinutes(2);
}

/// <summary>What a pipeline run did.</summary>
public sealed record PipelineResult(int Published, int Indexed, int Failed, TimeSpan Elapsed);

/// <summary>
/// Pipeline stage 1 and the coordinator of a full reindex: resets the index, publishes one
/// <see cref="Core.Messages.DocumentDiscovered"/> message per file, and waits until the tokenizer and
/// writer stages have stored or dead-lettered every one of them. It does not read any file itself.
/// </summary>
public sealed class IndexingPipeline
{
    private readonly IDocumentSource _source;
    private readonly IMessageQueue _queue;
    private readonly IDocumentWriter _writer;
    private readonly PipelineOptions _options;

    public IndexingPipeline(IDocumentSource source, IMessageQueue queue, IDocumentWriter writer, PipelineOptions options)
    {
        _source = source;
        _queue = queue;
        _writer = writer;
        _options = options;
    }

    public async Task<PipelineResult> RunAsync(Action<string> log, CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;

        await _queue.DeclareAsync(cancellationToken);
        foreach (var queue in new[] { Queues.Discovered, Queues.Tokenized, Queues.Failed })
            await _queue.PurgeAsync(queue, cancellationToken);
        _writer.ResetSchema();

        var published = 0;
        foreach (var reference in _source.EnumerateReferences())
        {
            await _queue.PublishAsync(Queues.Discovered, new DocumentDiscovered(reference.Url, reference.CreatedAt), cancellationToken);
            published++;
        }
        log($"Published {published} documents to {Queues.Discovered}; waiting for the workers.");

        var lastProgress = DateTime.UtcNow;
        var lastProcessed = -1;
        var lastLogged = DateTime.UtcNow;
        while (true)
        {
            var indexed = _writer.DocumentCount;
            var failed = await _queue.CountAsync(Queues.Failed, cancellationToken);
            var processed = indexed + failed;
            if (processed >= published)
                return new PipelineResult(published, indexed, failed, DateTime.UtcNow - start);

            if (processed != lastProcessed)
            {
                lastProcessed = processed;
                lastProgress = DateTime.UtcNow;
            }
            else if (DateTime.UtcNow - lastProgress > _options.StallTimeout)
            {
                throw new TimeoutException(
                    $"No progress for {_options.StallTimeout}: {indexed} indexed, {failed} failed of {published}. " +
                    "Are the tokenizer and writer workers running?");
            }

            if (DateTime.UtcNow - lastLogged >= TimeSpan.FromSeconds(5))
            {
                lastLogged = DateTime.UtcNow;
                log($"{indexed} indexed, {failed} failed of {published}.");
            }
            await Task.Delay(_options.PollInterval, cancellationToken);
        }
    }
}
