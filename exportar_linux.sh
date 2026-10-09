#!/usr/bin/env bash
# Genera el ejecutable de Linux sin abrir el editor de Unity, usando la escena
# configurada en Build Settings.
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UNITY="${UNITY_EDITOR:-$HOME/Unity/Hub/Editor/2022.3.30f1/Editor/Unity}"
PROYECTO="$RAIZ/Unity"
CARPETA_SALIDA="$PROYECTO/Builds/Linux"
EJECUTABLE="$CARPETA_SALIDA/FiveNightsOfTerror.x86_64"
REGISTRO="$PROYECTO/Builds/exportar_linux.log"

if [ ! -x "$UNITY" ]; then
  echo "No se encontró el editor de Unity en $UNITY."
  echo "Indica la ruta con: UNITY_EDITOR=/ruta/a/Unity ./exportar_linux.sh"
  exit 1
fi

# Unity no permite dos instancias sobre el mismo proyecto.
if pgrep -fi "projectpath.*$PROYECTO" >/dev/null; then
  echo "El editor de Unity está abierto con este proyecto. Ciérralo y vuelve a intentar."
  exit 1
fi

# Se borra el ejecutable anterior para que un export fallido no deje uno viejo
# que parezca recién generado.
rm -rf "$CARPETA_SALIDA"
mkdir -p "$CARPETA_SALIDA"

echo "Exportando... (la primera vez puede tardar varios minutos)"
if ! "$UNITY" -batchmode -quit -projectPath "$PROYECTO" \
     -buildLinux64Player "$EJECUTABLE" -logFile "$REGISTRO"; then
  echo "La exportación falló. Revisa $REGISTRO (busca 'error CS' o 'Build Failed')."
  exit 1
fi

if [ ! -x "$EJECUTABLE" ]; then
  echo "Unity terminó pero no generó $EJECUTABLE. Revisa $REGISTRO."
  exit 1
fi

echo "Listo: $EJECUTABLE"
echo "Para jugar: ./iniciar.sh"
