namespace Indexer.Core.Messages;

/// <summary>Stage 2 -> 3: the distinct, normalised words of one document, ready to be stored.</summary>
public sealed record DocumentTokenized(string Url, string CreatedAt, IReadOnlyList<string> Words);
