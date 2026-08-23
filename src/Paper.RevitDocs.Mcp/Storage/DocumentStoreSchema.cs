namespace Paper.RevitDocs.Mcp.Storage;

internal static class DocumentStoreSchema
{
    public const int Version = 1;

    public const string Create = """
        PRAGMA journal_mode = WAL;
        CREATE TABLE IF NOT EXISTS documents (
            result_id TEXT PRIMARY KEY,
            source_id TEXT NOT NULL,
            title TEXT NOT NULL,
            symbol TEXT NULL,
            canonical_uri TEXT NOT NULL,
            revit_version INTEGER NULL,
            revision TEXT NULL,
            kind INTEGER NOT NULL,
            cache_state INTEGER NOT NULL,
            score REAL NOT NULL,
            supporting_sources TEXT NOT NULL,
            summary TEXT NULL,
            content TEXT NULL,
            retrieved_at TEXT NOT NULL,
            expires_at TEXT NULL,
            quarantined INTEGER NOT NULL DEFAULT 0
        );
        CREATE VIRTUAL TABLE IF NOT EXISTS documents_fts USING fts5(
            result_id UNINDEXED,
            title,
            symbol,
            summary,
            content
        );
        CREATE TABLE IF NOT EXISTS source_states (
            source_id TEXT PRIMARY KEY,
            state TEXT NOT NULL,
            message TEXT NULL,
            last_successful_update TEXT NULL
        );
        PRAGMA user_version = 1;
        """;
}
