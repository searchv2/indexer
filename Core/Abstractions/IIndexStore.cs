using Indexer.Core.Entities;

namespace Indexer.Core.Abstractions;

/// <summary>
/// The port through which the indexer writes documents, words and word/document
/// occurrences. Infrastructure supplies the implementation (SQLite, or an in-memory
/// stand-in).
/// </summary>
public interface IIndexStore
{
    /// <summary>All indexed words, keyed by word value, valued by word id.</summary>
    Dictionary<string, int> GetAllWords();

    /// <summary>The number of documents currently in the store.</summary>
    int DocumentCount { get; }

    void InsertDocument(IndexedDocument doc);

    /// <summary>
    /// Insert a word with id <c>v</c> and value <c>k</c> for each entry <c>(k, v)</c> in
    /// <paramref name="words"/>.
    /// </summary>
    void InsertAllWords(Dictionary<string, int> words);

    void InsertAllOccurrences(int docId, ISet<int> wordIds);
}
