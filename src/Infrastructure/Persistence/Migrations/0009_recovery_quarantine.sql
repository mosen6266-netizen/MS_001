-- v9: non-destructive safety quarantine for corrupt durable metadata.
-- The original rows stay in their original tables for forensic inspection.
CREATE TABLE recovery_quarantine (
 source TEXT NOT NULL CHECK(source IN ('scheduler_simulation_jobs','delivery_attempts')),
 record_id TEXT NOT NULL,
 reason TEXT NOT NULL CHECK(reason IN ('MALFORMED_CHECKPOINT','MALFORMED_DELIVERY')),
 discovered_utc TEXT NOT NULL,
 PRIMARY KEY(source,record_id)
);
CREATE INDEX ix_recovery_quarantine_time ON recovery_quarantine(discovered_utc);
