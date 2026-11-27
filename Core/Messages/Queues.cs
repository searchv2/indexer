namespace Indexer.Core.Messages;

/// <summary>Names of the queues that connect the pipeline stages.</summary>
public static class Queues
{
    /// <summary>Crawler -> tokenizers.</summary>
    public const string Discovered = "searchv2.documents.discovered";

    /// <summary>Tokenizers -> writers.</summary>
    public const string Tokenized = "searchv2.documents.tokenized";

    /// <summary>Dead-letter queue: messages a stage could not process, kept for inspection.</summary>
    public const string Failed = "searchv2.documents.failed";
}
