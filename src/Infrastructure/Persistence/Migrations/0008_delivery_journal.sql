-- v8: delivery simulation ledger, NOT a live Telegram outbox or send queue.
-- Separate from early runs/outbox schema; no message text, phone, auth/session or secrets.
CREATE TABLE delivery_attempts (
 attempt_id TEXT PRIMARY KEY NOT NULL,
 run_id TEXT NOT NULL,
 step_key TEXT NOT NULL,
 account_id TEXT NOT NULL,
 chat_id TEXT NOT NULL,
 topic_id TEXT,
 correlation_id TEXT NOT NULL UNIQUE,
 state TEXT NOT NULL CHECK(state IN
   ('Planned','Submitting','Pending','Confirmed','Failed','Unknown','Review')),
 platform_message_id TEXT,
 diagnostic_code TEXT NOT NULL DEFAULT '',
 revision INTEGER NOT NULL CHECK(revision>=1),
 created_utc TEXT NOT NULL,
 updated_utc TEXT NOT NULL,
 UNIQUE(run_id,step_key),
 CHECK(platform_message_id IS NULL OR length(platform_message_id) BETWEEN 1 AND 64)
);
CREATE UNIQUE INDEX ux_delivery_confirmed_message
  ON delivery_attempts(account_id,chat_id,platform_message_id)
  WHERE platform_message_id IS NOT NULL;
CREATE INDEX ix_delivery_review ON delivery_attempts(state,updated_utc);
CREATE INDEX ix_delivery_run ON delivery_attempts(run_id,state);
CREATE TABLE delivery_events (
 event_id TEXT PRIMARY KEY NOT NULL,
 attempt_id TEXT NOT NULL REFERENCES delivery_attempts(attempt_id) ON DELETE RESTRICT,
 from_state TEXT NOT NULL,
 to_state TEXT NOT NULL,
 event_code TEXT NOT NULL,
 event_utc TEXT NOT NULL
);
CREATE INDEX ix_delivery_events_attempt ON delivery_events(attempt_id,event_utc);
