using Microsoft.Data.Sqlite;
using Indexer.Core.Abstractions;
using Indexer.Core.Entities;

namespace Indexer.Infrastructure.Persistence;

/// <summary>
/// Stores documents, words and occurrences in a local SQLite file (see
/// <see cref="SearchDatabase"/>). Recreates the schema on construction, so every run starts
/// from an empty index. SearchAPI opens the same file to read it back.
/// </summary>
public sealed class SqliteIndexStore : IIndexStore, IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteIndexStore()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SearchDatabase.DatabaseFile)!);

        _connection = new SqliteConnection(SearchDatabase.ConnectionString);
        _connection.Open();

        Execute("PRAGMA journal_mode=WAL");
        Execute("PRAGMA foreign_keys=ON");

        Execute("DROP TABLE IF EXISTS Occ");
        Execute("DROP TABLE IF EXISTS document");
        Execute("DROP TABLE IF EXISTS word");

        Execute("CREATE TABLE document(id INTEGER PRIMARY KEY, url TEXT, idxTime TEXT, creationTime TEXT)");
        Execute("CREATE TABLE word(id INTEGER PRIMARY KEY, name TEXT UNIQUE)");
        Execute("CREATE TABLE Occ(wordId INTEGER, docId INTEGER, "
                + "FOREIGN KEY (wordId) REFERENCES word(id), "
                + "FOREIGN KEY (docId) REFERENCES document(id))");
        Execute("CREATE INDEX word_index ON Occ (wordId)");
        Execute("CREATE INDEX occ_doc ON Occ (docId, wordId)");
    }

    private void Execute(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void InsertAllWords(Dictionary<string, int> res)
    {
        using var transaction = _connection.BeginTransaction();
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO word(id, name) VALUES(@id,@name)";

        var paramName = command.CreateParameter();
        paramName.ParameterName = "name";
        command.Parameters.Add(paramName);

        var paramId = command.CreateParameter();
        paramId.ParameterName = "id";
        command.Parameters.Add(paramId);

        foreach (var p in res)
        {
            paramName.Value = p.Key;
            paramId.Value = p.Value;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void InsertAllOccurrences(int docId, ISet<int> wordIds)
    {
        using var transaction = _connection.BeginTransaction();
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO Occ(wordId, docId) VALUES(@wordId,@docId)";

        var paramWordId = command.CreateParameter();
        paramWordId.ParameterName = "wordId";
        command.Parameters.Add(paramWordId);

        var paramDocId = command.CreateParameter();
        paramDocId.ParameterName = "docId";
        paramDocId.Value = docId;
        command.Parameters.Add(paramDocId);

        foreach (var wordId in wordIds)
        {
            paramWordId.Value = wordId;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void InsertDocument(IndexedDocument doc)
    {
        using var insertCmd = _connection.CreateCommand();
        insertCmd.CommandText =
            "INSERT INTO document(id, url, idxTime, creationTime) VALUES(@id,@url,@idxTime,@creationTime)";

        insertCmd.Parameters.AddWithValue("id", doc.Id);
        insertCmd.Parameters.AddWithValue("url", doc.Url);
        insertCmd.Parameters.AddWithValue("idxTime", doc.IndexedAt);
        insertCmd.Parameters.AddWithValue("creationTime", doc.CreatedAt);

        insertCmd.ExecuteNonQuery();
    }

    public Dictionary<string, int> GetAllWords()
    {
        var res = new Dictionary<string, int>();

        using var selectCmd = _connection.CreateCommand();
        selectCmd.CommandText = "SELECT id, name FROM word";

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
            using var selectCmd = _connection.CreateCommand();
            selectCmd.CommandText = "SELECT count(*) FROM document";

            using var reader = selectCmd.ExecuteReader();
            return reader.Read() ? reader.GetInt32(0) : -1;
        }
    }

    public void Dispose() => _connection.Dispose();
}
