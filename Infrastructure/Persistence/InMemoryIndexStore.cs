using Indexer.Core.Abstractions;
using Indexer.Core.Entities;

namespace Indexer.Infrastructure.Persistence;

/// <summary>
/// In-memory stand-in for <see cref="SqliteIndexStore"/>. Writes go into plain collections
/// so a crawl can be run and observed (document/word counts) without touching disk. Swap
/// the DI registration in <c>DependencyInjection</c> back to <see cref="SqliteIndexStore"/>
/// to persist the index for SearchAPI.
/// </summary>
public sealed class InMemoryIndexStore : IIndexStore
{
    private readonly Dictionary<string, int> _words = new();
    private readonly List<IndexedDocument> _documents = new();

    public void InsertDocument(IndexedDocument doc) => _documents.Add(doc);

    public void InsertAllWords(Dictionary<string, int> words)
    {
        foreach (var p in words)
            _words[p.Key] = p.Value;
    }

    public void InsertAllOccurrences(int docId, ISet<int> wordIds)
    {
        // Occurrences aren't queried back by the indexer itself, only by SearchAPI's own
        // database - nothing to store here.
    }

    public Dictionary<string, int> GetAllWords() => new(_words);

    public int DocumentCount => _documents.Count;
}
