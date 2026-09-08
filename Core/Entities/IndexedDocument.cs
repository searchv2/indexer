namespace Indexer.Core.Entities;

/// <summary>
/// A document that has been written to the index. The indexer assigns <see cref="Id"/>;
/// everything else comes from the document source.
/// </summary>
public sealed class IndexedDocument
{
    public required int Id { get; init; }

    public required string Url { get; init; }

    public required string IndexedAt { get; init; }

    public required string CreatedAt { get; init; }
}
