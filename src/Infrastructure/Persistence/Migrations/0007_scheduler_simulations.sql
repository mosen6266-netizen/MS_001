-- v7: simulation-only checkpoint table. Not a Telegram outbox or live task source.
-- Never auto-run records restored from disk.
CREATE TABLE scheduler_simulation_jobs (
 run_id TEXT PRIMARY KEY NOT NULL,
 chat_id TEXT NOT NULL,
 topic_id TEXT,
 state TEXT NOT NULL CHECK(state IN ('Ready','Running','Paused','Review','Completed','Stopped','Failed')),
 revision INTEGER NOT NULL CHECK(revision >= 1),
 snapshot_json TEXT NOT NULL CHECK(length(snapshot_json) BETWEEN 50 AND 524288),
 updated_utc TEXT NOT NULL
);
CREATE UNIQUE INDEX ux_scheduler_sim_target_active ON scheduler_simulation_jobs(chat_id,ifnull(topic_id,''))
 WHERE state IN ('Ready','Running','Paused','Review');
CREATE INDEX ix_scheduler_sim_state ON scheduler_simulation_jobs(state,updated_utc);
