using Indexer.Core.Entities;

namespace Indexer.Core.Abstractions;

/// <summary>
/// Supplies the documents to be indexed. Keeps directory walking and file reading out of
/// the domain, so a crawl can be driven from a fake source in tests.
/// </summary>
public interface IDocumentSource
{
    /// <summary>Every document currently available to index, streamed lazily.</summary>
    IEnumerable<SourceDocument> EnumerateDocuments();

    /// <summary>Every document available to index, without reading its content (used by the crawler stage).</summary>
    IEnumerable<DocumentReference> EnumerateReferences();

    /// <summary>Reads the text of one document found by <see cref="EnumerateReferences"/> (used by the tokenizer stage).</summary>
    string Read(string url);
}
