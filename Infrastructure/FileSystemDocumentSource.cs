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
        EnumerateFiles(new DirectoryInfo(_options.Folder)).Select(file => new SourceDocument(
            file.FullName,
            file.CreationTime.ToString(),
            File.ReadAllText(file.FullName)));

    public IEnumerable<DocumentReference> EnumerateReferences() =>
        EnumerateFiles(new DirectoryInfo(_options.Folder)).Select(file =>
            new DocumentReference(file.FullName, file.CreationTime.ToString()));

    /// <summary>
    /// Reads a file named in a queue message. Only files inside <see cref="IndexerOptions.Folder"/>
    /// are read, so a message cannot make a worker read an arbitrary path.
    /// </summary>
    public string Read(string url)
    {
        var root = Path.GetFullPath(_options.Folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(url);
        if (!path.StartsWith(root, StringComparison.Ordinal))
            throw new InvalidOperationException($"{url} is outside the document folder.");
        return File.ReadAllText(path);
    }

    private IEnumerable<FileInfo> EnumerateFiles(DirectoryInfo dir)
    {
        foreach (var file in dir.EnumerateFiles())
            if (_options.Extensions.Contains(file.Extension))
                yield return file;

        foreach (var sub in dir.EnumerateDirectories())
            foreach (var file in EnumerateFiles(sub))
                yield return file;
    }
}
