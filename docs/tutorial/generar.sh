#!/usr/bin/env bash
# Convierte las diapositivas HTML del tutorial en las imágenes que carga Unity
# (Unity/Assets/Resources/tutorial/slide_NN.png), con Firefox sin ventana.
# Uso: docs/tutorial/generar.sh
set -euo pipefail
AQUI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DESTINO="$AQUI/../../Unity/Assets/Resources/tutorial"
# Firefox de snap solo escribe dentro de tu carpeta personal: el perfil va ahí.
PERFIL="$(mktemp -d -p "$HOME/snap/firefox/common" 2>/dev/null || mktemp -d)"
trap 'rm -rf "$PERFIL"' EXIT
n=0
for html in "$AQUI"/[0-9][0-9]_*.html; do
  n=$((n + 1))
  salida="$DESTINO/$(printf 'slide_%02d.png' "$n")"
  firefox --headless --profile "$PERFIL" --window-size=1920,1080 --screenshot "$salida" "file://$html" >/dev/null 2>&1
  # Una imagen nueva necesita su .meta con «Non-Power of 2» en None y sin mipmaps
  # (si no, Unity la reescala a 2048x1024 y el texto se ve borroso): se copia el
  # de la primera diapositiva con un guid nuevo.
  if [ ! -f "$salida.meta" ]; then
    sed "s/^guid: .*/guid: $(python3 -c 'import uuid; print(uuid.uuid4().hex)')/" "$DESTINO/slide_01.png.meta" > "$salida.meta"
  fi
  echo "$(basename "$html") -> $(basename "$salida")"
done
# Quedaron imágenes de más de una versión anterior con más diapositivas: se borran.
for viejo in "$DESTINO"/slide_*.png; do
  num=$((10#$(basename "$viejo" .png | sed 's/slide_//')))
  if [ "$num" -gt "$n" ]; then rm -f "$viejo" "$viejo.meta"; echo "borrada $(basename "$viejo")"; fi
done
