#!/bin/bash
set -u
root=/app/AppData/updates
mkdir -p "$root/releases"
export NESTHUB_APP_LAUNCHER=1
export Updates__DataPath=/app/AppData
base_revision="${NESTHUB_BUILD_REVISION:-local}"
child=""
stop() { [ -z "$child" ] || kill -TERM "$child" 2>/dev/null; [ -z "$child" ] || wait "$child"; exit 0; }
trap stop TERM INT
# CLI commands (including database migration) run without the supervisor.
if [ "$#" -gt 0 ]; then cd /app/base; exec dotnet NestHub.Api.dll "$@"; fi
report() {
  jq -n --arg state "$1" --arg message "$2" --arg started "$started" --arg now "$(date -u +%FT%TZ)" \
    '{state:$state,stage:$state,message:$message,startedAt:$started,updatedAt:$now}' > "$root/status.json.launcher"
  mv "$root/status.json.launcher" "$root/status.json"
}
valid() { [[ "$1" =~ ^[a-fA-F0-9]{40}$ ]] && [ -f "$root/releases/$1/NestHub.Api.dll" ]; }
# Interrupted downloads do not resume automatically after a process/container restart.
started=$(jq -r '.startedAt // empty' "$root/status.json" 2>/dev/null || true)
state=$(jq -r '.state // empty' "$root/status.json" 2>/dev/null || true)
if [ ! -f "$root/pending.json" ] && [[ "$state" =~ ^(pulling|replacing|checking|rollingBack)$ ]]; then
  report failed '上一次更新被中断，未启用新版本；请重新发起更新。'
fi
while true; do
  active=$(cat "$root/active" 2>/dev/null || true)
  candidate=""; started=""
  if [ -f "$root/pending.json" ]; then
    candidate=$(jq -r '.revision // empty' "$root/pending.json")
    started=$(jq -r '.startedAt // empty' "$root/pending.json")
    if ! valid "$candidate"; then report failed '更新包无效，继续使用原版本。'; rm -f "$root/pending.json"; candidate=""; fi
  fi
  selected="$active"
  [ -z "$candidate" ] || selected="$candidate"
  location=/app/base; revision="$base_revision"
  if valid "$selected"; then location="$root/releases/$selected"; revision="$selected"; fi
  cd "$location"
  export NESTHUB_BUILD_REVISION="$revision"
  dotnet NestHub.Api.dll & child=$!
  printf "%s" "$child" > "$root/app.pid"
  if [ -n "$candidate" ]; then
    report checking '正在启动新版本并检查健康状态。'
    healthy=false
    for attempt in $(seq 1 90); do
      kill -0 "$child" 2>/dev/null || break
      running_revision=$(curl --fail --silent --max-time 2 http://127.0.0.1:8080/health/version | jq -r '.revision // empty' 2>/dev/null || true)
      if [ "$running_revision" = "$candidate" ] && curl --fail --silent --max-time 2 http://127.0.0.1:8080/health >/dev/null; then healthy=true; break; fi
      sleep 2
    done
    if [ "$healthy" = true ]; then
      printf '%s' "$active" > "$root/previous.tmp"; mv "$root/previous.tmp" "$root/previous"
      printf '%s' "$candidate" > "$root/active.tmp"; mv "$root/active.tmp" "$root/active"
      rm -f "$root/pending.json"
      report success '应用更新完成，新版本已通过健康检查。'
    else
      report rollingBack '新版本未通过健康检查，正在恢复原版本。'
      kill -TERM "$child" 2>/dev/null || true
      # Bound shutdown time before rollback.
      for attempt in $(seq 1 15); do kill -0 "$child" 2>/dev/null || break; sleep 1; done
      kill -KILL "$child" 2>/dev/null || true
      wait "$child" 2>/dev/null || true
      rm -f "$root/pending.json"
      report failed '新版本启动失败，已恢复原版本配置；原版本正在启动。'
      child=""; continue
    fi
  fi
  wait "$child"; child=""
  # Pending application updates restart inside the same container.
  [ -f "$root/pending.json" ] || exit 1
done
