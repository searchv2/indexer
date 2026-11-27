using Npgsql;
using Indexer.Infrastructure.Persistence;

namespace Indexer.Tests;

/// <summary>Runs only when SEARCH_DB_TEST_CONNECTION points at a PostgreSQL server; it works in a throwaway schema.</summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SEARCH_DB_TEST_CONNECTION")))
            Skip = "Set SEARCH_DB_TEST_CONNECTION to a PostgreSQL connection string to run this test.";
    }
}

public class PostgresDocumentWriterTests
{
    [PostgresFact]
    public async Task Redelivery_is_harmless_and_parallel_writers_neither_deadlock_nor_duplicate_words()
    {
        var admin = Environment.GetEnvironmentVariable("SEARCH_DB_TEST_CONNECTION")!;
        var schema = "indexer_test_" + Guid.NewGuid().ToString("N");
        await using var adminConnection = new NpgsqlConnection(admin);
        await adminConnection.OpenAsync();
        await Run(adminConnection, $"CREATE SCHEMA {schema}");
        try
        {
            using var writer = new PostgresDocumentWriter(new NpgsqlConnectionStringBuilder(admin) { SearchPath = schema }.ConnectionString);
            writer.ResetSchema();

            // The same message delivered twice leaves one document with one set of occurrences.
            writer.Write("/docs/once.txt", "2026-01-01", new[] { "apple", "banana" });
            writer.Write("/docs/once.txt", "2026-01-01", new[] { "apple", "banana" });
            Assert.Equal(1, writer.DocumentCount);
            Assert.Equal(2L, await Scalar(adminConnection, $"SELECT count(*) FROM {schema}.occ"));

            // Eight writers store documents that share most of their words, at the same time.
            var shared = Enumerable.Range(0, 200).Select(i => $"word{i:D3}").ToArray();
            await Task.WhenAll(Enumerable.Range(0, 8).Select(w => Task.Run(() =>
            {
                for (var d = 0; d < 25; d++)
                    writer.Write($"/docs/{w}-{d}.txt", "2026-01-01", shared.Append($"own{w}").ToArray());
            }))).WaitAsync(TimeSpan.FromSeconds(60));

            Assert.Equal(1 + 8 * 25, writer.DocumentCount);
            // 200 shared words + 8 own words + apple + banana, each exactly once.
            Assert.Equal(210L, await Scalar(adminConnection, $"SELECT count(*) FROM {schema}.word"));
            Assert.Equal(2L + 8 * 25 * 201, await Scalar(adminConnection, $"SELECT count(*) FROM {schema}.occ"));
        }
        finally
        {
            await Run(adminConnection, $"DROP SCHEMA {schema} CASCADE");
        }
    }

    private static async Task Run(NpgsqlConnection connection, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<long> Scalar(NpgsqlConnection connection, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, connection);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
}
