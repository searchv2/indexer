using Npgsql;
using NpgsqlTypes;
using Indexer.Core.Abstractions;
using Indexer.Core.Entities;

namespace Indexer.Infrastructure.Persistence;

/// <summary>
/// Stores documents, words and occurrences in PostgreSQL (see <see cref="SearchDatabase"/>).
/// Recreates the schema on construction, so every run starts from an empty index. SearchAPI
/// reads the same tables back.
/// </summary>
public sealed class PostgresIndexStore : IIndexStore, IDisposable
{
    private readonly NpgsqlConnection _connection;

    public PostgresIndexStore(string connectionString)
    {
        _connection = new NpgsqlConnection(connectionString);
        _connection.Open();

        Execute("DROP TABLE IF EXISTS occ");
        Execute("DROP TABLE IF EXISTS document");
        Execute("DROP TABLE IF EXISTS word");

        Execute("CREATE TABLE document(id INTEGER PRIMARY KEY, url TEXT, idxTime TEXT, creationTime TEXT)");
        Execute("CREATE TABLE word(id INTEGER PRIMARY KEY, name TEXT UNIQUE)");
        Execute("CREATE TABLE occ(wordId INTEGER REFERENCES word(id), docId INTEGER REFERENCES document(id))");
        Execute("CREATE INDEX word_index ON occ (wordId)");
        Execute("CREATE INDEX occ_doc ON occ (docId, wordId)");
    }

    private void Execute(string sql)
    {
        using var cmd = new NpgsqlCommand(sql, _connection);
        cmd.ExecuteNonQuery();
    }

    // Binary COPY sends all rows in one stream instead of one round trip per row.
    public void InsertAllWords(Dictionary<string, int> res)
    {
        if (res.Count == 0)
            return;

        using var writer = _connection.BeginBinaryImport("COPY word(id, name) FROM STDIN (FORMAT BINARY)");
        foreach (var p in res)
        {
            writer.StartRow();
            writer.Write(p.Value, NpgsqlDbType.Integer);
            writer.Write(p.Key, NpgsqlDbType.Text);
        }
        writer.Complete();
    }

    public void InsertAllOccurrences(int docId, ISet<int> wordIds)
    {
        if (wordIds.Count == 0)
            return;

        using var writer = _connection.BeginBinaryImport("COPY occ(wordId, docId) FROM STDIN (FORMAT BINARY)");
        foreach (var wordId in wordIds)
        {
            writer.StartRow();
            writer.Write(wordId, NpgsqlDbType.Integer);
            writer.Write(docId, NpgsqlDbType.Integer);
        }
        writer.Complete();
    }

    public void InsertDocument(IndexedDocument doc)
    {
        using var insertCmd = new NpgsqlCommand(
            "INSERT INTO document(id, url, idxTime, creationTime) VALUES(@id,@url,@idxTime,@creationTime)",
            _connection);

        insertCmd.Parameters.AddWithValue("id", doc.Id);
        insertCmd.Parameters.AddWithValue("url", doc.Url);
        insertCmd.Parameters.AddWithValue("idxTime", doc.IndexedAt);
        insertCmd.Parameters.AddWithValue("creationTime", doc.CreatedAt);

        insertCmd.ExecuteNonQuery();
    }

    public Dictionary<string, int> GetAllWords()
    {
        var res = new Dictionary<string, int>();

        using var selectCmd = new NpgsqlCommand("SELECT id, name FROM word", _connection);
        using var reader = selectCmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            var w = reader.GetString(1);
            res.Add(w, id);
        }

        return res;
    }

    public int DocumentCount
    {
        get
        {
            using var selectCmd = new NpgsqlCommand("SELECT count(*)::int FROM document", _connection);
            return (int)selectCmd.ExecuteScalar()!;
        }
    }

    public void Dispose() => _connection.Dispose();
}
