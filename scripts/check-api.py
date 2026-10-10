"""Exercise a disposable deployment; never run seed mode against production data."""
import argparse
import json
import os
from pathlib import Path
from urllib.request import Request, urlopen

parser = argparse.ArgumentParser()
parser.add_argument("--url", default="http://localhost:5186")
parser.add_argument("--snapshot", required=True)
parser.add_argument("--seed", action="store_true")
args = parser.parse_args()
token = None

def request(path, data=None):
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    req = Request(args.url + path, data=json.dumps(data).encode() if data is not None else None, headers=headers)
    with urlopen(req, timeout=20) as response:
        content = response.read()
        return json.loads(content) if content else None

auth = request("/api/auth/login", {"email": os.environ["ADMIN_EMAIL"], "password": os.environ["ADMIN_PASSWORD"]})
token = auth["token"]
if args.seed:
    folder = request("/api/categories", {"name": "迁移验证分类", "sortOrder": 10})
    request("/api/links", {"categoryId": folder["id"], "title": "中英迁移验证", "url": "https://example.com/test", "description": "保留描述", "tags": "测试", "iconUrl": "https://example.com/custom/icon.png", "isPinned": True, "sortOrder": 10})
results = request("/api/portal/search?keyword=%E4%B8%AD")
assert any(row["title"] == "中英迁移验证" for row in results), "Single-character search or persistence failed"
portal = request("/api/portal")
snapshot = {"tenant": portal["tenant"], "categories": portal["categories"]}
file = Path(args.snapshot)
if args.seed:
    file.write_text(json.dumps(snapshot, ensure_ascii=False, sort_keys=True), encoding="utf-8")
else:
    assert json.loads(file.read_text(encoding="utf-8")) == snapshot, "Migrated/restarted API data changed"
assert request("/api/site-icons?url=http%3A%2F%2F127.0.0.1%2F") == [], "Private icon URL was fetched"
print("LOGIN_SEARCH_AND_DATA_PERSISTENCE_OK")
