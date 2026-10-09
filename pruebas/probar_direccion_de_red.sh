#!/usr/bin/env bash
# Compila DireccionDeRed.cs con sus pruebas usando el Mono que trae Unity y
# las corre, sin abrir el editor.
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MONO_BIN="${MONO_BIN:-$HOME/Unity/Hub/Editor/2022.3.30f1/Editor/Data/MonoBleedingEdge/bin}"
TEMP="$(mktemp -d)"
trap 'rm -rf "$TEMP"' EXIT

"$MONO_BIN/mcs" -out:"$TEMP/prueba.exe" \
  "$RAIZ/Unity/Assets/scripts/DireccionDeRed.cs" \
  "$RAIZ/pruebas/PruebaDireccionDeRed.cs"
"$MONO_BIN/mono" "$TEMP/prueba.exe"
