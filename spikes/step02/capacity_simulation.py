"""Synthetic scheduling resource spike, no Telegram logins or network calls.

The output verifies orchestration logic only; it is NOT proof that 300 TDLib
sessions can stay online, or that 50 Telegram groups can send concurrently.
"""
from __future__ import annotations

import argparse
import asyncio
import json
import time
import tracemalloc


async def exercise(accounts: int = 300, groups: int = 50, steps: int = 200) -> dict:
    if not (1 <= accounts <= 300 and 1 <= groups <= 50 and 1 <= steps <= 10000):
        raise ValueError("simulation size outside declared bounds")
    # Bare account descriptors; these are not Telegram sessions.
    registry = {f"sim-{i:03d}": {"alias": f"测试账号{i:03d}", "active": False} for i in range(accounts)}
    assert len(registry) == accounts
    # In this test, 50 group actors share at most 20 simulated accounts.
    gates = {key: asyncio.Lock() for key in list(registry)[:min(accounts, 20)]}
    active: dict[str, int] = {key: 0 for key in gates}
    max_active: dict[str, int] = {key: 0 for key in gates}
    queues = [asyncio.Queue(maxsize=32) for _ in range(groups)]
    counters = [0] * groups

    async def worker(group_id: int) -> None:
        account = list(gates)[group_id % len(gates)]
        q = queues[group_id]
        while True:
            value = await q.get()
            try:
                if value is None:
                    return
                async with gates[account]:
                    active[account] += 1
                    max_active[account] = max(max_active[account], active[account])
                    await asyncio.sleep(0)  # deterministic cooperative handoff
                    counters[group_id] += 1
                    active[account] -= 1
            finally:
                q.task_done()

    async def producer(group_id: int) -> None:
        for value in range(steps):
            await queues[group_id].put(value)
        await queues[group_id].put(None)

    tracemalloc.start()
    start = time.perf_counter()
    try:
        workers = [asyncio.create_task(worker(i)) for i in range(groups)]
        await asyncio.gather(*(producer(i) for i in range(groups)))
        await asyncio.wait_for(asyncio.gather(*workers), timeout=20)
        elapsed = time.perf_counter() - start
        _, peak_bytes = tracemalloc.get_traced_memory()
    finally:
        tracemalloc.stop()

    assert sum(counters) == groups * steps
    assert all(n == steps for n in counters)
    assert max(max_active.values()) == 1
    assert all(q.empty() for q in queues)
    return {
        "scope": "synthetic_registry_and_task_queues_only",
        "accounts_metadata": accounts,
        "group_actors": groups,
        "events_processed": sum(counters),
        "per_account_peak_concurrency": max(max_active.values()),
        "queue_bound": 32,
        "elapsed_sec": round(elapsed, 5),
        "tracemalloc_peak_mib": round(peak_bytes / (1024 * 1024), 4),
        "real_telegram_sessions": 0,
        "real_telegram_messages": 0,
        "native_tdlib_loaded": False,
        "windows_wpf_executed": False,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--accounts", type=int, default=300)
    parser.add_argument("--groups", type=int, default=50)
    parser.add_argument("--steps", type=int, default=200)
    args = parser.parse_args()
    print(json.dumps(asyncio.run(exercise(args.accounts, args.groups, args.steps)), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
