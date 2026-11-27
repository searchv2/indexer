namespace Indexer.Core.Messages;

/// <summary>Stage 1 -> 2: the crawler found a file. Carries no content, only where to read it.</summary>
public sealed record DocumentDiscovered(string Url, string CreatedAt);
