-- Exclusive MS_001 product schema. Never execute on Signal MS002's D1.
CREATE TABLE IF NOT EXISTS licenses (
  id TEXT PRIMARY KEY CHECK(length(id)=36),
  card_hmac TEXT NOT NULL UNIQUE CHECK(length(card_hmac)=64),
  product_id TEXT NOT NULL CHECK(product_id='MOSEN_TELEGRAM_SCHEDULER'),
  duration_seconds INTEGER NOT NULL CHECK(duration_seconds>0 AND duration_seconds<=158112000),
  max_devices INTEGER NOT NULL CHECK(max_devices BETWEEN 1 AND 5),
  activated_ms INTEGER,
  expires_ms INTEGER,
  disabled INTEGER NOT NULL DEFAULT 0 CHECK(disabled IN (0,1)),
  created_ms INTEGER NOT NULL,
  version INTEGER NOT NULL DEFAULT 1,
  CHECK((activated_ms IS NULL AND expires_ms IS NULL) OR
        (activated_ms IS NOT NULL AND expires_ms IS NOT NULL))
);
CREATE TABLE IF NOT EXISTS license_devices (
  license_id TEXT NOT NULL REFERENCES licenses(id) ON DELETE CASCADE,
  device_hash TEXT NOT NULL CHECK(length(device_hash)=64),
  first_seen_ms INTEGER NOT NULL,
  last_seen_ms INTEGER NOT NULL,
  PRIMARY KEY(license_id,device_hash)
);
CREATE INDEX IF NOT EXISTS ix_licenses_product ON licenses(product_id,disabled);
CREATE TABLE IF NOT EXISTS license_renewals (
  operation_id TEXT PRIMARY KEY CHECK(length(operation_id)=36),
  license_id TEXT NOT NULL REFERENCES licenses(id),
  duration_seconds INTEGER NOT NULL CHECK(duration_seconds>0),
  applied INTEGER NOT NULL DEFAULT 0 CHECK(applied IN(0,1)),
  created_ms INTEGER NOT NULL,
  result_expires_ms INTEGER
);
CREATE TABLE IF NOT EXISTS license_admin_audit (
  id TEXT PRIMARY KEY CHECK(length(id)=36),
  event_type TEXT NOT NULL CHECK(event_type IN ('issued','renewed','disabled','enabled')),
  license_id TEXT NOT NULL,
  created_ms INTEGER NOT NULL
);
