#!/usr/bin/env zsh
set -euo pipefail

SCRIPT_DIR=${0:A:h}
PROJECT_ROOT=${SCRIPT_DIR:h}
UNITY_BIN=${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.4.3f1/Unity.app/Contents/MacOS/Unity}
LOG_FILE=${LOG_FILE:-/private/tmp/AttackOnRasshiine-webgl-build.log}

"${UNITY_BIN}" \
  -batchmode \
  -nographics \
  -projectPath "${PROJECT_ROOT}" \
  -executeMethod AttackOnRasshiine.Editor.RasshiineSceneBuilder.BuildWebGL \
  -quit \
  -logFile "${LOG_FILE}"

printf 'WebGL build completed at %s/Builds/WebGL\n' "${PROJECT_ROOT}"
printf 'Build log: %s\n' "${LOG_FILE}"
