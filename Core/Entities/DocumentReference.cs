namespace Indexer.Core.Entities;

/// <summary>Where a document is and when it was created, without its content.</summary>
public sealed record DocumentReference(string Url, string CreatedAt);
