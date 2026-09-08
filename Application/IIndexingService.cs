using Indexer.Core.Entities;

namespace Indexer.Application;

/// <summary>
/// The indexer's single use case: crawl the document source and (re)build the
/// word/document/occurrence index.
/// <para>
/// This is the one entry point any trigger calls - the console entry point today, and
/// whatever starts a crawl later (an "enough documents uploaded" event, a scheduled batch
/// update, an ops "reindex now" call). Those are adapters in the outer layer; none of them
/// change this contract.
/// </para>
/// </summary>
public interface IIndexingService
{
    IndexStatistics Reindex();
}
