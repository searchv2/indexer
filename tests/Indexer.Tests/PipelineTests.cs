using Indexer.Application;
using Indexer.Core.Messages;
using Indexer.Infrastructure;

namespace Indexer.Tests;

[Collection("Indexer metrics are process-wide")]
public class PipelineTests
{
    [Fact]
    public async Task Tokenizer_publishes_the_distinct_normalised_words_in_a_fixed_order()
    {
        var source = new FakeSource();
        source.Files["/docs/a.txt"] = "Banana apple BANANA cherry";
        var queue = new FakeQueue();

        await new TokenizerWorker(source, queue).HandleAsync(new DocumentDiscovered("/docs/a.txt", "2026-01-01"), default);

        var (name, message) = Assert.Single(queue.Published);
        Assert.Equal(Queues.Tokenized, name);
        var tokenized = Assert.IsType<DocumentTokenized>(message);
        Assert.Equal("/docs/a.txt", tokenized.Url);
        Assert.Equal(new[] { "apple", "banana", "cherry" }, tokenized.Words);
    }

    [Fact]
    public async Task Tokenizer_fails_for_a_missing_file_so_the_message_can_be_dead_lettered()
    {
        var tokenizer = new TokenizerWorker(new FakeSource(), new FakeQueue());
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            tokenizer.HandleAsync(new DocumentDiscovered("/docs/missing.txt", "x"), default));
    }

    [Fact]
    public async Task Writer_stores_the_document_with_its_words()
    {
        var writer = new FakeWriter();

        await new WriterWorker(writer, new FakeQueue())
            .HandleAsync(new DocumentTokenized("/docs/a.txt", "2026-01-01", new[] { "apple", "banana" }), default);

        var written = Assert.Single(writer.Written);
        Assert.Equal("/docs/a.txt", written.Url);
        Assert.Equal(new[] { "apple", "banana" }, written.Words.Order().ToArray());
    }

    [Fact]
    public async Task Pipeline_resets_the_index_before_publishing_and_waits_until_every_document_is_stored()
    {
        var source = new FakeSource();
        for (var i = 0; i < 3; i++) source.Files[$"/docs/{i}.txt"] = "x";
        var queue = new FakeQueue();
        var writer = new FakeWriter();
        var pipeline = new IndexingPipeline(source, queue, writer, new PipelineOptions { PollInterval = TimeSpan.FromMilliseconds(5) });

        // Stands in for the workers: stores one document every few milliseconds.
        var workers = Task.Run(async () =>
        {
            for (var i = 0; i < 3; i++) { await Task.Delay(20); Interlocked.Increment(ref writer.Count); }
        });
        var result = await pipeline.RunAsync(_ => { }, default);
        await workers;

        Assert.Equal(3, result.Published);
        Assert.Equal(3, result.Indexed);
        Assert.Equal(0, result.Failed);
        Assert.Equal(new[] { "reset" }, writer.Calls);
        // The index is reset after the queues are purged and before the first message is published.
        Assert.True(queue.Calls.IndexOf($"purge {Queues.Failed}") < queue.Calls.IndexOf($"publish {Queues.Discovered}"));
    }

    [Fact]
    public async Task Pipeline_counts_dead_lettered_documents_as_processed()
    {
        var source = new FakeSource();
        source.Files["/docs/a.txt"] = "x";
        source.Files["/docs/b.txt"] = "x";
        var queue = new FakeQueue { Counts = { [Queues.Failed] = 1 } };
        var writer = new FakeWriter { Count = 1 };

        var result = await new IndexingPipeline(source, queue, writer, new PipelineOptions()).RunAsync(_ => { }, default);

        Assert.Equal(1, result.Indexed);
        Assert.Equal(1, result.Failed);
    }

    [Fact]
    public async Task Pipeline_fails_instead_of_waiting_forever_when_no_worker_makes_progress()
    {
        var source = new FakeSource();
        source.Files["/docs/a.txt"] = "x";
        var options = new PipelineOptions { PollInterval = TimeSpan.FromMilliseconds(5), StallTimeout = TimeSpan.FromMilliseconds(60) };

        var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
            new IndexingPipeline(source, new FakeQueue(), new FakeWriter(), options).RunAsync(_ => { }, default));

        Assert.Contains("0 indexed", ex.Message);
    }

    [Fact]
    public void File_source_refuses_to_read_outside_the_document_folder()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(folder, "inside.txt"), "hello");
        var source = new FileSystemDocumentSource(new IndexerOptions { Folder = folder });

        Assert.Equal("hello", source.Read(Path.Combine(folder, "inside.txt")));
        Assert.Throws<InvalidOperationException>(() => source.Read("/etc/hostname"));
        Assert.Throws<InvalidOperationException>(() => source.Read(Path.Combine(folder, "..", "outside.txt")));
        Assert.Single(source.EnumerateReferences());
    }
}
