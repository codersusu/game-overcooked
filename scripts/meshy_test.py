#!/usr/bin/env python3
"""Small, resumable Meshy API probe. Credentials never enter command arguments or logs."""
import argparse
import base64
import json
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / ".local" / "meshy-test"
OUTPUT = ROOT / "art" / "experiments" / "meshy-capybara-01"
API = "https://api.meshy.ai/openapi/v1/"
ENDPOINTS = {"model": "image-to-3d", "rig": "rigging", "animation": "animations"}


def key():
    source = (ROOT / ".env").read_text()
    match = re.search(r"^\s*(?:export\s+)?MESHY_API_KEY\s*=\s*[\"']?(msy_[A-Za-z0-9_-]+)", source, re.M)
    if not match:
        raise RuntimeError("MESHY_API_KEY is missing or has an unsupported format")
    return match.group(1)


def safe(text):
    return re.sub(r"msy_[A-Za-z0-9_-]+", "[REDACTED]", str(text))


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def api(endpoint, payload=None):
    if not re.fullmatch(r"[a-z0-9/?=&_+.-]+", endpoint):
        raise ValueError("Unsupported endpoint")
    request = urllib.request.Request(
        API + endpoint,
        data=None if payload is None else json.dumps(payload).encode(),
        headers={"Authorization": "Bearer " + key(), "Content-Type": "application/json"},
        method="GET" if payload is None else "POST",
    )
    try:
        with urllib.request.build_opener(NoRedirect()).open(request, timeout=60) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        message = safe(error.read().decode(errors="replace"))[:1500]
        raise RuntimeError(f"Meshy HTTP {error.code}: {message}") from None


def save(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(data, indent=2) + "\n")
    temporary.replace(path)


def record(stage):
    path = CACHE / (stage + ".json")
    return path, json.loads(path.read_text()) if path.exists() else None


def brief(stage, task):
    return {"stage": stage, **{k: task[k] for k in
        ("id", "status", "progress", "consumed_credits", "preceding_tasks", "task_error") if k in task}}


def submit(stage):
    path, existing = record(stage)
    if existing:
        if existing.get("id"):
            print(json.dumps({"reused_existing_task": True, **brief(stage, existing)}))
            return
        raise RuntimeError("A submission was already attempted. Inspect its outcome before creating another paid task.")
    if stage == "model":
        payload = json.loads((OUTPUT / "image-to-3d-request.json").read_text())
        image_path = OUTPUT / "input.png"
        payload["image_url"] = "data:image/png;base64," + base64.b64encode(image_path.read_bytes()).decode()
    elif stage == "rig":
        _, model = record("model")
        if not model or model.get("status") != "SUCCEEDED":
            raise RuntimeError("A successful model task is required")
        payload = {"input_task_id": model["id"], "height_meters": 1.2}
    else:
        _, rig = record("rig")
        if not rig or rig.get("status") != "SUCCEEDED":
            raise RuntimeError("A successful rig task is required")
        settings = json.loads((OUTPUT / "animation-request.json").read_text())
        payload = {"rig_task_id": rig["id"], **settings}
    sanitized = {k: v for k, v in payload.items() if k != "image_url"}
    save(path, {"submission_started_at": time.time(), "request": sanitized})
    # POST is deliberately never retried automatically: a timeout may hide success.
    result = api(ENDPOINTS[stage], payload)
    data = {"id": result["result"], "status": "SUBMITTED", "request": sanitized}
    save(path, data)
    print(json.dumps(brief(stage, data)))


def poll(stage):
    path, current = record(stage)
    if not current or not current.get("id"):
        raise RuntimeError("No task ID exists for this stage")
    data = api(ENDPOINTS[stage] + "/" + current["id"])
    if "request" in current:
        data["request"] = current["request"]
    save(path, data)
    save(OUTPUT / (stage + "-status.json"), brief(stage, data))
    print(json.dumps(brief(stage, data)))


def download(stage):
    _, task = record(stage)
    if not task or task.get("status") != "SUCCEEDED":
        raise RuntimeError("Only successful tasks can be downloaded")
    assets = {}
    if stage == "model":
        assets.update({"model." + ext: url for ext, url in task.get("model_urls", {}).items() if ext in ("glb", "fbx")})
        if task.get("thumbnail_url"):
            assets["preview.png"] = task["thumbnail_url"]
        assets.update({"preview-" + view + ".png": url for view, url in task.get("thumbnail_urls", {}).items()})
        for index, maps in enumerate(task.get("texture_urls", [])):
            for name, url in maps.items():
                if url:
                    ext = Path(urllib.parse.urlparse(url).path).suffix or ".png"
                    assets[f"texture-{index}-{name}{ext}"] = url
    else:
        result = task.get("result") or {}
        for name, url in result.items():
            if isinstance(url, str) and url and name.endswith("_url"):
                ext = Path(urllib.parse.urlparse(url).path).suffix
                assets[name.removesuffix("_url") + ext] = url
        for name, url in result.get("basic_animations", {}).items():
            if url:
                ext = Path(urllib.parse.urlparse(url).path).suffix
                assets[name.removesuffix("_url") + ext] = url
    destination = OUTPUT / stage
    destination.mkdir(parents=True, exist_ok=True)
    for name, url in assets.items():
        host = urllib.parse.urlparse(url).hostname or ""
        if not url.startswith("https://") or not (host == "assets.meshy.ai" or host.endswith(".meshy.ai")):
            raise RuntimeError("Unexpected asset origin: " + host)
        path = destination / name
        if path.exists() and path.stat().st_size:
            print("Already saved:", str(path.relative_to(ROOT)))
            continue
        # Asset URLs are signed; no API credential is attached to downloads.
        with urllib.request.urlopen(url, timeout=120) as response:
            content = response.read()
        path.write_bytes(content)
        print("Saved:", str(path.relative_to(ROOT)), len(content), "bytes")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=("balance", "library", "submit", "poll", "download"))
    parser.add_argument("stage", nargs="?", choices=tuple(ENDPOINTS))
    args = parser.parse_args()
    if args.command == "balance":
        result = api("balance")
        save(CACHE / ("balance-" + str(int(time.time())) + ".json"), result)
        print(json.dumps(result))
    elif args.command == "library":
        result = api("animations/library")
        save(OUTPUT / "animation-library.json", result)
        actions = result if isinstance(result, list) else result.get("result", result.get("data", []))
        matches = [a for a in actions if any(word in str(a.get("name", "")).lower()
                   for word in ("jump", "bow", "chop", "cook", "wash", "carry"))]
        print(json.dumps({"total_actions": len(actions), "relevant_actions":
              [{k: a.get(k) for k in ("action_id", "name", "category")} for a in matches]}))
    elif not args.stage:
        parser.error("A stage is required")
    else:
        {"submit": submit, "poll": poll, "download": download}[args.command](args.stage)


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(safe(error), file=sys.stderr)
        sys.exit(1)
