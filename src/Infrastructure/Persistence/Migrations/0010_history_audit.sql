-- v10: bounded, message-free local operational history and audit timeline.
-- This is a derived log, NEVER a queue of Telegram messages or a source of outbound authority.
CREATE TABLE history_events (
  seq INTEGER PRIMARY KEY AUTOINCREMENT,
  source_key TEXT NOT NULL UNIQUE,
  event_utc TEXT NOT NULL,
  kind TEXT NOT NULL CHECK (kind IN ('Scheduler','Delivery','System')),
  code TEXT NOT NULL CHECK (code IN (
     'TaskReady','TaskRunning','TaskPaused','TaskReview','TaskCompleted',
     'TaskStopped','TaskFailed','DeliveryPlanned','DeliverySubmitting',
     'DeliveryPending','DeliveryConfirmed','DeliveryFailed','DeliveryUnknown',
     'DeliveryReview','CorruptRecord','EmergencyStopped','HistoryPurged')),
  severity TEXT NOT NULL CHECK (severity IN ('Info','Warning','Error')),
  context_id TEXT,
  recorded_utc TEXT NOT NULL,
  CHECK(context_id IS NULL OR length(context_id)=36)
);
CREATE INDEX ix_history_severity_seq ON history_events(severity,seq DESC);
CREATE INDEX ix_history_code_seq ON history_events(code,seq DESC);
CREATE INDEX ix_history_context_seq ON history_events(context_id,seq DESC);
CREATE INDEX ix_history_retention_utc ON history_events(event_utc);
