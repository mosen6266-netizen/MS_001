-- v1: user-visible metadata only; TDLib auth databases are kept outside app.db.
CREATE TABLE schema_migrations (
    version INTEGER PRIMARY KEY CHECK(version > 0),
    sha256 TEXT NOT NULL,
    applied_utc TEXT NOT NULL
);
CREATE TABLE accounts (
    account_id TEXT PRIMARY KEY NOT NULL,
    alias TEXT NOT NULL CHECK(length(trim(alias)) > 0),
    display_name TEXT NOT NULL DEFAULT '',
    login_state TEXT NOT NULL DEFAULT 'disconnected'
      CHECK(login_state IN ('disconnected','auth_required','connecting','ready','restricted')),
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);
CREATE TABLE chats (
    chat_id TEXT PRIMARY KEY NOT NULL,
    title TEXT NOT NULL,
    chat_kind TEXT NOT NULL CHECK(chat_kind IN ('group','supergroup','channel')),
    updated_utc TEXT NOT NULL
);
CREATE TABLE topics (
    chat_id TEXT NOT NULL REFERENCES chats(chat_id) ON DELETE CASCADE,
    topic_id TEXT NOT NULL,
    title TEXT NOT NULL DEFAULT '',
    PRIMARY KEY(chat_id, topic_id)
);
CREATE TABLE account_memberships (
    account_id TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    chat_id TEXT NOT NULL REFERENCES chats(chat_id) ON DELETE CASCADE,
    can_send INTEGER NOT NULL DEFAULT 0 CHECK(can_send IN (0,1)),
    checked_utc TEXT NOT NULL,
    PRIMARY KEY(account_id, chat_id)
);
CREATE TABLE scripts (
    script_id TEXT PRIMARY KEY NOT NULL,
    title TEXT NOT NULL CHECK(length(trim(title)) > 0),
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);
CREATE TABLE script_versions (
    script_id TEXT NOT NULL REFERENCES scripts(script_id) ON DELETE CASCADE,
    version INTEGER NOT NULL CHECK(version > 0),
    published INTEGER NOT NULL DEFAULT 0 CHECK(published IN (0,1)),
    created_utc TEXT NOT NULL,
    PRIMARY KEY(script_id, version)
);
CREATE TABLE script_roles (
    script_id TEXT NOT NULL,
    script_version INTEGER NOT NULL,
    role_key TEXT NOT NULL,
    role_label TEXT NOT NULL CHECK(length(trim(role_label)) > 0),
    PRIMARY KEY(script_id, script_version, role_key),
    FOREIGN KEY(script_id, script_version) REFERENCES script_versions(script_id, version) ON DELETE CASCADE
);
CREATE TABLE media_assets (
    sha256 TEXT PRIMARY KEY NOT NULL CHECK(length(sha256) = 64),
    relative_path TEXT NOT NULL,
    media_type TEXT NOT NULL,
    byte_size INTEGER NOT NULL CHECK(byte_size >= 0),
    created_utc TEXT NOT NULL
);
CREATE TABLE script_steps (
    script_id TEXT NOT NULL,
    script_version INTEGER NOT NULL,
    step_key TEXT NOT NULL,
    ordinal INTEGER NOT NULL CHECK(ordinal >= 0),
    role_key TEXT NOT NULL,
    body TEXT NOT NULL DEFAULT '',
    pre_send_delay_ms INTEGER NOT NULL DEFAULT 5000 CHECK(pre_send_delay_ms BETWEEN 0 AND 3600000),
    typing_duration_ms INTEGER NOT NULL DEFAULT 5000 CHECK(typing_duration_ms BETWEEN 0 AND 60000),
    post_send_delay_ms INTEGER NOT NULL DEFAULT 0 CHECK(post_send_delay_ms BETWEEN 0 AND 3600000),
    media_sha256 TEXT REFERENCES media_assets(sha256) ON DELETE RESTRICT,
    PRIMARY KEY(script_id, script_version, step_key),
    UNIQUE(script_id, script_version, ordinal),
    FOREIGN KEY(script_id, script_version, role_key)
      REFERENCES script_roles(script_id, script_version, role_key) ON DELETE CASCADE
);
CREATE TABLE settings (
    setting_key TEXT PRIMARY KEY NOT NULL,
    value_json TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);
CREATE INDEX ix_accounts_alias ON accounts(alias);
CREATE INDEX ix_chats_title ON chats(title);
CREATE INDEX ix_script_steps_ordinal ON script_steps(script_id, script_version, ordinal);
