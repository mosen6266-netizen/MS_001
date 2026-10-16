-- v3: metadata-only account groups, verified-user dedupe, tombstones and revision checks.
-- Never store phone, TDLib API hash, QR code, password, token or database encryption keys.
ALTER TABLE accounts ADD COLUMN group_tag TEXT NOT NULL DEFAULT ''
  CHECK(length(group_tag) <= 64);
ALTER TABLE accounts ADD COLUMN verified_telegram_user_id TEXT
  CHECK(verified_telegram_user_id IS NULL OR length(verified_telegram_user_id) BETWEEN 1 AND 24);
ALTER TABLE accounts ADD COLUMN revision INTEGER NOT NULL DEFAULT 1 CHECK(revision > 0);
ALTER TABLE accounts ADD COLUMN deleted_utc TEXT;
CREATE UNIQUE INDEX ux_accounts_verified_user_id
  ON accounts(verified_telegram_user_id) WHERE verified_telegram_user_id IS NOT NULL;
CREATE INDEX ix_accounts_group ON accounts(group_tag, alias);
CREATE INDEX ix_accounts_active ON accounts(deleted_utc, updated_utc);
-- Persisted ready state from legacy metadata cannot prove official authorization after restart.
UPDATE accounts SET login_state='auth_required'
  WHERE login_state IN ('ready','connecting');
