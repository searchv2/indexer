using Shared;

namespace Indexer.Application;

/// <summary>Turns document text into the set of words that is indexed. Shared by batch indexing and the tokenizer stage.</summary>
public static class WordExtractor
{
    /// <summary>The set of words in <paramref name="content"/>, NFKC-normalized and case folded.</summary>
    public static ISet<string> Extract(string content)
    {
        var res = new HashSet<string>();
        foreach (var word in Tokenizer.Tokenize(content))
            res.Add(TextNormalizer.Fold(word));
        return res;
    }
}
