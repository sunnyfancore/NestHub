"""CI check: invoke native update and verify new image health and task cleanup."""
import json
import os
import subprocess
import time
import urllib.request

base = "http://127.0.0.1:5186"
def request(path, body=None, token=None):
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    req = urllib.request.Request(base + path, data=json.dumps(body).encode() if body is not None else None, headers=headers)
    with urllib.request.urlopen(req, timeout=30) as response:
        return json.load(response)

login = request("/api/auth/login", {"email": os.environ["ADMIN_EMAIL"], "password": os.environ["ADMIN_PASSWORD"]})
request("/api/system-updates", {}, login["token"])
for attempt in range(120):
    result = subprocess.run(["docker", "inspect", "nesthub"], capture_output=True, text=True)
    if result.returncode == 0:
        container = json.loads(result.stdout)[0]
        revision = "NESTHUB_BUILD_REVISION=" + "b" * 40
        if revision in container["Config"]["Env"] and container["State"].get("Health", {}).get("Status") == "healthy":
            workers = subprocess.check_output(["docker", "ps", "--filter", "name=nesthub-update-task", "--format", "{{.ID}}"], text=True).strip()
            if not workers:
                print("NATIVE_SELF_UPDATE_HEALTH_AND_CLEANUP_OK")
                break
    time.sleep(2)
else:
    # Only show the task result, never container environments or authentication tokens.
    subprocess.run(["docker", "exec", "nesthub", "cat", "/app/AppData/update-status.json"])
    raise SystemExit("Native self update failed to replace the image and remove its task")
