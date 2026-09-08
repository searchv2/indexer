namespace Indexer.Core.Entities;

/// <summary>
/// A document offered by an <see cref="Abstractions.IDocumentSource"/> for indexing, before
/// the indexer has assigned it an id. <see cref="Content"/> is the raw text to tokenize.
/// </summary>
public sealed record SourceDocument(string Url, string CreatedAt, string Content);
