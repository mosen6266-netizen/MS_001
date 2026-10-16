CREATE TABLE editor_drafts (
 script_id TEXT NOT NULL PRIMARY KEY REFERENCES scripts(script_id) ON DELETE RESTRICT,
 revision INTEGER NOT NULL DEFAULT 1 CHECK(revision >= 1),
 snapshot_json TEXT NOT NULL CHECK(length(snapshot_json) BETWEEN 2 AND 524288),
 archived_utc TEXT,
 updated_utc TEXT NOT NULL
);
CREATE INDEX ix_editor_drafts_active ON editor_drafts(archived_utc,updated_utc);
