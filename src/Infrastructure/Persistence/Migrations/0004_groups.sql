-- v4: trusted Telegram group/topic metadata, account-specific membership and permission snapshots.
-- Existing manual entries remain 'unverified'; never treat them as Telegram discovery.
ALTER TABLE chats ADD COLUMN source TEXT NOT NULL DEFAULT 'unverified'
  CHECK(source IN ('unverified','tdlib'));
ALTER TABLE chats ADD COLUMN is_forum INTEGER NOT NULL DEFAULT 0 CHECK(is_forum IN (0,1));
ALTER TABLE topics ADD COLUMN is_closed INTEGER NOT NULL DEFAULT 0 CHECK(is_closed IN (0,1));
ALTER TABLE topics ADD COLUMN is_hidden INTEGER NOT NULL DEFAULT 0 CHECK(is_hidden IN (0,1));
ALTER TABLE topics ADD COLUMN updated_utc TEXT NOT NULL DEFAULT '';
ALTER TABLE account_memberships ADD COLUMN member_status TEXT NOT NULL DEFAULT 'unknown'
  CHECK(member_status IN ('unknown','member','administrator','creator','restricted','left','banned'));
ALTER TABLE account_memberships ADD COLUMN can_send_text INTEGER NOT NULL DEFAULT 0 CHECK(can_send_text IN (0,1));
ALTER TABLE account_memberships ADD COLUMN can_send_photos INTEGER NOT NULL DEFAULT 0 CHECK(can_send_photos IN (0,1));
ALTER TABLE account_memberships ADD COLUMN slow_mode_delay_seconds INTEGER NOT NULL DEFAULT 0
  CHECK(slow_mode_delay_seconds BETWEEN 0 AND 3600);
ALTER TABLE account_memberships ADD COLUMN permission_source TEXT NOT NULL DEFAULT 'unverified'
  CHECK(permission_source IN ('unverified','tdlib'));
CREATE INDEX ix_chats_trusted_title ON chats(source, chat_kind, title);
CREATE INDEX ix_memberships_account_checked ON account_memberships(account_id,permission_source,checked_utc);
-- Crucial: previous can_send=true rows were legacy metadata, NOT official Telegram permission.
UPDATE account_memberships SET can_send=0;
