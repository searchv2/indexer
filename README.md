# indexer

A simple full-text indexer, part of the `searchv2` project.

The indexer crawls a folder tree, reads every `.txt` file it finds, tokenizes
and normalizes the words in each file, and stores the resulting
documents/words/occurrences so they can later be searched.

## How it works

- `Crawler` walks a directory recursively and, for each `.txt` file:
  - extracts the set of words in the file (`Tokenizer.Tokenize` +
    `TextNormalizer.Fold` from the `SearchUtilities` package),
  - assigns each new word an id,
  - inserts the document, the new words, and the word/document occurrences
    into an `IDatabase`.
- `IDatabase` is the storage abstraction, with two implementations:
  - `MockDatabase` - an in-memory stand-in used until a real Postgres server
    is provisioned.
  - `DatabasePostgres` - stores documents, words and occurrences in
    PostgreSQL (via `Npgsql`), (re)creating the schema on startup.
- `App.Run()` wires a database into a `Crawler`, indexes all `.txt` files
  under the folder configured in `Config.FOLDER`, and prints a short summary
  (document count, number of distinct words, first few words indexed).
- `Renamer` is a small helper that can recursively rename files in a folder
  so they end in `.txt`.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A PostgreSQL server, if you want to run against `DatabasePostgres` instead
  of the in-memory `MockDatabase`.

## Setup

The project depends on the `SearchUtilities` package. Until it is published
to nuget.org, it is restored from a local NuGet feed configured in
`NuGet.config`, which points at `../search-utilities/nupkg`. Clone the
`search-utilities` repository as a sibling of this repository and build its
package before restoring `indexer`, or update `NuGet.config` once
`SearchUtilities` is published for real.

Restore and build:

```bash
dotnet restore
dotnet build
```

## Usage

1. Set the folder to index by editing `Config.FOLDER` in `Config.cs` (all
   `.txt` files under that folder, including subfolders, will be indexed).
2. Choose a database in `App.cs`: `MockDatabase` (default, in-memory) or
   `DatabasePostgres` (set the connection string via `Paths.POSTGRES_DATABASE`
   from `SearchUtilities`).
3. Run the indexer:

   ```bash
   dotnet run
   ```

   This crawls `Config.FOLDER`, indexes every `.txt` file, and prints the
   number of documents indexed, the number of distinct words found, and the
   first 10 words.
