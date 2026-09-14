"""Summarize completed work turns without publishing private conversation logs.

Example: python3 scripts/update_development_time.py /path/to/session.jsonl \
    --through 2026-09-14T13:07:00.339Z --commit 949aea3
The explicit cutoff excludes the audit that is generating the report.
"""
import argparse
from collections import defaultdict
from datetime import datetime
import json
from pathlib import Path
from zoneinfo import ZoneInfo


def timestamp(value):
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def milliseconds(delta):
    return (delta.days * 86400 + delta.seconds) * 1000 + delta.microseconds // 1000


def summarize(session, through, commit):
    starts, completed = {}, {}
    cutoff = timestamp(through)
    with session.open() as stream:
        for line in stream:
            if not line.strip():
                continue
            record = json.loads(line)
            if record.get("type") != "event_msg":
                continue
            event = record.get("payload", {})
            kind = event.get("type")
            if kind == "task_started":
                starts.setdefault(event["turn_id"], record["timestamp"])
            elif kind == "task_complete" and timestamp(record["timestamp"]) <= cutoff:
                turn = event["turn_id"]
                assert turn in starts, "Completed turn has no matching start"
                duration = event["duration_ms"]
                assert isinstance(duration, int) and duration >= 0
                entry = {"start": starts[turn], "end": record["timestamp"],
                         "activeMilliseconds": duration}
                assert turn not in completed or completed[turn] == entry, "Conflicting completion records"
                completed[turn] = entry
    turns = sorted(completed.values(), key=lambda entry: entry["start"])
    assert turns and timestamp(turns[-1]["end"]) == cutoff, "Cutoff must match a completed work turn"
    daily = defaultdict(lambda: {"completedTurns": 0, "activeMilliseconds": 0})
    wall = 0
    for index, turn in enumerate(turns):
        start, end = timestamp(turn["start"]), timestamp(turn["end"])
        assert end >= start
        if index:
            assert start >= timestamp(turns[index - 1]["end"]), "Overlapping turns require separate accounting"
        # Current evidence has no turn crossing a Berlin midnight; do not misattribute a future one.
        local_start = start.astimezone(ZoneInfo("Europe/Berlin"))
        local_end = end.astimezone(ZoneInfo("Europe/Berlin"))
        assert local_start.date() == local_end.date(), "Split cross-midnight turns before daily reporting"
        day = daily[local_start.date().isoformat()]
        day["completedTurns"] += 1
        day["activeMilliseconds"] += turn["activeMilliseconds"]
        wall += milliseconds(end - start)
    active = sum(turn["activeMilliseconds"] for turn in turns)
    elapsed = milliseconds(timestamp(turns[-1]["end"]) - timestamp(turns[0]["start"]))
    return {
        "throughCommit": commit,
        "start": turns[0]["start"],
        "end": turns[-1]["end"],
        "elapsedMilliseconds": elapsed,
        "activeMilliseconds": active,
        "completedTurns": len(turns),
        "activeDates": len(daily),
        "reportingTimezone": "Europe/Berlin",
        "daily": [{"date": day, **value} for day, value in sorted(daily.items())],
        "summedTurnWallMilliseconds": wall,
        "turnWallMinusReportedActiveMilliseconds": wall - active,
        "method": "Elapsed = first task_started timestamp to final task_complete timestamp. Active = sum of duration_ms for unique completed turn IDs, as reported by the session runtime; not a gap-threshold estimate.",
        "scope": "All completed work in this game conversation: design/research and learning discussion, art generation, implementation, testing, builds, documentation, trailers and uploads. Includes tool execution and waits within active turns. Excludes gaps between turns, the current timing audit, and unrecorded/offline human work. This is assistant runtime, not human labor or pure CPU/GPU/model inference time.",
        "clockCaveat": "The runtime reports durations separately from start/end timestamps. The difference is recorded above; its cause is not identified in the log. Use reported duration_ms without inventing an allocation.",
        "evidenceFile": "development-time-turns.json",
    }, turns


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("session", type=Path)
    parser.add_argument("--through", required=True)
    parser.add_argument("--commit", required=True)
    args = parser.parse_args()
    summary, turns = summarize(args.session, args.through, args.commit)
    root = Path(__file__).resolve().parents[1]
    metrics_path = root / "docs/development-metrics.json"
    metrics = json.loads(metrics_path.read_text())
    metrics["developmentTime"] = summary
    metrics_path.write_text(json.dumps(metrics, indent=2) + "\n")
    evidence = {"schemaVersion": 1, "source": "Sanitized task_started/task_complete timing records from the local development conversation; no prompts, tool outputs or credentials.",
                "through": args.through, "turns": [{"index": i + 1, **turn} for i, turn in enumerate(turns)]}
    (root / "docs/development-time-turns.json").write_text(json.dumps(evidence, indent=2) + "\n")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
