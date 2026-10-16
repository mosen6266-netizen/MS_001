-- v6: immutable, checksummed published script snapshots. A draft edit must never affect a published run.
CREATE TABLE published_script_snapshots (
 script_id TEXT NOT NULL,
 version INTEGER NOT NULL CHECK(version > 0),
 source_revision INTEGER NOT NULL CHECK(source_revision > 0),
 snapshot_json TEXT NOT NULL CHECK(length(snapshot_json) BETWEEN 2 AND 524288),
 sha256 TEXT NOT NULL CHECK(length(sha256) = 64),
 created_utc TEXT NOT NULL,
 PRIMARY KEY(script_id,version),
 FOREIGN KEY(script_id,version) REFERENCES script_versions(script_id,version) ON DELETE RESTRICT
);
CREATE INDEX ix_published_script_snapshots_revision ON published_script_snapshots(script_id,source_revision);
