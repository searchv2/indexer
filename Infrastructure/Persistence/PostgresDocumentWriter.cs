using Npgsql;
using Indexer.Core.Abstractions;

namespace Indexer.Infrastructure.Persistence;

/// <summary>
/// Writes one document per transaction, with database-assigned ids, so any number of writer
/// workers can run at the same time. Does not touch the schema unless <see cref="ResetSchema"/> is called.
/// </summary>
public sealed class PostgresDocumentWriter : IDocumentWriter, IDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresDocumentWriter(string connectionString) =>
        _dataSource = NpgsqlDataSource.Create(connectionString);

    public void ResetSchema()
    {
        using var connection = _dataSource.OpenConnection();
        IndexSchema.Recreate(connection);
    }

    public int DocumentCount
    {
        get
        {
            using var connection = _dataSource.OpenConnection();
            using var cmd = new NpgsqlCommand("SELECT count(*)::int FROM document", connection);
            return (int)cmd.ExecuteScalar()!;
        }
    }

    public void Write(string url, string createdAt, IReadOnlyCollection<string> words)
    {
        // Sorted so that two writers insert shared words in the same order and cannot deadlock.
        var names = words.Order(StringComparer.Ordinal).ToArray();

        using var connection = _dataSource.OpenConnection();
        using var transaction = connection.BeginTransaction();

        int docId;
        using (var cmd = new NpgsqlCommand(
            """
            INSERT INTO document(url, idxTime, creationTime) VALUES (@url, @idxTime, @creationTime)
            ON CONFLICT (url) DO UPDATE SET idxTime = EXCLUDED.idxTime, creationTime = EXCLUDED.creationTime
            RETURNING id
            """, connection, transaction))
        {
            cmd.Parameters.AddWithValue("url", url);
            cmd.Parameters.AddWithValue("idxTime", DateTime.Now.ToString());
            cmd.Parameters.AddWithValue("creationTime", createdAt);
            docId = (int)cmd.ExecuteScalar()!;
        }

        // A redelivered message finds the document already there: drop its old occurrences first.
        using (var cmd = new NpgsqlCommand("DELETE FROM occ WHERE docId = @docId", connection, transaction))
        {
            cmd.Parameters.AddWithValue("docId", docId);
            cmd.ExecuteNonQuery();
        }

        if (names.Length > 0)
        {
            using (var cmd = new NpgsqlCommand(
                "INSERT INTO word(name) SELECT unnest(@names) ON CONFLICT (name) DO NOTHING", connection, transaction))
            {
                cmd.Parameters.AddWithValue("names", names);
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new NpgsqlCommand(
                "INSERT INTO occ(wordId, docId) SELECT id, @docId FROM word WHERE name = ANY(@names)", connection, transaction))
            {
                cmd.Parameters.AddWithValue("docId", docId);
                cmd.Parameters.AddWithValue("names", names);
                cmd.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    public void Dispose() => _dataSource.Dispose();
}
