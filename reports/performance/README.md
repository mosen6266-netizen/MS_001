# Step30 performance artifacts

Each GitHub CI runner produces its own JSON file named step30-ubuntu-latest.json or step30-windows-latest.json. Both are uploaded as artifacts, retained for 30 days. They record hardware/runtime metadata and short-run process counters. CI also runs 72 hours of accelerated logical model time; these are not 72 hours of wall clock stability.

No fabricated machine readings should ever be committed as benchmark data. See docs/BENCHMARKS.md and docs/CAPACITY_LIMITS.md. Real tdjson, 300 online sessions and 72-hour physical validation remain DEFERRED.
