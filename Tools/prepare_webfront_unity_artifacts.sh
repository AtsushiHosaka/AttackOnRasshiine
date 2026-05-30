#!/usr/bin/env zsh
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: Tools/prepare_webfront_unity_artifacts.sh <webfront-public-unity-dir>" >&2
  exit 64
fi

SCRIPT_DIR=${0:A:h}
PROJECT_ROOT=${SCRIPT_DIR:h}
SOURCE_DIR="${PROJECT_ROOT}/Builds/WebGL"
TARGET_DIR=$1

if [[ ! -f "${SOURCE_DIR}/Build/WebGL.loader.js" ]]; then
  echo "Missing WebGL build output at ${SOURCE_DIR}. Run Tools/build_webgl_production.sh first." >&2
  exit 66
fi

mkdir -p "${TARGET_DIR}"
rsync -a --delete "${SOURCE_DIR}/" "${TARGET_DIR}/"

printf 'Copied Unity WebGL artifacts to %s\n' "${TARGET_DIR}"
