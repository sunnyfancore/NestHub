"""Create immutable application update assets from framework-dependent publishes."""
import hashlib
import json
import re
import sys
import zipfile
from datetime import datetime, timezone
from pathlib import Path

revision = sys.argv[1]
if not re.fullmatch(r"[a-f0-9]{40}", revision):
    raise SystemExit("Expected full Git commit SHA")
root = Path("publish/application")
assets = root / "assets"
assets.mkdir(parents=True, exist_ok=True)
manifest = {"revision": revision, "runtime": "net10.0", "publishedAt": datetime.now(timezone.utc).isoformat(), "packages": {}}
for rid in ("linux-x64", "linux-arm64"):
    source = root / rid
    for required in ("NestHub.Api.dll", "NestHub.Api.runtimeconfig.json", "wwwroot/index.html", "wwwroot/downloads/nesthub-extension.zip"):
        if not (source / required).is_file():
            raise SystemExit(f"Missing packaged file: {source / required}")
    target = assets / f"nesthub-{rid}.zip"
    with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as archive:
        for file in sorted(source.rglob("*")):
            if file.is_file() and "AppData" not in file.relative_to(source).parts:
                archive.write(file, file.relative_to(source).as_posix())
    manifest["packages"][rid] = {"url": f"https://github.com/sunnyfancore/NestHub/releases/download/app-{revision}/{target.name}", "sha256": hashlib.file_digest(target.open("rb"), "sha256").hexdigest(), "size": target.stat().st_size}
(assets / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
