"""Package existing Unity exports without calling asset-generation services."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "art/downloads"
GAME = ROOT / "art/production/gameplay-round-01"
settings = (ROOT / "Unity/BaraKitchen/ProjectSettings/ProjectSettings.asset").read_text()
version = re.search(r"^  bundleVersion: (.+)$", settings, re.M).group(1).strip()
assert re.fullmatch(r"\d+\.\d+\.\d+", version), "Expected a semantic game version"
OUTPUT.mkdir(parents=True, exist_ok=True)

mac = OUTPUT / f"Bara-Kitchen-v{version}-macOS-AppleSilicon.zip"
subprocess.run(["ditto", "-c", "-k", "--sequesterRsrc", "--keepParent",
                str(ROOT / ".local/release/Bara Kitchen.app"), str(mac)], check=True)
# A helper is included separately; the application and Web build contain no secret.
helper_start = """Optional GPT-Live conversation (Python 3.9+ and your own OpenAI key)
On Mac, open Start Voice Helper.command (Python 3.9+ required).
Or run these commands in this folder:
  python3 -m venv .local/voice-venv
  .local/voice-venv/bin/pip install -r scripts/voice/requirements.txt
  .local/voice-venv/bin/python scripts/voice/live_chef_server.py --player-keys-only
Keep the terminal open. Launch the game and click AI chat or press V.
Paste YOUR OpenAI key into the masked popup, then choose Connect & chat.
No key file is needed. Your key is kept only in memory for this play session.
Settings > Voice setup > Forget key clears it; quitting also forgets it.
No developer key is included. The supplied launcher ignores environment keys.
Allow microphone access, then talk while playing. End chat / V stops capture.
The game continues during chat; there is no typing interface.
Bara can give contextual help, discuss progress and offer occasional reminders.
Say 'no hints' to disable automatic reminders. Use headphones on native Mac.
Audio streams to OpenAI only while chat is on; the helper saves no conversations.
GPT-Live charges for connected time, including silence; backend reasoning is extra.
Each chat ends after 15 minutes or a lost game connection; reconnect if desired.
The helper listens on 127.0.0.1:54115 only. Never distribute your .env file.
Ordinary gameplay requires no helper, internet or API key.
"""
def add_launcher(archive):
    launcher = zipfile.ZipInfo("Bara-Kitchen-Voice-Helper/Start Voice Helper.command")
    launcher.create_system = 3
    launcher.external_attr = 0o100755 << 16
    launcher.compress_type = zipfile.ZIP_DEFLATED
    archive.writestr(launcher, '#!/bin/sh\nset -eu\ncd "$(dirname "$0")"\nexec /bin/sh scripts/voice/start_helper.command\n')

helper_files = [(ROOT / ("scripts/voice/" + name), "scripts/voice/" + name)
                for name in ("live_chef_server.py", "chef_brain.py", "requirements.txt", "start_helper.command")]

with zipfile.ZipFile(mac, "a", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for source, relative in helper_files:
        archive.write(source, "Bara-Kitchen-Voice-Helper/" + relative)
    archive.writestr("Bara-Kitchen-Voice-Helper/START.txt", helper_start)
    add_launcher(archive)
web = OUTPUT / f"Bara-Kitchen-v{version}-Web.zip"
with zipfile.ZipFile(web, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    prefix = "Bara-Kitchen-Web/"
    html = (GAME / "index.html").read_text().replace(
        "../../brand/round-01/bara-kitchen-icon.png", "bara-kitchen-icon.png")
    archive.writestr(prefix + "index.html", html)
    archive.write(GAME / "live-chat.js", prefix + "live-chat.js")
    archive.write(ROOT / "art/brand/round-01/bara-kitchen-icon.png", prefix + "bara-kitchen-icon.png")
    for source in sorted((GAME / "web").rglob("*")):
        if source.is_file() and not source.name.startswith("."):
            archive.write(source, prefix + source.relative_to(GAME).as_posix())
    for source, relative in helper_files:
        archive.write(source, "Bara-Kitchen-Voice-Helper/" + relative)
    archive.writestr("Bara-Kitchen-Voice-Helper/START.txt", helper_start)
    add_launcher(archive)
    archive.writestr(prefix + "VOICE-HELP.txt", "Set up the separate sibling Bara-Kitchen-Voice-Helper folder.\nOpen a terminal there and follow START.txt.\nKeep .env outside the Bara-Kitchen-Web folder served by HTTP.\n")
    archive.writestr(prefix + "START.txt", f"""Bara Kitchen demo {version}
Run python3 -m http.server 8000 inside this folder, then open http://localhost:8000
A local HTTP server is needed; opening index.html directly does not load Unity.
Desktop keyboard and mouse required. See the GitHub README for controls.
Optional spoken gameplay help: see VOICE-HELP.txt (requires your own OpenAI key).
After extinguishing: E picks up the burnt pot, E at the bin empties it, E returns it to the stove.
""")

manifest, validation = [], []
for archive in (mac, web):
    with zipfile.ZipFile(archive) as package:
        assert package.testzip() is None, f"Corrupt ZIP: {archive.name}"
        validation.append({"file": archive.name, "fileCount": len(package.namelist()), "crcPassed": True})
    digest = hashlib.sha256()
    with archive.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    manifest.append({"file": archive.name, "bytes": archive.stat().st_size, "sha256": digest.hexdigest()})
(OUTPUT / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
(OUTPUT / "package-validation.json").write_text(json.dumps(validation, indent=2) + "\n")
print(json.dumps({"version": version, "packages": manifest, "validated": True}))
