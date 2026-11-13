#!/usr/bin/env bash
# Compila las reglas del jugador 2 (recursos, tareas, rescate de Puppet, ruta, cámara), el perfil de video
# y el ajuste del socket con sus pruebas usando el Mono que trae Unity y las corre, sin abrir el editor.
# PruebaAjusteDeSocket abre el relay (UnityRelay/relay.py) en el puerto 8000 para probar con un ClientWebSocket real.
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MONO_BIN="${MONO_BIN:-$HOME/Unity/Hub/Editor/2022.3.30f1/Editor/Data/MonoBleedingEdge/bin}"
TEMP="$(mktemp -d)"
trap 'rm -rf "$TEMP"' EXIT

# Cada prueba tiene su propio Main, así que se compila una por una.
for par in "RecursosDeOperador:PruebaRecursosDeOperador" "TareasDeOperador:PruebaTareasDeOperador" "RescateDePuppet:PruebaRescateDePuppet" "RutaFija:PruebaRutaFija" "ControlDeCamara:PruebaControlDeCamara" "PerfilDeVideo:PruebaPerfilDeVideo" "AjusteDeSocket:PruebaAjusteDeSocket" "AmbienteDelJuego:PruebaAmbienteDelJuego" "PlanoDelMapa:PruebaPlanoDelMapa"; do
  regla="${par%%:*}"
  prueba="${par##*:}"
  echo "== $regla =="
  "$MONO_BIN/mcs" -out:"$TEMP/$prueba.exe" \
    "$RAIZ/Unity/Assets/scripts/$regla.cs" \
    "$RAIZ/pruebas/$prueba.cs"
  RAIZ="$RAIZ" "$MONO_BIN/mono" "$TEMP/$prueba.exe"
done
