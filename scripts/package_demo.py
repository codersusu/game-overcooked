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
web = OUTPUT / f"Bara-Kitchen-v{version}-Web.zip"
with zipfile.ZipFile(web, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    prefix = "Bara-Kitchen-Web/"
    html = (GAME / "index.html").read_text().replace(
        "../../brand/round-01/bara-kitchen-icon.png", "bara-kitchen-icon.png")
    archive.writestr(prefix + "index.html", html)
    archive.write(ROOT / "art/brand/round-01/bara-kitchen-icon.png", prefix + "bara-kitchen-icon.png")
    for source in sorted((GAME / "web").rglob("*")):
        if source.is_file() and not source.name.startswith("."):
            archive.write(source, prefix + source.relative_to(GAME).as_posix())
    archive.writestr(prefix + "START.txt", f"""Bara Kitchen demo {version}
Run python3 -m http.server 8000 inside this folder, then open http://localhost:8000
A local HTTP server is needed; opening index.html directly does not load Unity.
Desktop keyboard and mouse required. See the GitHub README for controls.
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
