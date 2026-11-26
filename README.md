# indexer

A simple full-text indexer, part of the `searchv2` project.

The indexer crawls a folder tree, reads every `.txt` file it finds, tokenizes
and normalizes the words in each file, and stores the resulting
documents/words/occurrences so they can later be searched.

## How it works

- `Application/IndexingService` is the single use case: it pulls documents from
  an `IDocumentSource`, and for each one assigns new words an id and writes the
  document, the new words, and the word/document occurrences to an `IIndexStore`.
  Tokenization uses `Tokenizer.Tokenize` + `TextNormalizer.Fold` from the
  `SearchUtilities` package.
- It is invoked through `IIndexingService.Reindex()`. `Program.cs` is one caller
  (a manual console run); a future trigger - "enough documents uploaded", a
  scheduled batch update, an ops "reindex now" call - would be another adapter
  calling the same method, without touching the domain.
- `IIndexStore` is the storage port, with two implementations in
  `Infrastructure/Persistence/`:
  - `PostgresIndexStore` (default) - stores documents, words and occurrences in
    PostgreSQL (via `Npgsql`), (re)creating the `document`, `word` and `occ`
    tables on startup. Words and occurrences are bulk-loaded with binary `COPY`.
    SearchAPI reads the same database (see `SearchDatabase`).
  - `InMemoryIndexStore` - an in-memory stand-in that needs no database.
- `IDocumentSource` is the crawl port; `FileSystemDocumentSource` walks
  `IndexerOptions.Folder` recursively for files matching `IndexerOptions.Extensions`.

## Architecture

Onion architecture; dependencies point inward only:

| Layer | Folder | Contents |
|-------|--------|----------|
| Core | `Core/` | Domain entities (`IndexedDocument`, `SourceDocument`, `IndexStatistics`) and ports (`IIndexStore`, `IDocumentSource`). No framework dependencies. |
| Application | `Application/` | The `IndexingService` use case and its `IIndexingService` interface. Depends only on Core. |
| Infrastructure | `Infrastructure/` | Adapters behind the Core ports: `PostgresIndexStore`, `InMemoryIndexStore`, `FileSystemDocumentSource`, `IndexerOptions`, `SearchDatabase`, plus DI wiring. |
| Entry point | `Program.cs` | Composition root: builds the DI container and calls `IIndexingService.Reindex()`. |

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

- A PostgreSQL database the user can create tables in. In Kubernetes this is
  the `postgresql-db` Helm release in the `database` namespace (see
  `../k8s/README.md`); `../docker-compose.yml` starts one for the local stack.

## Setup

The project depends on the `SearchUtilities` package. Until it is published to
nuget.org, it is restored from a local NuGet feed configured in `NuGet.config`,
which points at `../search-utilities/nupkg`. Clone the `search-utilities`
repository as a sibling of this repository and build its package before
restoring `indexer`, or update `NuGet.config` once `SearchUtilities` is
published for real.

```bash
dotnet restore
dotnet build
```

## Usage

1. Set the folder to index in `Infrastructure/IndexerOptions.cs` (all files
   matching `Extensions`, under `Folder` and its subfolders, are indexed).
2. Point the indexer at PostgreSQL with `SEARCH_DB_CONNECTION` (an Npgsql
   connection string, default
   `Host=localhost;Port=5432;Database=searchengine;Username=searcher;Gss Encryption Mode=Disable`)
   and `SEARCH_DB_PASSWORD`, or switch the store in
   `Infrastructure/DependencyInjection.cs` to `InMemoryIndexStore`. Each run
   drops and recreates the index tables.
3. Run the indexer:

   ```bash
   dotnet run
   ```

   This crawls the configured folder, indexes every matching file, and prints
   the elapsed time, the number of documents indexed, the number of distinct
   words found, and the first 10 words.

## Docker

```bash
docker build -t searchv2-indexer .
docker run --rm \
  -e SEARCH_DB_CONNECTION="Host=<db-host>;Database=searchengine;Username=searcher" \
  -e SEARCH_DB_PASSWORD=<password> \
  -v "$PWD/../seData/medium:/data/docs:ro" \
  searchv2-indexer
```

The image sets `INDEXER_FOLDER=/data/docs`; mount the document folder there. The
build fetches and packs `SearchUtilities` from GitHub (override the ref with
`--build-arg SEARCH_UTILITIES_REF=...`). See `../docker-compose.yml` for the
full local stack.
