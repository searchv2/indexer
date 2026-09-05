using System.Collections.Generic;
using Shared.Model;

namespace Indexer
{
    /// <summary>
    /// In-memory stand-in for <see cref="DatabasePostgres"/>, used until a real Postgres
    /// server is provisioned. Writes go into plain collections so a crawl can be run and
    /// observed (document/word counts) without a live DB. Swap App.cs's IDatabase wiring for
    /// DatabasePostgres once the database exists - no other code needs to change.
    /// </summary>
    public class MockDatabase : IDatabase
    {
        private readonly Dictionary<string, int> _words = new();
        private readonly List<BEDocument> _documents = new();

        public void InsertDocument(BEDocument doc) => _documents.Add(doc);

        public void InsertAllWords(Dictionary<string, int> words)
        {
            foreach (var p in words)
                _words[p.Key] = p.Value;
        }

        public void InsertAllOcc(int docId, ISet<int> wordIds)
        {
            // Occurrences aren't queried back by the indexer itself, only by SearchAPI's
            // own database - nothing to store here.
        }

        public Dictionary<string, int> GetAllWords() => new(_words);

        public int DocumentCounts => _documents.Count;
    }
}
