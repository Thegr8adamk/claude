#!/usr/bin/env bash
# Rex Magnum - install the Unity 6 Linux editor in the cloud session, activate it with the license in $UNITY_LICENSE
# (the text of the owner's Unity_lic.ulf, stored as an environment variable - never in the repo), make a URP project,
# copy Assets/RexMagnum in, and run the builders in batch mode. Logs: /tmp/unity/*.log
# Needs network access to: download.unity3d.com, public-cdn.cloud.unity3d.com, license.unity3d.com,
# core.cloud.unity3d.com, services.api.unity.com, packages.unity.com, download.packages.unity.com
set -euo pipefail
VERSION_PREFIX="${UNITY_VERSION:-6000.0}"
ROOT=/tmp/unity
mkdir -p "$ROOT"
cd "$ROOT"
if [ -z "${UNITY_LICENSE:-}" ]; then echo "UNITY_LICENSE is not set (environment settings)"; exit 2; fi

# 1. find + download the newest Linux editor for the version prefix
if [ ! -x "$ROOT/Editor/Unity" ]; then
  URL=$(curl -sS "https://services.api.unity.com/unity/editor/release/v1/releases?version=${VERSION_PREFIX}&platform=LINUX&architecture=X86_64&limit=1&order=RELEASE_DATE_DESC" \
        | python3 -c "import sys,json; r=json.load(sys.stdin)['results'][0]; print([d['url'] for d in r['downloads'] if d['type']=='TAR_XZ' or d['url'].endswith('.tar.xz')][0]); print(r['version'], file=sys.stderr)")
  echo "Editor: $URL"
  curl -sS -L "$URL" -o editor.tar.xz
  tar -xf editor.tar.xz
  rm -f editor.tar.xz
fi
UNITY="$ROOT/Editor/Unity"

# 2. license
mkdir -p ~/.local/share/unity3d/Unity
printf "%s" "$UNITY_LICENSE" > ~/.local/share/unity3d/Unity/Unity_lic.ulf

# 3. project: Packages/manifest.json with URP + Input System, our Assets
P="$ROOT/RexMagnumProject"
mkdir -p "$P/Assets" "$P/Packages"
cat > "$P/Packages/manifest.json" <<'JSON'
{ "dependencies": {
  "com.unity.render-pipelines.universal": "17.0.4",
  "com.unity.inputsystem": "1.11.2",
  "com.unity.ugui": "2.0.0",
  "com.unity.modules.ai": "1.0.0", "com.unity.modules.animation": "1.0.0", "com.unity.modules.audio": "1.0.0",
  "com.unity.modules.imageconversion": "1.0.0", "com.unity.modules.imgui": "1.0.0", "com.unity.modules.physics": "1.0.0",
  "com.unity.modules.terrain": "1.0.0", "com.unity.modules.terrainphysics": "1.0.0", "com.unity.modules.ui": "1.0.0",
  "com.unity.modules.jsonserialize": "1.0.0", "com.unity.modules.screencapture": "1.0.0", "com.unity.modules.video": "1.0.0" } }
JSON
rsync -a --delete "$(dirname "$0")/../Unity/Assets/RexMagnum/" "$P/Assets/RexMagnum/"

# 4. import + compile, then build the test scene and the island (no graphics card: -nographics)
run() { "$UNITY" -batchmode -nographics -quit -projectPath "$P" -logFile "$ROOT/$1.log" ${2:+-executeMethod $2} || true;
        echo "== $1: $(grep -c 'error CS' "$ROOT/$1.log" || true) compile errors, $(grep -c 'Exception' "$ROOT/$1.log" || true) exceptions"; }
run import
run setup RexMagnum.EditorTools.ProjectSetup.Menu
run rex RexMagnum.EditorTools.RexBuilder.BuildFromCommandLine
run island RexMagnum.EditorTools.IslandBuilder.BuildFromCommandLine
grep -h "error CS\|Exception\|\[Rex Magnum\]" "$ROOT"/*.log | sort | uniq -c | sort -rn | head -60
