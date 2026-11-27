namespace Indexer.Core.Abstractions;

/// <summary>
/// The port through which pipeline writers store one document at a time. Unlike
/// <see cref="IIndexStore"/> the database assigns the ids, so several writers can run at once.
/// </summary>
public interface IDocumentWriter
{
    /// <summary>Drops and recreates the index tables. Run once, before a new crawl starts.</summary>
    void ResetSchema();

    /// <summary>
    /// Stores the document and its word occurrences in one transaction. Idempotent: a document with
    /// the same <paramref name="url"/> replaces the earlier one, so a redelivered message is harmless.
    /// </summary>
    void Write(string url, string createdAt, IReadOnlyCollection<string> words);

    int DocumentCount { get; }
}
