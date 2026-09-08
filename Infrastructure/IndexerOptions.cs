namespace Indexer.Infrastructure;

/// <summary>
/// Settings for the filesystem document source. All files with an extension in
/// <see cref="Extensions"/>, under <see cref="Folder"/> (recursively), are indexed.
/// </summary>
public sealed class IndexerOptions
{
    public string Folder { get; init; } = "/home/miso/School/apip/seData/medium";

    public IReadOnlyList<string> Extensions { get; init; } = new[] { ".txt" };
}
