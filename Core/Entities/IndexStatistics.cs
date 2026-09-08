namespace Indexer.Core.Entities;

/// <summary>
/// A summary of what a crawl indexed. <see cref="SampleWords"/> is a small prefix of the
/// indexed vocabulary, for a quick sanity check.
/// </summary>
public sealed class IndexStatistics
{
    public required int DocumentCount { get; init; }

    public required int DistinctWordCount { get; init; }

    public required IReadOnlyList<KeyValuePair<string, int>> SampleWords { get; init; }

    public required TimeSpan Elapsed { get; init; }
}
