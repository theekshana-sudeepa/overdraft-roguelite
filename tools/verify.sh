#!/usr/bin/env bash
# tools/verify.sh — OVERDRAFT verification loop (docs/05_QA_AND_VERIFY.md §1)
# Usage: ./tools/verify.sh [--release] [--static-only]
set -uo pipefail
FAIL=0
UNITY="${UNITY_PATH:-/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity}"
PROJ="$(cd "$(dirname "$0")/.." && pwd)"
mkdir -p "$PROJ/build/logs"

say() { printf "\n\033[1m== %s\033[0m\n" "$1"; }
bad() { printf "\033[31mFAIL: %s\033[0m\n" "$1"; FAIL=1; }

STATIC_ONLY=0
RELEASE=0
for arg in "$@"; do
  case "$arg" in
    --static-only) STATIC_ONLY=1 ;;
    --release)     RELEASE=1 ;;
  esac
done

# ---------- 1. Static bans (fast, run first) ----------
say "Static analysis"
S="$PROJ/Assets/_Project/Scripts"

grep -rn "UnityEngine" "$S/Generation/" 2>/dev/null \
  && bad "UnityEngine referenced in Generation/ — breaks determinism (ARCH §1)"

grep -rnE "FindObjectOfType|GameObject\.Find|SendMessage" "$S" 2>/dev/null \
  && bad "Banned lookup API in Scripts/ (CLAUDE.md §4)"

grep -rnE "using System\.Linq" "$S/Player/" "$S/Enemies/" "$S/Economy/" "$S/Generation/" 2>/dev/null \
  && bad "LINQ in a hot-path folder (CLAUDE.md §4)"

grep -rn "Instantiate(" "$S" 2>/dev/null | grep -vE "ObjectPool|Bootstrap|Editor" \
  && bad "Runtime Instantiate outside the pool (C3)"

grep -rn "UnityEngine.Random" "$S/Generation/" "$S/Core/RunController.cs" 2>/dev/null \
  && bad "UnityEngine.Random in a generation path (NFR-07)"

grep -rn "Camera.main" "$S" 2>/dev/null | grep -v "Awake\|Start" \
  && bad "Camera.main outside Awake/Start"

# ---------- 2. Compile + EditMode tests ----------
if [ "$STATIC_ONLY" -eq 1 ]; then
  say "Compile + EditMode tests"
  echo "SKIPPED (--static-only). Compilation and tests were NOT run."
elif [ ! -x "$UNITY" ]; then
  say "Compile + EditMode tests"
  bad "Unity not found at: $UNITY
       Install Unity 2022.3 LTS, or set UNITY_PATH to your editor binary, e.g.
         UNITY_PATH=/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity ./tools/verify.sh
       Compilation and tests were NOT run — do not report this task verified."
else
  say "Compile + EditMode tests"
  "$UNITY" -batchmode -nographics -projectPath "$PROJ" \
    -runTests -testPlatform EditMode \
    -testResults "$PROJ/build/logs/results.xml" \
    -logFile "$PROJ/build/logs/tests.log" -quit
  [ $? -ne 0 ] && bad "Unity tests failed — see build/logs/tests.log"

  grep -qE 'result="Failed"' "$PROJ/build/logs/results.xml" 2>/dev/null \
    && bad "One or more EditMode tests failed"

  grep -E "error CS[0-9]+" "$PROJ/build/logs/tests.log" 2>/dev/null \
    && bad "Compile errors"
fi

# ---------- 3. Release-only checks ----------
if [ "$RELEASE" -eq 1 ]; then
  say "Release checks"
  [ -f "$PROJ/steam_appid.txt" ] && bad "steam_appid.txt must not ship (STEAM §2)"
  grep -rn "Debug.Log(" "$S" 2>/dev/null && bad "Debug.Log left in shipping code"
  grep -rn "TODO\|FIXME" "$S" 2>/dev/null && bad "TODO/FIXME left in code"
fi

say "RESULT"
[ $FAIL -eq 0 ] && echo "✅ PASS" || { echo "❌ FAIL"; exit 1; }
