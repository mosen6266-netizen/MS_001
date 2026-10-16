-- v2: frozen role/account binding and safe no-blind-retry outbox.
CREATE TABLE group_bindings (
    binding_id TEXT PRIMARY KEY NOT NULL,
    chat_id TEXT NOT NULL REFERENCES chats(chat_id) ON DELETE CASCADE,
    topic_id TEXT,
    script_id TEXT NOT NULL REFERENCES scripts(script_id) ON DELETE CASCADE,
    script_version INTEGER NOT NULL CHECK(script_version > 0),
    role_key TEXT NOT NULL,
    account_id TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE RESTRICT,
    checked_utc TEXT NOT NULL,
    FOREIGN KEY(script_id, script_version, role_key)
      REFERENCES script_roles(script_id, script_version, role_key) ON DELETE RESTRICT,
    FOREIGN KEY(chat_id, topic_id)
      REFERENCES topics(chat_id, topic_id) ON DELETE RESTRICT
);
CREATE TABLE runs (
    run_id TEXT PRIMARY KEY NOT NULL,
    chat_id TEXT NOT NULL REFERENCES chats(chat_id),
    topic_id TEXT,
    script_id TEXT NOT NULL,
    script_version INTEGER NOT NULL,
    state TEXT NOT NULL CHECK(state IN ('ready','running','paused','review','completed','stopped','failed')),
    started_utc TEXT,
    updated_utc TEXT NOT NULL,
    FOREIGN KEY(script_id, script_version)
      REFERENCES script_versions(script_id, version) ON DELETE RESTRICT,
    FOREIGN KEY(chat_id, topic_id)
      REFERENCES topics(chat_id, topic_id) ON DELETE RESTRICT
);
CREATE TABLE run_steps (
    run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE CASCADE,
    step_key TEXT NOT NULL,
    state TEXT NOT NULL CHECK(state IN ('planned','waiting_before','typing','submitting','pending','confirmed','failed','outcome_unknown','waiting_after')),
    account_id TEXT NOT NULL REFERENCES accounts(account_id),
    updated_utc TEXT NOT NULL,
    PRIMARY KEY(run_id, step_key)
);
CREATE TABLE outbox (
    attempt_id TEXT PRIMARY KEY NOT NULL,
    run_id TEXT NOT NULL,
    step_key TEXT NOT NULL,
    state TEXT NOT NULL CHECK(state IN ('planned','submitting','pending','confirmed','failed','outcome_unknown')),
    platform_message_id TEXT,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL,
    FOREIGN KEY(run_id, step_key) REFERENCES run_steps(run_id, step_key) ON DELETE RESTRICT
);
CREATE TABLE task_events (
    event_id TEXT PRIMARY KEY NOT NULL,
    run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE CASCADE,
    event_type TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    diagnostic_code TEXT NOT NULL DEFAULT ''
);
CREATE INDEX ix_runs_state ON runs(state);
CREATE INDEX ix_outbox_state ON outbox(state);
CREATE INDEX ix_events_run_time ON task_events(run_id, created_utc);
