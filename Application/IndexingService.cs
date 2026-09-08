using Shared;
using Indexer.Core.Abstractions;
using Indexer.Core.Entities;

namespace Indexer.Application;

/// <summary>
/// Crawls the <see cref="IDocumentSource"/> and writes documents, words and occurrences to
/// the <see cref="IIndexStore"/>. Pure application logic - depends only on Core ports, and
/// holds no state between calls to <see cref="Reindex"/>.
/// </summary>
public sealed class IndexingService : IIndexingService
{
    private readonly IIndexStore _store;
    private readonly IDocumentSource _source;

    public IndexingService(IIndexStore store, IDocumentSource source)
    {
        _store = store;
        _source = source;
    }

    public IndexStatistics Reindex()
    {
        var start = DateTime.Now;

        // word value -> word id, accumulated across this crawl
        var words = new Dictionary<string, int>();
        var documentCounter = 0;

        foreach (var source in _source.EnumerateDocuments())
        {
            documentCounter++;
            var doc = new IndexedDocument
            {
                Id = documentCounter,
                Url = source.Url,
                IndexedAt = DateTime.Now.ToString(),
                CreatedAt = source.CreatedAt,
            };
            _store.InsertDocument(doc);

            var wordsInDoc = ExtractWords(source.Content);

            var newWords = new Dictionary<string, int>();
            foreach (var word in wordsInDoc)
                if (!words.ContainsKey(word))
                {
                    words[word] = words.Count + 1;
                    newWords[word] = words[word];
                }
            _store.InsertAllWords(newWords);

            _store.InsertAllOccurrences(doc.Id, wordsInDoc.Select(w => words[w]).ToHashSet());
        }

        var all = _store.GetAllWords();
        return new IndexStatistics
        {
            DocumentCount = _store.DocumentCount,
            DistinctWordCount = all.Count,
            SampleWords = all.Take(10).ToList(),
            Elapsed = DateTime.Now - start,
        };
    }

    // The set of words in [content], NFKC-normalized and case folded.
    private static ISet<string> ExtractWords(string content)
    {
        var res = new HashSet<string>();
        foreach (var word in Tokenizer.Tokenize(content))
            res.Add(TextNormalizer.Fold(word));
        return res;
    }
}
