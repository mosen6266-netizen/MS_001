-- Dedicated MS_001 Telegram auth only; never migrate Signal MS002.
CREATE TABLE IF NOT EXISTS admin_auth_state (
 id INTEGER PRIMARY KEY CHECK(id=1),
 last_totp_step INTEGER NOT NULL DEFAULT -1,
 epoch INTEGER NOT NULL DEFAULT 1
);
INSERT OR IGNORE INTO admin_auth_state(id) VALUES(1);
CREATE TABLE IF NOT EXISTS admin_sessions (
 token_digest TEXT PRIMARY KEY CHECK(length(token_digest)=64),
 csrf_digest TEXT NOT NULL CHECK(length(csrf_digest)=64),
 created_ms INTEGER NOT NULL,
 expires_ms INTEGER NOT NULL,
 auth_epoch INTEGER NOT NULL,
 used_recovery INTEGER NOT NULL CHECK(used_recovery IN(0,1))
);
CREATE INDEX IF NOT EXISTS idx_admin_sessions_expiry ON admin_sessions(expires_ms);
CREATE TABLE IF NOT EXISTS admin_recovery (
 code_digest TEXT PRIMARY KEY CHECK(length(code_digest)=64),
 created_ms INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS admin_rate_limits (
 bucket_key TEXT NOT NULL CHECK(length(bucket_key)=64),
 bucket_ms INTEGER NOT NULL,
 hits INTEGER NOT NULL CHECK(hits>=0),
 PRIMARY KEY(bucket_key,bucket_ms)
);
CREATE INDEX IF NOT EXISTS idx_admin_rate_bucket ON admin_rate_limits(bucket_ms);
CREATE TABLE IF NOT EXISTS admin_security_audit (
 id TEXT PRIMARY KEY,
 action TEXT NOT NULL CHECK(action IN
 ('login_totp','login_recovery','login_denied','logout','recovery_rotated',
  'sessions_revoked','rate_limited','key_rotation_checked')),
 occurred_ms INTEGER NOT NULL,
 ok INTEGER NOT NULL CHECK(ok IN (0,1)),
 actor TEXT NOT NULL DEFAULT 'admin'
);
CREATE INDEX IF NOT EXISTS idx_admin_audit_time ON admin_security_audit(occurred_ms DESC);
