using Indexer.Core.Abstractions;
using Indexer.Core.Entities;

namespace Indexer.Infrastructure;

/// <summary>
/// Walks <see cref="IndexerOptions.Folder"/> recursively and yields every file whose
/// extension is in <see cref="IndexerOptions.Extensions"/>, reading its text on demand.
/// </summary>
public sealed class FileSystemDocumentSource : IDocumentSource
{
    private readonly IndexerOptions _options;

    public FileSystemDocumentSource(IndexerOptions options) => _options = options;

    public IEnumerable<SourceDocument> EnumerateDocuments() =>
        Walk(new DirectoryInfo(_options.Folder));

    private IEnumerable<SourceDocument> Walk(DirectoryInfo dir)
    {
        foreach (var file in dir.EnumerateFiles())
            if (_options.Extensions.Contains(file.Extension))
                yield return new SourceDocument(
                    file.FullName,
                    file.CreationTime.ToString(),
                    File.ReadAllText(file.FullName));

        foreach (var sub in dir.EnumerateDirectories())
            foreach (var doc in Walk(sub))
                yield return doc;
    }
}
